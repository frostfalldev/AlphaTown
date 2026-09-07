using AlphaTown.Core.Spatial;
using UnityEngine;

namespace AlphaTown.UI.CameraControl
{
    /// <summary>
    /// Orthographic isometric camera with inertia, damping and bounds clamping.
    ///
    /// It does not read input. It used to, and that was the bug: the camera, the tap handler and
    /// the sickle each polled the same finger and each acted on it, so a single drag panned the
    /// map and harvested every crop it crossed at the same time. Gestures are now recognised once,
    /// by <c>TownGestures</c>, and this is told what to do — which also means the camera can be
    /// driven from a test, a tutorial or a "focus on this building" button without faking input.
    ///
    /// Scene glue only: it moves a transform and knows nothing about the simulation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float _minOrthoSize = 4f;
        [SerializeField] private float _maxOrthoSize = 14f;
        [SerializeField] private float _zoomDamping = 10f;

        [Header("Pan & Inertia Settings")]
        [SerializeField] private float _panDamping = 12f;
        [SerializeField] private float _inertiaDecay = 0.92f;

        [Header("Grid Boundaries")]
        [SerializeField] private Vector2Int _gridSize = new Vector2Int(100, 100);

        [SerializeField, Min(0f)]
        [Tooltip("World units of slack around the map, so the edge tiles are not jammed against " +
                 "the screen edge.")]
        private float _boundsPadding = 2f;

        private Camera _cam;
        private Vector3 _targetPosition;
        private float _targetOrthoSize;

        private Vector3 _panVelocity;
        private bool _isPanning;

        private Vector2 _minWorldBounds;
        private Vector2 _maxWorldBounds;

        public float OrthoSize => _cam != null ? _cam.orthographicSize : _targetOrthoSize;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _targetOrthoSize = Mathf.Clamp(_cam.orthographicSize, _minOrthoSize, _maxOrthoSize);
            _targetPosition = transform.position;

            CalculateMapBounds();
        }

        /// <summary>Keeps the camera over the town. Call again if the grid is resized.</summary>
        public void SetGridSize(Vector2Int gridSize)
        {
            _gridSize = gridSize;
            CalculateMapBounds();
        }

        // --- Gesture API ------------------------------------------------------------------------

        /// <summary>A finger has taken hold of the map. Cancels any inertia still running.</summary>
        public void BeginPan()
        {
            _isPanning = true;
            _panVelocity = Vector3.zero;
        }

        /// <summary>
        /// Drags the map by a screen-space delta. The world moves with the finger, so the camera
        /// moves against it.
        /// </summary>
        public void PanByScreenDelta(Vector2 screenDelta, float deltaSeconds)
        {
            if (!_isPanning) BeginPan();

            var worldDelta = ScreenToWorldDelta(screenDelta);
            _targetPosition -= worldDelta;

            // Guarded because a frame can report zero elapsed time, and dividing by it would send
            // the inertia to infinity and the camera to NaN.
            if (deltaSeconds > 0f) _panVelocity = -worldDelta / deltaSeconds;
        }

        /// <summary>Releases the map and lets the throw carry it.</summary>
        public void EndPan() => _isPanning = false;

        /// <summary>
        /// Pinch. <paramref name="pixelDelta"/> is how much further apart the fingers moved, so
        /// spreading them zooms in.
        /// </summary>
        public void ZoomByPinch(float pixelDelta)
        {
            // Scaled against screen height rather than a raw pixel constant: the same physical
            // gesture then produces the same zoom on a 720p phone and a 1440p one.
            var normalised = pixelDelta / Mathf.Max(1, Screen.height);
            ZoomBy(-normalised * _targetOrthoSize * 2.5f);
        }

        /// <summary>Mouse wheel, in notches. Editor and desktop only in practice.</summary>
        public void ZoomByScroll(float notches) => ZoomBy(-notches * _targetOrthoSize * 0.12f);

        public void ZoomBy(float increment) =>
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + increment, _minOrthoSize, _maxOrthoSize);

        public void FocusOnGridCell(Vector2Int gridPos)
        {
            var targetWorld = IsoGridMath.GridToWorld(gridPos.x, gridPos.y);
            _targetPosition = new Vector3(targetWorld.x, targetWorld.y, transform.position.z);
            _panVelocity = Vector3.zero;
        }

        // --- Movement ---------------------------------------------------------------------------

        private void LateUpdate()
        {
            ApplyInertiaAndDamping();
            ClampCameraToBounds();
        }

        private void CalculateMapBounds()
        {
            // The four grid corners project to a diamond, so the extents come from the corners
            // rather than from the grid dimensions directly.
            var origin = IsoGridMath.GridToWorld(0, 0);
            var cornerX = IsoGridMath.GridToWorld(_gridSize.x, 0);
            var cornerY = IsoGridMath.GridToWorld(0, _gridSize.y);
            var cornerFar = IsoGridMath.GridToWorld(_gridSize.x, _gridSize.y);

            var minX = Mathf.Min(origin.x, cornerX.x, cornerY.x, cornerFar.x);
            var maxX = Mathf.Max(origin.x, cornerX.x, cornerY.x, cornerFar.x);
            var minY = Mathf.Min(origin.y, cornerX.y, cornerY.y, cornerFar.y);
            var maxY = Mathf.Max(origin.y, cornerX.y, cornerY.y, cornerFar.y);

            _minWorldBounds = new Vector2(minX - _boundsPadding, minY - _boundsPadding);
            _maxWorldBounds = new Vector2(maxX + _boundsPadding, maxY + _boundsPadding);
        }

        private Vector3 ScreenToWorldDelta(Vector2 screenDelta)
        {
            var vertSize = _cam.orthographicSize * 2f;
            var horizSize = vertSize * _cam.aspect;

            return new Vector3(
                screenDelta.x / Mathf.Max(1, Screen.width) * horizSize,
                screenDelta.y / Mathf.Max(1, Screen.height) * vertSize,
                0f);
        }

        private void ApplyInertiaAndDamping()
        {
            var delta = Time.unscaledDeltaTime;

            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, delta * _zoomDamping);

            if (!_isPanning && _panVelocity.sqrMagnitude > 0.01f)
            {
                _targetPosition += _panVelocity * delta;
                _panVelocity *= _inertiaDecay;
            }

            transform.position = Vector3.Lerp(transform.position, _targetPosition, delta * _panDamping);
        }

        private void ClampCameraToBounds()
        {
            var vertExtent = _cam.orthographicSize;
            var horizExtent = vertExtent * _cam.aspect;

            var minX = _minWorldBounds.x + horizExtent;
            var maxX = _maxWorldBounds.x - horizExtent;
            var minY = _minWorldBounds.y + vertExtent;
            var maxY = _maxWorldBounds.y - vertExtent;

            // Zoomed out far enough to see the whole map, there is nothing left to pan: centre it
            // rather than letting Mathf.Clamp misbehave on an inverted range.
            if (minX > maxX) minX = maxX = (_minWorldBounds.x + _maxWorldBounds.x) * 0.5f;
            if (minY > maxY) minY = maxY = (_minWorldBounds.y + _maxWorldBounds.y) * 0.5f;

            var clampedX = Mathf.Clamp(_targetPosition.x, minX, maxX);
            var clampedY = Mathf.Clamp(_targetPosition.y, minY, maxY);

            // Killing the velocity at the edge stops inertia from grinding against the boundary
            // for a second after the finger has gone.
            if (!Mathf.Approximately(clampedX, _targetPosition.x)) _panVelocity.x = 0f;
            if (!Mathf.Approximately(clampedY, _targetPosition.y)) _panVelocity.y = 0f;

            _targetPosition = new Vector3(clampedX, clampedY, transform.position.z);

            var current = transform.position;
            current.x = Mathf.Clamp(current.x, minX, maxX);
            current.y = Mathf.Clamp(current.y, minY, maxY);
            transform.position = current;
        }
    }
}
