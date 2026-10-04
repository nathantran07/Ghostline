using System;
using Ghostline.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Ghostline.Game
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class CarController : MonoBehaviour
    {
        [Header("Acceleration and resistance")]
        [FormerlySerializedAs("_speed")]
        [Tooltip("Reference speed for both curves. Actual terminal speed comes from force and drag balance.")]
        [SerializeField, Min(0.1f)] private float _topSpeed = 18f;
        [FormerlySerializedAs("_acceleration")]
        [SerializeField, Min(0f)] private float _maxAcceleration = 18f;
        [SerializeField, Min(0f)] private float _throttleRampTime = 0.4f;
        [Tooltip("X: speed / top speed. Y: acceleration multiplier; values above reference speed remain active.")]
        [SerializeField] private AnimationCurve _accelCurve = new AnimationCurve(
            new Keyframe(0f, 1f, -0.2f, -0.2f), new Keyframe(1f, 0.8f, -0.2f, -0.2f),
            new Keyframe(2f, 0.25f, -0.55f, -0.55f));
        [FormerlySerializedAs("_linearDamping")]
        [SerializeField, Min(0f)] private float _drag = 0.85f;
        [SerializeField, Min(0f)] private float _engineBraking = 1.5f;
        [SerializeField, Min(0f)] private float _brakeAcceleration = 6f;
        [SerializeField, Min(0f)] private float _reverseAcceleration = 8f;
        [SerializeField, Min(0f)] private float _reverseThreshold = 0.3f;
        [Header("Steering and grip")]
        [SerializeField, Min(1f)] private float _steering = 150f;
        [SerializeField] private AnimationCurve _steeringCurve = new AnimationCurve(
            new Keyframe(0f, 1f, -0.6f, -0.6f), new Keyframe(1f, 0.4f, -0.3f, -0.3f),
            new Keyframe(2f, 0.25f, -0.15f, -0.15f));
        [SerializeField, Min(0.1f)] private float _minimumSteeringSpeed = 2f;
        [SerializeField, Min(0f)] private float _grip = 12f;
        [SerializeField, Min(0f)] private float _angularDamping = 8f;
        [Header("Wall impacts")]
        [Tooltip("Fraction of incoming speed removed once per wall contact, including head-on impacts.")]
        [SerializeField, Range(0f, 1f)] private float _wallSpeedLoss = 0.35f;
        private Rigidbody2D _body;
        private float _throttle;
        private float _smoothedThrottle;
        private bool _brake;
        private float _turn;
        private DrivingCurve _accelerationProfile;
        private DrivingCurve _steeringProfile;
        private Vector2 _incomingVelocity;
        private float _lastWallImpactTime = float.NegativeInfinity;

        public Rigidbody2D Body => _body;
        public bool CanDrive { get; set; } = true;
        public bool InputEnabled { get; set; } = true;
        public float SmoothedThrottle => _smoothedThrottle;
        public float TopSpeedReference => _topSpeed;
        public event Action<float> WallImpacted;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.linearDamping = 0f;
            _body.angularDamping = _angularDamping;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CacheCurves();
        }

        private void OnValidate()
        {
            CacheCurves();
        }

        private void CacheCurves()
        {
            _accelerationProfile = DrivingCurveAdapter.ToCore(_accelCurve);
            _steeringProfile = DrivingCurveAdapter.ToCore(_steeringCurve);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            _throttle = 0f;
            _brake = false;
            _turn = 0f;
            if (!CanDrive || !InputEnabled || keyboard == null)
                return;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                _throttle += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                _brake = true;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                _turn += 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                _turn -= 1f;
        }

        private void FixedUpdate()
        {
            if (!CanDrive || !InputEnabled)
            {
                Stop();
                return;
            }
            Vector2 forward = transform.up;
            Vector2 right = transform.right;
            Vector2 velocity = _body.linearVelocity;
            float forwardSpeed = Vector2.Dot(velocity, forward);
            float lateralSpeed = Vector2.Dot(velocity, right);
            float step = Time.fixedDeltaTime;
            _smoothedThrottle = DrivingMath.SmoothThrottle(_smoothedThrottle, _brake ? 0f : _throttle,
                _throttleRampTime, step);
            float gripFactor = DrivingMath.GripFraction(_grip, step);
            velocity -= right * lateralSpeed * gripFactor;
            _body.linearVelocity = velocity;
            float drag = DrivingMath.DragRate(_drag, step);
            float speedAfterDrag = forwardSpeed * (1f - drag * step);
            float acceleration = _brake
                ? DrivingMath.BrakeOrReverse(speedAfterDrag, _brakeAcceleration, _reverseAcceleration, _reverseThreshold, step)
                : DrivingMath.Acceleration(_smoothedThrottle, velocity.magnitude, _topSpeed, _maxAcceleration, _accelerationProfile);
            if (!_brake && _throttle == 0f)
                acceleration += DrivingMath.EngineBraking(speedAfterDrag, _engineBraking * (1f - _smoothedThrottle), step);
            Vector2 accelerationVector = forward * acceleration - velocity * drag;
            _body.AddForce(accelerationVector * _body.mass, ForceMode2D.Force);
            _incomingVelocity = velocity + accelerationVector * step;
            float steeringRate = DrivingMath.SteeringRate(forwardSpeed, _topSpeed, _steering, _minimumSteeringSpeed, _steeringProfile);
            _body.MoveRotation(_body.rotation + _turn * steeringRate * step);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!CanDrive || !InputEnabled || !(collision.collider is EdgeCollider2D)
                || collision.collider.GetComponentInParent<TrackGenerator>() == null
                || _lastWallImpactTime == Time.fixedTime)
                return;
            _lastWallImpactTime = Time.fixedTime;
            float retained = DrivingMath.RetainedSpeed(_incomingVelocity.magnitude, _wallSpeedLoss);
            Vector2 direction = _body.linearVelocity.normalized;
            if (direction.sqrMagnitude < 0.5f && collision.contactCount > 0)
                direction = Vector2.Reflect(_incomingVelocity, collision.GetContact(0).normal).normalized;
            _body.linearVelocity = direction * retained;
            WallImpacted?.Invoke(_incomingVelocity.magnitude);
        }

        public void ResetPose(Vector2 position, float rotation)
        {
            Stop();
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f),
                Quaternion.Euler(0f, 0f, rotation));
            _throttle = 0f;
            _smoothedThrottle = 0f;
            _brake = false;
            _turn = 0f;
            CanDrive = true;
        }

        private void Stop()
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _smoothedThrottle = 0f;
            _incomingVelocity = Vector2.zero;
            _lastWallImpactTime = float.NegativeInfinity;
        }
    }
}
