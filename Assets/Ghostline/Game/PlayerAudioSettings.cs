using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghostline.Game
{
    [DisallowMultipleComponent]
    public sealed class PlayerAudioSettings : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _engineVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;
        private bool _muted;

        public float MasterVolume => SafeVolume(_masterVolume);
        public float EngineVolume => SafeVolume(_engineVolume);
        public float SfxVolume => SafeVolume(_sfxVolume);
        public bool IsMuted => _muted;

        public void ToggleMute()
        {
            _muted = !_muted;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                ToggleMute();
        }

        private static float SafeVolume(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }
    }
}
