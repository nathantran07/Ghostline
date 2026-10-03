using UnityEngine;

namespace Ghostline.Game
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField, Min(0f)] private float _smoothing = 5f;

        public void Configure(Transform target)
        {
            _target = target;
        }

        public void SnapToTarget()
        {
            if (_target != null)
                transform.position = new Vector3(_target.position.x, _target.position.y, -10f);
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;
            Vector3 desired = new Vector3(_target.position.x, _target.position.y, -10f);
            transform.position = Vector3.Lerp(transform.position, desired,
                1f - Mathf.Exp(-_smoothing * Time.deltaTime));
        }
    }
}
