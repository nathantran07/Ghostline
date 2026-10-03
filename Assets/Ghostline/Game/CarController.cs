using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghostline.Game
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class CarController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _speed = 12f;
        [SerializeField, Min(0.1f)] private float _acceleration = 18f;
        [SerializeField, Min(1f)] private float _steering = 150f;
        [SerializeField, Min(0f)] private float _grip = 12f;
        [SerializeField, Min(0f)] private float _linearDamping = 1.2f;
        [SerializeField, Min(0f)] private float _angularDamping = 8f;
        private Rigidbody2D _body;
        private float _throttle;
        private float _turn;

        public Rigidbody2D Body => _body;
        public bool CanDrive { get; set; } = true;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.linearDamping = _linearDamping;
            _body.angularDamping = _angularDamping;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            _throttle = 0f;
            _turn = 0f;
            if (!CanDrive || keyboard == null)
                return;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                _throttle += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                _throttle -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                _turn += 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                _turn -= 1f;
        }

        private void FixedUpdate()
        {
            if (!CanDrive)
            {
                Stop();
                return;
            }
            Vector2 forward = transform.up;
            Vector2 right = transform.right;
            Vector2 velocity = _body.linearVelocity;
            float forwardSpeed = Vector2.Dot(velocity, forward);
            float lateralSpeed = Vector2.Dot(velocity, right);
            float gripFactor = 1f - Mathf.Exp(-_grip * Time.fixedDeltaTime);
            velocity -= right * lateralSpeed * gripFactor;
            velocity += forward * (_throttle * _acceleration * Time.fixedDeltaTime);
            _body.linearVelocity = Vector2.ClampMagnitude(velocity, _speed);
            float steerFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2f);
            float reverseSign = forwardSpeed < 0f ? -1f : 1f;
            _body.MoveRotation(_body.rotation + _turn * _steering * steerFactor
                * reverseSign * Time.fixedDeltaTime);
        }

        public void ResetPose(Vector2 position, float rotation)
        {
            Stop();
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f),
                Quaternion.Euler(0f, 0f, rotation));
            _throttle = 0f;
            _turn = 0f;
            CanDrive = true;
        }

        private void Stop()
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }
    }
}
