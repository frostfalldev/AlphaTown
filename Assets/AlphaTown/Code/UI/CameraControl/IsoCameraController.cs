using AlphaTown.Core.Spatial;
using UnityEngine;

namespace AlphaTown.UI.CameraControl
{
    /// <summary>
    /// Orthographic isometric camera with touch pan, pinch zoom, inertia and bounds clamping.
    ///
    /// Scene glue only: it moves a transform and knows nothing about the simulation, which is why
    /// it lives in the UI assembly rather than anywhere the game logic can reach.
    ///
    /// Uses the legacy Input class, so Active Input Handling must be "Both" or "Input Manager
    /// (Old)" — the project settings pass sets that.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float _minOrthoSize = 4f;
        [SerializeField] private float _maxOrthoSize = 14f;
        [SerializeField] private float _zoomSensitivity = 0.01f;
        [SerializeField] private float _mouseScrollSensitivity = 2f;
        [SerializeField] private float _zoomDamping = 10f;

        [Header("Pan & Inertia Settings")]
        [SerializeField] private float _panDamping = 12f;
        [SerializeField] private float _inertiaDecay = 0.92f;

        [Header("Grid Boundaries")]
        [SerializeField] private Vector2Int _gridSize = new Vector2Int(100, 100);

        private Camera _cam;
        private Vector3 _targetPosition;
        private float _targetOrthoSize;

        private Vector3 _panVelocity;
        private Vector2 _lastTouchPosition;
        private bool _isDragging;

        private Vector2 _minWorldBounds;
        private Vector2 _maxWorldBounds;

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

        private void CalculateMapBounds()
        {
            Vector3 origin = IsoGridMath.GridToWorld(0, 0);
            Vector3 cornerX = IsoGridMath.GridToWorld(_gridSize.x, 0);
            Vector3 cornerY = IsoGridMath.GridToWorld(0, _gridSize.y);
            Vector3 cornerFar = IsoGridMath.GridToWorld(_gridSize.x, _gridSize.y);

            float minX = Mathf.Min(origin.x, cornerX.x, cornerY.x, cornerFar.x);
            float maxX = Mathf.Max(origin.x, cornerX.x, cornerY.x, cornerFar.x);
            float minY = Mathf.Min(origin.y, cornerX.y, cornerY.y, cornerFar.y);
            float maxY = Mathf.Max(origin.y, cornerX.y, cornerY.y, cornerFar.y);

            _minWorldBounds = new Vector2(minX - 2f, minY - 2f);
            _maxWorldBounds = new Vector2(maxX + 2f, maxY + 2f);
        }

        private void LateUpdate()
        {
            HandleTouchInput();
            HandleMouseInput();

            ApplyInertiaAndDamping();
            ClampCameraToBounds();
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    _isDragging = true;
                    _lastTouchPosition = touch.position;
                    _panVelocity = Vector3.zero;
                }
                else if (touch.phase == TouchPhase.Moved && _isDragging)
                {
                    Vector2 delta = touch.position - _lastTouchPosition;
                    _lastTouchPosition = touch.position;

                    Vector3 worldDelta = ScreenToWorldDelta(delta);
                    _targetPosition -= worldDelta;
                    _panVelocity = -worldDelta / Time.deltaTime;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _isDragging = false;
                }
            }
            else if (Input.touchCount == 2)
            {
                _isDragging = false;

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
                float currentMagnitude = (touch0.position - touch1.position).magnitude;

                float difference = currentMagnitude - prevMagnitude;

                Zoom(-difference * _zoomSensitivity);
            }
        }

        private void HandleMouseInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Zoom(-scroll * _mouseScrollSensitivity);
            }

            if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftShift)))
            {
                _isDragging = true;
                _lastTouchPosition = Input.mousePosition;
                _panVelocity = Vector3.zero;
            }
            else if (Input.GetMouseButton(2) || (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift)))
            {
                Vector2 currentMousePos = Input.mousePosition;
                Vector2 delta = currentMousePos - _lastTouchPosition;
                _lastTouchPosition = currentMousePos;

                Vector3 worldDelta = ScreenToWorldDelta(delta);
                _targetPosition -= worldDelta;
                _panVelocity = -worldDelta / Time.deltaTime;
            }
            else if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }
#endif
        }

        private void Zoom(float increment)
        {
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + increment, _minOrthoSize, _maxOrthoSize);
        }

        private Vector3 ScreenToWorldDelta(Vector2 screenDelta)
        {
            float vertSize = _cam.orthographicSize * 2.0f;
            float horizSize = vertSize * _cam.aspect;

            float worldX = (screenDelta.x / Screen.width) * horizSize;
            float worldY = (screenDelta.y / Screen.height) * vertSize;

            return new Vector3(worldX, worldY, 0f);
        }

        private void ApplyInertiaAndDamping()
        {
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, Time.deltaTime * _zoomDamping);

            if (!_isDragging && _panVelocity.sqrMagnitude > 0.01f)
            {
                _targetPosition += _panVelocity * Time.deltaTime;
                _panVelocity *= _inertiaDecay;
            }

            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _panDamping);
        }

        private void ClampCameraToBounds()
        {
            float vertExtent = _cam.orthographicSize;
            float horizExtent = vertExtent * _cam.aspect;

            float minX = _minWorldBounds.x + horizExtent;
            float maxX = _maxWorldBounds.x - horizExtent;
            float minY = _minWorldBounds.y + vertExtent;
            float maxY = _maxWorldBounds.y - vertExtent;

            if (minX > maxX) minX = maxX = (_minWorldBounds.x + _maxWorldBounds.x) * 0.5f;
            if (minY > maxY) minY = maxY = (_minWorldBounds.y + _maxWorldBounds.y) * 0.5f;

            float clampedX = Mathf.Clamp(_targetPosition.x, minX, maxX);
            float clampedY = Mathf.Clamp(_targetPosition.y, minY, maxY);

            _targetPosition = new Vector3(clampedX, clampedY, transform.position.z);

            Vector3 currentPos = transform.position;
            currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
            currentPos.y = Mathf.Clamp(currentPos.y, minY, maxY);
            transform.position = currentPos;
        }

        public void FocusOnGridCell(Vector2Int gridPos)
        {
            Vector3 targetWorld = IsoGridMath.GridToWorld(gridPos.x, gridPos.y);
            _targetPosition = new Vector3(targetWorld.x, targetWorld.y, transform.position.z);
            _panVelocity = Vector3.zero;
        }
    }
}
