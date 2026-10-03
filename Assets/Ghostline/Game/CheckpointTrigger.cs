using UnityEngine;

namespace Ghostline.Game
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CheckpointTrigger : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private bool _isStartFinish;
        [SerializeField, Range(0, 3)] private int _checkpointIndex;
        [SerializeField] private Vector2 _forwardDirection = Vector2.right;

        public void Configure(RaceManager raceManager, bool isStartFinish,
            int checkpointIndex, Vector2 forwardDirection)
        {
            _raceManager = raceManager;
            _isStartFinish = isStartFinish;
            _checkpointIndex = checkpointIndex;
            _forwardDirection = forwardDirection.normalized;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_raceManager == null || other.attachedRigidbody == null)
                return;
            CarController car = other.attachedRigidbody.GetComponent<CarController>();
            if (car == null || Vector2.Dot(car.Body.linearVelocity, _forwardDirection) <= 0f)
                return;
            _raceManager.CrossTrigger(car, _isStartFinish, _checkpointIndex);
        }
    }
}
