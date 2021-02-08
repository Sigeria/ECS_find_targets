using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ScrollCamera : MonoBehaviour {
	public enum ReliefScrollerState {
		None = 0,
		TestScrolling,
		Scrolling,
		Inertia
	}

	[Header("Main Camera Settings")]
	[SerializeField]
	private float _dragThreshold = .05f;

	[SerializeField] private Vector3 _maxCameraPosition;
	[SerializeField] private Vector3 _minCameraPosition;

	[SerializeField] private float MainSpeed = 3;
	[SerializeField] private float ZoomSpeed = 6f;
	[SerializeField] private AnimationCurve _zoomByRotationCurve;
	private readonly float _mouseWheelInfluence = 0.25f;
	[SerializeField] private float _zoomFMin = 0.05f;
	[SerializeField] private float _zoomFMax = 1f;
	[SerializeField] private float _minAngle = 15;
	[SerializeField] private float _maxAngle = 75;

	private StartScrollInfo _startScrollInfo = new StartScrollInfo {HitPlane = new Plane()};
	private Camera _unityCamera;
	private Transform _unityCameraTransform;

	private Vector3 _downCursorPosition;
	private int _hitPlaneMask;
	private bool _isBlocked;
	private bool _isZoomToFactor;
	private Vector3 _lastDelta;
	private bool _scrollDisabled;
	private float _targetFactor;
	private Vector3 _targetPosition;
	private bool _zoomDisabled;
	private ZoomingStateParams _zoomingParams;
	private ReliefScrollerState _scrollState = ReliefScrollerState.None;
	private int _lockCount;
	private float _zoomFactor = 1;
	private Vector3 _cameraCachedPosition;
		
	private bool HasZoomingTouches { get; set; }
	private bool IsScrollToPosition { get; set; }
	private bool IsZooming { get; set; }
		
	private ReliefScrollerState State {
		get => _scrollState;
		set => _scrollState = value;
	}

	public Camera UnityCamera => _unityCamera;

	private void Awake() {
		_unityCamera = GetComponentInChildren<Camera>();
		_unityCameraTransform = _unityCamera.transform;
		_hitPlaneMask = LayerMask.GetMask("CameraHitPlane");
		_cameraCachedPosition = _unityCamera.transform.localPosition;

		_unityCamera.transparencySortMode = TransparencySortMode.Perspective;
	}

	public void SetSize(int size) {
		_zoomFMax = (float)size / 25;
		_zoomFactor = _zoomFMax;
		_minCameraPosition = new Vector3(0, 0, 0 - size * 0.2f);
		_maxCameraPosition = new Vector3(size, 0, size + size * 0.2f);
		SetPosition(new Vector3(size/2, 0, size/2));
	}

	public void UpdateCamera() {
		UpdateScroll();
		UpdateAngle();
		UpdateZooming();
		ScrollToPosition();
		ZoomToFactor();
	}

	private void UpdateAngle() {
		if (!Input.GetMouseButton(1)) {
			return;
		}
		
		var xAngleDelta = -Input.GetAxis("Mouse Y") * 2;
		_unityCameraTransform.Rotate(xAngleDelta, 0, 0);
		var xAngle =_unityCameraTransform.localEulerAngles.x;
		xAngle = Mathf.Max(xAngle, _minAngle);
		xAngle = Mathf.Min(xAngle, _maxAngle);
		_unityCameraTransform.localEulerAngles = new Vector3(xAngle, 0, 0);
	}

	private void UpdateScroll() {
		if (_scrollDisabled) {
			return;
		}

		var cursorPosition = Input.mousePosition;

		if (Input.touchSupported && Input.touchCount >= 2) {
			State = ReliefScrollerState.None;
			return;
		}

		if (State == ReliefScrollerState.Scrolling) {
			DoScroll(cursorPosition);
		} else if (State == ReliefScrollerState.TestScrolling && IsLeftTapArea(cursorPosition)) {
			State = ReliefScrollerState.Scrolling;
			BeginScroll(cursorPosition);
		} else if (State == ReliefScrollerState.Inertia) {
			DoInertia();
		}

		if (Input.GetMouseButtonDown(0)) {
			if (IsTouchingUI()) {
				return;
			}
			State = ReliefScrollerState.TestScrolling;
			PrepareScroll(cursorPosition);
		} else if ((State == ReliefScrollerState.TestScrolling || State == ReliefScrollerState.Scrolling) &&
		           !Input.GetMouseButton(0)) {
			State = ReliefScrollerState.Inertia;
		} else if (State == ReliefScrollerState.Inertia && _lastDelta.magnitude < 0.001f) {
			State = ReliefScrollerState.None;
		}

		KeepCameraInsideBorders();
	}

	private void UpdateZooming() {
		if (!_zoomDisabled) {
			Touch[] touches = null;
			if (Input.touchSupported) {
				touches = Input.touches;
				if (touches == null || touches.Length == 0) {
					HasZoomingTouches = false;
				}
			}

			if (!IsZooming) {
				if (touches != null &&
				    touches.Length >= 2) {
					var touchA = touches[0];
					var touchB = touches[1];

					if (Vector2.Distance(touchA.position, touchB.position) < Mathf.Epsilon) {
						return;
					}

					IsZooming = true;
					HasZoomingTouches = true;

					_zoomingParams = new ZoomingStateParams(_zoomFactor, touchA, touchB);
				} else if (Input.mousePresent) {
					var wheel = Input.mouseScrollDelta.y;
					if (Mathf.Abs(wheel) > Mathf.Epsilon) {
						_targetFactor = _targetFactor * (1 + -wheel * _mouseWheelInfluence);
						_targetFactor = Mathf.Clamp(_targetFactor,
							_zoomFMin,
							_zoomFMax);
						_isZoomToFactor = true;
					}
				}
			} else {
				if (touches == null || touches.Length < 2) {
					IsZooming = false;
					_targetFactor = Mathf.Clamp(_targetFactor,
						_zoomFMin,
						_zoomFMax);
					_isZoomToFactor = true;
				} else {
					Touch touchA;
					Touch touchB;
					try {
						touchA = touches.First(t => t.fingerId == _zoomingParams.TouchA.Touch.fingerId);
						touchB = touches.First(t => t.fingerId == _zoomingParams.TouchB.Touch.fingerId);
					}
					catch (Exception) {
						IsZooming = false;
						_targetFactor = Mathf.Clamp(_targetFactor,
							_zoomFMin,
							_zoomFMax);
						_isZoomToFactor = true;
						return;
					}

					var delta = (touchA.position - touchB.position).magnitude;
					var scale = _zoomingParams.Delta / delta;
					_targetFactor = _zoomingParams.Factor * scale;
					_targetFactor = Mathf.Clamp(_targetFactor,
						_zoomFMin - 0.25f,
						_zoomFMax + 0.25f);
					_isZoomToFactor = true;
				}
			}
		}

		DoZooming();
	}

	private void ScrollToPosition() {
		if (!IsScrollToPosition) {
			return;
		}

		if (Vector3.Distance(transform.position, _targetPosition) > 0.1f) {
			SetPosition(Vector3.Lerp(transform.position, _targetPosition, Time.smoothDeltaTime * MainSpeed));
		} else {
			_targetPosition = transform.position;

			IsScrollToPosition = false;

			if (_isBlocked) {
				_isBlocked = false;
			}
		}
	}

	private void ZoomToFactor() {
		if (!_isZoomToFactor) {
			return;
		}

		if (Mathf.Abs(_zoomFactor - _targetFactor) > 0.01f) {
			_zoomFactor = Mathf.Lerp(_zoomFactor, _targetFactor, Time.deltaTime * ZoomSpeed);
		} else {
			_targetFactor = _zoomFactor;
			_zoomFactor = _targetFactor;
			_isZoomToFactor = false;
		}
	}

	public void Lock(bool includeLookAt = false) {
		_lockCount++;

		if (includeLookAt || !IsScrollToPosition) {
			_targetPosition = transform.position;
		}

		_scrollDisabled = true;
		State = ReliefScrollerState.None;
		_zoomDisabled = true;
		IsZooming = false;
		HasZoomingTouches = false;
	}

	public void Unlock() {
		_lockCount = 0;
		_scrollDisabled = false;
		_zoomDisabled = false;
	}

	public void DecreaseLock() {
		if (--_lockCount <= 0) {
			Unlock();
		}
	}

	private Vector3 _cachedResultPosition = Vector3.zero;

	private void KeepCameraInsideBorders(bool forceSetPosition = true) {
		var position = transform.position;

		var resultPosition = new Vector3(
			Mathf.Clamp(position.x, _minCameraPosition.x, _maxCameraPosition.x),
			position.y,
			Mathf.Clamp(position.z, _minCameraPosition.z, _maxCameraPosition.z)
		);

		if (forceSetPosition) {
			SetPosition(resultPosition);
		} else {
			if (Vector3.Distance(_cachedResultPosition, resultPosition) > 0.001f) {
				_cachedResultPosition = resultPosition;
			}
		}
	}

	private bool IsLeftTapArea(Vector3 cursorPosition) {
		var minSize = Mathf.Min(Screen.width, Screen.height);
		var cursorRelativeCoordinates = cursorPosition / minSize;
		var downRelativeCoordinates = _downCursorPosition / minSize;
		return Vector3.Distance(cursorRelativeCoordinates, downRelativeCoordinates) > _dragThreshold;
	}

	private void DoInertia() {
		_lastDelta = Vector3.Lerp(_lastDelta, Vector3.zero, 4 * Time.deltaTime);
		AddPosition(-_lastDelta);
	}

	private void PrepareScroll(Vector3 cursorPosition) {
		_lastDelta = Vector3.zero;
		_downCursorPosition = cursorPosition;
	}

	private void BeginScroll(Vector3 cursorPosition) {
		var ray = _unityCamera.ScreenPointToRay(cursorPosition);

		var dir = Vector3.up;

		Physics.Raycast(ray, out var hit, 1000, _hitPlaneMask);
		if (!(hit.collider is null)) {
			dir = hit.normal;
		}

		var cameraTransform = _unityCamera.transform;
		_startScrollInfo.OriginalCameraPos = cameraTransform.position;
		_startScrollInfo.LocalStartPoint = hit.point - _startScrollInfo.OriginalCameraPos;
		_startScrollInfo.StartRotation = cameraTransform.rotation;
		_startScrollInfo.OriginalNormal = dir;
		_startScrollInfo.HitPlane.SetNormalAndPosition(dir, hit.point);
	}

	private bool IsTouchingUI() {
		var currentObject = EventSystem.current.currentSelectedGameObject;
		if (currentObject is null) {
			return false;
		}
		
		var uiLayer = LayerMask.NameToLayer("UI");
		return currentObject.layer == uiLayer;
	}

	private void DoScroll(Vector3 cursorPosition) {
		var ray = _unityCamera.ScreenPointToRay(cursorPosition);
		var cameraTransform = _unityCamera.transform;
		var deltaRotation = cameraTransform.rotation * Quaternion.Inverse(_startScrollInfo.StartRotation);
		var rotatedLocal = deltaRotation * _startScrollInfo.LocalStartPoint;
		var rotatedNormal = deltaRotation * _startScrollInfo.OriginalNormal;
		var rotatedPoint = _startScrollInfo.OriginalCameraPos + rotatedLocal;
		_startScrollInfo.HitPlane.SetNormalAndPosition(rotatedNormal, rotatedPoint);
		_startScrollInfo.HitPlane.Raycast(ray, out var planeHitDistance);
		_lastDelta = ray.GetPoint(planeHitDistance) - rotatedPoint;
		AddPosition(-Vector3.Lerp(Vector3.zero, _lastDelta, .5f), true);
	}

	private void AddPosition(Vector3 addPosition, bool speedUp = false) {
		transform.position += new Vector3(addPosition.x, 0f, addPosition.z);
	}

	private void SetPosition(Vector3 setPosition) {
		transform.position = new Vector3(setPosition.x, 0f, setPosition.z);
	}

	private void DoZooming() {
		var posY = _zoomFactor * 50f;
		var zPos = -Mathf.Abs(Mathf.Tan(1 - _unityCameraTransform.localRotation.x) * posY);
		var targetZoomPosition = new Vector3(0, posY, zPos);
		_unityCameraTransform.localPosition = targetZoomPosition;
		

		if (Vector3.Distance(_cameraCachedPosition, targetZoomPosition) > 0.001f) {
			_cameraCachedPosition = targetZoomPosition;
		}

		//var newAngle = _zoomByRotationCurve.Evaluate(1 - _zoomFactor / _zoomFMax) * 60 + 15;
		//_unityCameraTransform.localEulerAngles = new Vector3(newAngle, _yAngle, 0);
	}

	private struct StartScrollInfo {
		public Plane HitPlane;
		public Vector3 OriginalCameraPos;
		public Vector3 LocalStartPoint;
		public Quaternion StartRotation;
		public Vector3 OriginalNormal;
	}

	public struct ZoomingStateParams {
		public struct TouchParams {
			public Touch Touch { get; }

			public TouchParams(Touch touch) {
				Touch = touch;
			}
		}

		public TouchParams[] Touches;

		public TouchParams TouchA => Touches[0];

		public TouchParams TouchB => Touches[1];

		public float Delta { get; }

		internal float Factor;

		public ZoomingStateParams(float factor,
			Touch touchA,
			Touch touchB) {
			Factor = factor;

			Touches = new TouchParams[2];
			Touches[0] = new TouchParams(touchA);
			Touches[1] = new TouchParams(touchB);

			Delta = (touchA.position - touchB.position).magnitude;
		}
	}
}