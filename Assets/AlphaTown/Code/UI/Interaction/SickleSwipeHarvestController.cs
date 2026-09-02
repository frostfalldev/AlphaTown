using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.UI.Selection;
using AlphaTown.UI.View;
using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// The sickle: a tool the player picks up, sees in their hand, and sweeps across the fields.
    ///
    /// Arming is explicit — select a ripe field, tap Sickle — rather than inferred from where a
    /// drag happened to begin. An inferred gesture has to be discovered, and when it does not fire
    /// there is nothing to look at that explains why. A held tool is visible, so "am I in sickle
    /// mode" is answered by looking at the screen.
    ///
    /// It draws itself and follows the finger, but it does not read input:
    /// <see cref="TownGestures"/> owns that and drives this. Every cut goes through
    /// <c>TownCommands.HarvestAt</c>, so the same barn-space and readiness rules apply as a tap.
    /// </summary>
    public sealed class SickleSwipeHarvestController : MonoBehaviour
    {
        [SerializeField] GameRunner _runner;
        [SerializeField] TownTool _tool;
        [SerializeField] TownSelection _selection;

        [Header("Blade")]
        [SerializeField]
        [Tooltip("Optional. A sickle is drawn in code when this is empty.")]
        Sprite _bladeSprite;

        [SerializeField, Min(0.1f)] float _bladeScale = 1.4f;

        [SerializeField]
        [Tooltip("Sorting order for the blade. Above every building so it is never hidden by one.")]
        int _bladeSortingOrder = 30000;

        [Header("Visual Feedback")]
        [SerializeField] TrailRenderer _sickleTrail;
        [SerializeField] ParticleSystem _swipeParticles;

        [Header("Sampling")]
        [SerializeField, Min(0.05f)]
        [Tooltip("World units between samples along the swipe. Smaller catches more tiles on a " +
                 "fast flick; larger costs less.")]
        float _sampleSpacing = 0.25f;

        [SerializeField, Min(2)]
        [Tooltip("Cap on samples per frame, so a teleporting pointer cannot stall a frame.")]
        int _maxSamplesPerSegment = 64;

        readonly HashSet<GridPosition> _cutThisSwipe = new HashSet<GridPosition>();

        SpriteRenderer _blade;
        Vector3 _lastSamplePosition;
        bool _isSwiping;
        int _harvestedThisSwipe;

        /// <summary>How many crops the last completed sweep took. Read by the HUD for its toast.</summary>
        public int LastSwipeHarvestCount { get; private set; }

        void Awake()
        {
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_tool == null) _tool = FindAnyObjectByType<TownTool>();
            if (_selection == null) _selection = FindAnyObjectByType<TownSelection>();

            if (_sickleTrail != null) _sickleTrail.emitting = false;

            CreateBlade();
        }

        void OnEnable()
        {
            if (_tool != null) _tool.Changed += OnToolChanged;
            if (_selection != null) _selection.Changed += OnSelectionChanged;
        }

        void OnDisable()
        {
            if (_tool != null) _tool.Changed -= OnToolChanged;
            if (_selection != null) _selection.Changed -= OnSelectionChanged;
        }

        void Start() => OnToolChanged();

        public bool IsArmed => _tool != null && _tool.IsSickleArmed;

        // --- Swipe ------------------------------------------------------------------------------

        public void BeginSwipe(Vector3 worldPosition)
        {
            _isSwiping = true;
            _harvestedThisSwipe = 0;
            _cutThisSwipe.Clear();
            _lastSamplePosition = worldPosition;

            MoveBladeTo(worldPosition);

            if (_sickleTrail != null)
            {
                _sickleTrail.transform.position = worldPosition;
                _sickleTrail.Clear();
                _sickleTrail.emitting = true;
            }

            CutAt(worldPosition);
        }

        /// <summary>
        /// Continues the swipe to a new point, cutting everything on the way.
        ///
        /// The path between two frames is walked rather than just its endpoint sampled. A finger
        /// crossing the screen in a flick moves several tiles per frame, and sampling only where
        /// it landed would skip most of the row — which reads, fairly, as the sickle not working.
        /// </summary>
        public void CutAlong(Vector3 worldPosition)
        {
            if (!_isSwiping) return;

            AimBladeAlong(_lastSamplePosition, worldPosition);
            MoveBladeTo(worldPosition);

            if (_sickleTrail != null) _sickleTrail.transform.position = worldPosition;

            if (_swipeParticles != null)
            {
                _swipeParticles.transform.position = worldPosition;
                if (!_swipeParticles.isPlaying) _swipeParticles.Play();
            }

            var travelled = Vector3.Distance(_lastSamplePosition, worldPosition);
            var steps = Mathf.Clamp(Mathf.CeilToInt(travelled / _sampleSpacing), 1, _maxSamplesPerSegment);

            for (var i = 1; i <= steps; i++)
            {
                CutAt(Vector3.Lerp(_lastSamplePosition, worldPosition, (float)i / steps));
            }

            _lastSamplePosition = worldPosition;
        }

        /// <summary>Ends the sweep and returns how many crops it took.</summary>
        public int EndSwipe()
        {
            if (_sickleTrail != null) _sickleTrail.emitting = false;
            if (!_isSwiping) return 0;

            _isSwiping = false;
            LastSwipeHarvestCount = _harvestedThisSwipe;

            // One save for the whole sweep rather than one per field.
            if (_harvestedThisSwipe > 0) _runner.RequestSave();

            RestBladeOverSelection();
            return _harvestedThisSwipe;
        }

        void CutAt(Vector3 worldPosition)
        {
            var cell = IsoGridMath.WorldToGrid(worldPosition);
            if (!_cutThisSwipe.Add(cell)) return;

            if (_runner != null && _runner.Commands != null && _runner.Commands.HarvestAt(cell))
                _harvestedThisSwipe++;
        }

        // --- The blade --------------------------------------------------------------------------

        void CreateBlade()
        {
            var go = new GameObject("Sickle Blade");
            go.transform.SetParent(transform, false);

            _blade = go.AddComponent<SpriteRenderer>();
            _blade.sprite = _bladeSprite != null ? _bladeSprite : PlaceholderArt.Sickle();
            _blade.sortingOrder = _bladeSortingOrder;
            _blade.enabled = false;

            go.transform.localScale = Vector3.one * _bladeScale;
        }

        void OnToolChanged()
        {
            if (_blade == null) return;

            _blade.enabled = IsArmed;
            if (IsArmed) RestBladeOverSelection();
            else if (_sickleTrail != null) _sickleTrail.emitting = false;
        }

        void OnSelectionChanged()
        {
            if (IsArmed && !_isSwiping) RestBladeOverSelection();
        }

        /// <summary>
        /// Parks the blade over whatever is selected, so arming it puts the tool somewhere the
        /// player is already looking rather than at the world origin.
        /// </summary>
        void RestBladeOverSelection()
        {
            if (_blade == null || !IsArmed || _selection == null || !_selection.HasCell) return;

            var world = _runner != null && _runner.World != null &&
                        _runner.World.Buildings.TryGetBuilding(_selection.BuildingInstanceId, out var building)
                ? IsoGridMath.RectCentreToWorld(building.Footprint)
                : IsoGridMath.GridToWorld(_selection.Cell.X, _selection.Cell.Y);

            MoveBladeTo(world);
        }

        void MoveBladeTo(Vector3 worldPosition)
        {
            if (_blade == null) return;

            worldPosition.z = 0f;
            _blade.transform.position = worldPosition;
        }

        /// <summary>Points the blade the way the finger is travelling, so it swings rather than slides.</summary>
        void AimBladeAlong(Vector3 from, Vector3 to)
        {
            if (_blade == null) return;

            var direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;

            var degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // The drawn blade points up and right, so it is offset back to face along the travel.
            _blade.transform.rotation = Quaternion.Euler(0f, 0f, degrees - 45f);
        }
    }
}
