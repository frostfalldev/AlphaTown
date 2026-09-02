using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Bootstrap;
using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Drag across the fields to harvest them, the way a sickle would cut.
    ///
    /// The feel of the whole farming loop lives here: tapping ten fields is a chore, sweeping a
    /// finger over them is the reason to come back. Each cell may only be cut once per swipe, so
    /// dragging back over a field the player just cleared does not fight the auto-replant.
    ///
    /// The simulation is untouched — every cut goes through <c>TownCommands.HarvestAt</c>, which
    /// applies the same barn-space and readiness rules a tap would.
    /// </summary>
    public sealed class SickleSwipeHarvestController : MonoBehaviour
    {
        [SerializeField] GameRunner _runner;
        [SerializeField] Camera _camera;

        [Header("Feel")]
        [SerializeField, Min(1f)]
        [Tooltip("Screen pixels the finger must travel before a press becomes a swipe, so a tap " +
                 "to select does not harvest what it landed on.")]
        float _swipeThresholdPixels = 26f;

        [Header("Visual Feedback")]
        [SerializeField] TrailRenderer _sickleTrail;
        [SerializeField] ParticleSystem _swipeParticles;

        readonly HashSet<GridPosition> _cutThisSwipe = new HashSet<GridPosition>();

        Vector2 _pressPosition;
        bool _pressed;
        bool _isSwiping;
        int _harvestedThisSwipe;

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_sickleTrail != null) _sickleTrail.emitting = false;
        }

        void Update()
        {
            if (_runner == null || _runner.Commands == null || _camera == null) return;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began) BeginPress(touch.position);
                else if (touch.phase == TouchPhase.Moved) DragTo(touch.position);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) EndSwipe();

                return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0)) BeginPress(Input.mousePosition);
            else if (Input.GetMouseButton(0)) DragTo(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) EndSwipe();
#endif
        }

        void BeginPress(Vector2 screenPosition)
        {
            _pressed = true;
            _isSwiping = false;
            _pressPosition = screenPosition;
            _harvestedThisSwipe = 0;
            _cutThisSwipe.Clear();
        }

        void DragTo(Vector2 screenPosition)
        {
            if (!_pressed) return;

            // A swipe only begins once the finger has actually moved. Below the threshold this is
            // still a tap, and the tap handler owns it.
            if (!_isSwiping)
            {
                if ((screenPosition - _pressPosition).sqrMagnitude < _swipeThresholdPixels * _swipeThresholdPixels)
                    return;

                BeginSwipe(ScreenToWorld(_pressPosition));
            }

            var world = ScreenToWorld(screenPosition);
            if (_sickleTrail != null) _sickleTrail.transform.position = world;

            if (_swipeParticles != null && !_swipeParticles.isPlaying)
            {
                _swipeParticles.transform.position = world;
                _swipeParticles.Play();
            }

            CutAt(world);
        }

        void BeginSwipe(Vector3 worldPosition)
        {
            _isSwiping = true;

            if (_sickleTrail == null) return;

            _sickleTrail.transform.position = worldPosition;
            _sickleTrail.Clear();
            _sickleTrail.emitting = true;

            CutAt(worldPosition);
        }

        void EndSwipe()
        {
            _pressed = false;

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

            if (_runner.Commands.HarvestAt(cell)) _harvestedThisSwipe++;
        }

        Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var world = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));

            world.z = 0f;
            return world;
        }
    }
}
