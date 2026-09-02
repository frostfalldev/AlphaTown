using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Bootstrap;
using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Cuts every ripe crop the finger passes over, and draws the blade doing it.
    ///
    /// No longer reads input. <see cref="TownGestures"/> decides that a drag is a harvest — because
    /// it began on a crop that was ready — and drives this; before, it polled the pointer itself
    /// and fought the camera for the same finger.
    ///
    /// Each cell is cut at most once per swipe, so dragging back across a field the player just
    /// cleared does not fight the auto-replant. Every cut goes through
    /// <c>TownCommands.HarvestAt</c>, so the same barn-space and readiness rules apply as a tap.
    /// </summary>
    public sealed class SickleSwipeHarvestController : MonoBehaviour
    {
        [SerializeField] GameRunner _runner;

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

        Vector3 _lastSamplePosition;
        bool _isSwiping;
        int _harvestedThisSwipe;

        void Awake()
        {
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_sickleTrail != null) _sickleTrail.emitting = false;
        }

        public void BeginSwipe(Vector3 worldPosition)
        {
            _isSwiping = true;
            _harvestedThisSwipe = 0;
            _cutThisSwipe.Clear();
            _lastSamplePosition = worldPosition;

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

        public void EndSwipe()
        {
            if (_sickleTrail != null) _sickleTrail.emitting = false;
            if (!_isSwiping) return;

            _isSwiping = false;

            // One save for the whole sweep rather than one per field.
            if (_harvestedThisSwipe > 0) _runner.RequestSave();
        }

        void CutAt(Vector3 worldPosition)
        {
            var cell = IsoGridMath.WorldToGrid(worldPosition);
            if (!_cutThisSwipe.Add(cell)) return;

            if (_runner != null && _runner.Commands != null && _runner.Commands.HarvestAt(cell))
                _harvestedThisSwipe++;
        }
    }
}
