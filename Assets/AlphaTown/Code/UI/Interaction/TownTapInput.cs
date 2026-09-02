using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.UI.Selection;
using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Turns a tap into a grid cell and a selection.
    ///
    /// No colliders and no raycasts: the map is a regular grid, so the cell under a finger is
    /// arithmetic — screen to world, world to cell. That keeps a town of a thousand tiles free of
    /// a thousand colliders, and it works identically in a test.
    ///
    /// A tap is separated from a drag by distance and duration, so panning the camera does not
    /// also select whatever the finger started on.
    /// </summary>
    [RequireComponent(typeof(TownSelection))]
    public sealed class TownTapInput : MonoBehaviour
    {
        [SerializeField] GameRunner _runner;
        [SerializeField] Camera _camera;

        [SerializeField, Min(1f)]
        [Tooltip("Screen pixels the finger may travel and still count as a tap, not a drag.")]
        float _tapSlopPixels = 24f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds a press may last and still count as a tap.")]
        float _tapMaxSeconds = 0.6f;

        TownSelection _selection;
        Vector2 _pressPosition;
        float _pressTime;
        bool _pressed;

        void Awake()
        {
            _selection = GetComponent<TownSelection>();
            if (_camera == null) _camera = Camera.main;
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
        }

        void Update()
        {
            if (_runner == null || _runner.World == null || _camera == null) return;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began: BeginPress(touch.position); break;
                    case TouchPhase.Ended: EndPress(touch.position); break;
                    case TouchPhase.Canceled: _pressed = false; break;
                }

                return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0)) BeginPress(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) EndPress(Input.mousePosition);
#endif
        }

        void BeginPress(Vector2 screenPosition)
        {
            _pressed = true;
            _pressPosition = screenPosition;
            _pressTime = Time.unscaledTime;
        }

        void EndPress(Vector2 screenPosition)
        {
            if (!_pressed) return;
            _pressed = false;

            if (Time.unscaledTime - _pressTime > _tapMaxSeconds) return;
            if ((screenPosition - _pressPosition).sqrMagnitude > _tapSlopPixels * _tapSlopPixels) return;

            Select(screenPosition);
        }

        void Select(Vector2 screenPosition)
        {
            var world = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));

            var cell = IsoGridMath.WorldToGrid(world);
            var grid = _runner.World.Buildings.Grid;

            if (!grid.IsInBounds(cell))
            {
                _selection.Clear();
                return;
            }

            _selection.Select(cell, grid.TryGetOccupant(cell, out var occupant) ? occupant : string.Empty);
        }
    }
}
