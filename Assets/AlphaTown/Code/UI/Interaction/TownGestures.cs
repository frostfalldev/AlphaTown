using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.UI.CameraControl;
using AlphaTown.UI.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// The one place a touch is interpreted: tap to select, drag to pan, drag across ripe crops to
    /// harvest, pinch to zoom.
    ///
    /// Three components used to poll the raw pointer independently and all act on the same finger,
    /// so a single drag panned the camera and swung the sickle at once — you could not move the
    /// map without mowing everything you crossed. Recognising gestures once and dispatching the
    /// result fixes that by construction: a press resolves to exactly one gesture and stays there
    /// until the finger lifts.
    ///
    /// One finger drags the map, unless the sickle is in hand — then one finger swings it. Two
    /// fingers always drive the camera, so the map can still be moved and zoomed with the tool
    /// out and there is no way to get stranded in the mode.
    ///
    /// This replaced an earlier rule that inferred the sickle from a drag beginning on a ripe
    /// crop. It worked, but a gesture nobody told you about is a gesture that reads as broken when
    /// it does not fire, and it quietly stole panning from every tile that happened to be ready.
    /// An armed tool you can see is worth the extra tap.
    /// </summary>
    [RequireComponent(typeof(TownSelection))]
    public sealed class TownGestures : MonoBehaviour
    {
        enum Gesture
        {
            /// <summary>A finger is down but has not moved far enough to say what it wants.</summary>
            Undecided = 0,
            Pan = 1,
            Harvest = 2,
            Pinch = 3,

            /// <summary>Started on the HUD. The world ignores it for the rest of the press.</summary>
            OverUi = 4,

            /// <summary>Nothing is down.</summary>
            Idle = 5
        }

        [SerializeField] GameRunner _runner;
        [SerializeField] Camera _camera;
        [SerializeField] IsoCameraController _cameraController;
        [SerializeField] UIDocument _hudDocument;
        [SerializeField] SickleSwipeHarvestController _sickle;
        [SerializeField] TownTool _tool;

        [Header("Feel")]
        [SerializeField, Min(1f)]
        [Tooltip("Screen pixels a finger may travel and still count as a tap.")]
        float _tapSlopPixels = 24f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds a press may last and still count as a tap.")]
        float _tapMaxSeconds = 0.7f;

        readonly List<BuildingInstance> _harvestable = new List<BuildingInstance>(32);

        TownSelection _selection;
        Gesture _gesture = Gesture.Idle;
        Vector2 _pressPosition;
        Vector2 _lastPosition;
        float _pressTime;
        int _pointerId;
        int _previousCount;
        float _previousPinchDistance;
        Vector2 _previousPinchCentre;

        void Awake()
        {
            _selection = GetComponent<TownSelection>();
            if (_camera == null) _camera = Camera.main;
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_cameraController == null && _camera != null)
                _cameraController = _camera.GetComponent<IsoCameraController>();
            if (_sickle == null) _sickle = FindAnyObjectByType<SickleSwipeHarvestController>();
            if (_tool == null) _tool = FindAnyObjectByType<TownTool>();

            // Without a document to hit-test, every tap on the HUD would also land on the tile
            // behind it, so it is worth finding one rather than silently going without.
            if (_hudDocument == null) _hudDocument = FindAnyObjectByType<UIDocument>();
        }

        void Update()
        {
            if (_runner == null || _runner.World == null || _camera == null) return;

            var count = PointerInput.Count;

            if (count >= 2) UpdatePinch();
            else if (count == 1) UpdateSinglePointer();
            else if (_previousCount > 0) EndPress();

            HandleScroll();
            _previousCount = count;
        }

        // --- One finger ---------------------------------------------------------------------

        void UpdateSinglePointer()
        {
            if (!PointerInput.TryGetPrimary(out var id, out var position)) return;

            // A finger arriving after a pinch is a new press, not a continuation of the old one —
            // otherwise lifting one finger of a pinch would fling the camera.
            if (_previousCount != 1 || id != _pointerId)
            {
                BeginPress(id, position);
                return;
            }

            switch (_gesture)
            {
                case Gesture.OverUi:
                case Gesture.Idle:
                    return;

                case Gesture.Undecided:
                    if ((position - _pressPosition).sqrMagnitude < _tapSlopPixels * _tapSlopPixels) return;

                    // Far enough to be a drag, and with no tool in hand that always means the map.
                    _gesture = Gesture.Pan;
                    _cameraController?.BeginPan();
                    _lastPosition = _pressPosition;
                    break;
            }

            var delta = position - _lastPosition;
            _lastPosition = position;

            if (_gesture == Gesture.Pan) _cameraController?.PanByScreenDelta(delta, Time.unscaledDeltaTime);
            else if (_gesture == Gesture.Harvest) _sickle?.CutAlong(ScreenToWorld(position));
        }

        void BeginPress(int id, Vector2 position)
        {
            _pointerId = id;
            _pressPosition = position;
            _lastPosition = position;
            _pressTime = Time.unscaledTime;

            if (UiHitTest.IsOverUi(_hudDocument, position))
            {
                _gesture = Gesture.OverUi;
                return;
            }

            if (_sickle != null && _sickle.IsArmed)
            {
                // The blade starts cutting the moment it lands, so a single tap with the sickle
                // out takes that one crop. Waiting for movement would make the first tile of every
                // sweep the one tile it missed.
                _gesture = Gesture.Harvest;
                _sickle.BeginSwipe(ScreenToWorld(position));
                return;
            }

            _gesture = Gesture.Undecided;
        }

        void EndPress()
        {
            switch (_gesture)
            {
                case Gesture.Pan:
                    _cameraController?.EndPan();
                    break;

                case Gesture.Harvest:
                    if (_sickle != null) _sickle.EndSwipe();
                    DisarmIfNothingLeftToCut();
                    break;

                case Gesture.Undecided:
                    // Never travelled far enough to be a drag, so it was a tap — as long as it was
                    // not a long press waiting for a context menu that does not exist yet.
                    if (Time.unscaledTime - _pressTime <= _tapMaxSeconds) Select(_pressPosition);
                    break;
            }

            _gesture = Gesture.Idle;
            _previousPinchDistance = 0f;
        }

        // --- Two fingers --------------------------------------------------------------------

        /// <summary>
        /// Two fingers zoom by their separation and pan by their midpoint, both at once.
        ///
        /// Panning here is what makes an armed tool safe: the map is still fully navigable with
        /// the sickle out, so picking it up can never strand the player looking at the wrong
        /// corner of their town.
        /// </summary>
        void UpdatePinch()
        {
            // A second finger cancels whatever the first was doing. Cutting while the map moves
            // under the blade would harvest wherever the camera happened to slide.
            if (_gesture == Gesture.Pan) _cameraController?.EndPan();
            else if (_gesture == Gesture.Harvest && _sickle != null) _sickle.EndSwipe();

            _gesture = Gesture.Pinch;

            if (!PointerInput.TryGet(0, out _, out var first)) return;
            if (!PointerInput.TryGet(1, out _, out var second)) return;

            var distance = Vector2.Distance(first, second);
            var centre = (first + second) * 0.5f;

            // The first frame of a pinch has nothing to compare against, so it only records a
            // baseline. Measuring against zero would snap the camera to its zoom limit.
            if (_previousCount < 2 || _previousPinchDistance <= 0f)
            {
                _previousPinchDistance = distance;
                _previousPinchCentre = centre;
                return;
            }

            _cameraController?.ZoomByPinch(distance - _previousPinchDistance);
            _cameraController?.PanByScreenDelta(centre - _previousPinchCentre, Time.unscaledDeltaTime);
            _cameraController?.EndPan();

            _previousPinchDistance = distance;
            _previousPinchCentre = centre;
        }

        void HandleScroll()
        {
            var scroll = PointerInput.ScrollDelta;
            if (Mathf.Abs(scroll) > 0.01f) _cameraController?.ZoomByScroll(scroll);
        }

        // --- World queries ------------------------------------------------------------------

        void Select(Vector2 screenPosition)
        {
            var cell = CellUnder(screenPosition);
            var grid = _runner.World.Buildings.Grid;

            if (!grid.IsInBounds(cell))
            {
                _selection.Clear();
                return;
            }

            _selection.Select(cell, grid.TryGetOccupant(cell, out var occupant) ? occupant : string.Empty);
        }

        /// <summary>
        /// Puts the sickle away once the last ripe crop is gone.
        ///
        /// A tool that outlives its purpose is a mode the player has to remember to leave, and the
        /// first thing they will try after clearing the fields is to drag the map.
        /// </summary>
        void DisarmIfNothingLeftToCut()
        {
            if (_tool == null || !_tool.IsSickleArmed) return;

            _runner.Commands.CollectHarvestable(_harvestable);
            if (_harvestable.Count == 0) _tool.Clear();
        }

        GridPosition CellUnder(Vector2 screenPosition) => IsoGridMath.WorldToGrid(ScreenToWorld(screenPosition));

        /// <summary>
        /// Screen to the z = 0 plane. An orthographic projection does not change x or y with
        /// depth, so the z passed in is irrelevant and the result is simply flattened — which also
        /// means moving the camera along z can never break picking.
        /// </summary>
        Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var world = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
            world.z = 0f;
            return world;
        }
    }
}
