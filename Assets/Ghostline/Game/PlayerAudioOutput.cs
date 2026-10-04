using System;
using System.Threading;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    /// <summary>Runs after engine and SFX mixing on the scene's camera listener.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioListener))]
    public sealed class PlayerAudioOutput : MonoBehaviour
    {
        [SerializeField] private PlayerAudioSettings _settings;
        private AudioOutputLimiter _limiter;
        private float _master;
        private int _processedBuffers;
        public int ProcessedBufferCount => Volatile.Read(ref _processedBuffers);

        public void Configure(PlayerAudioSettings settings)
        {
            if (settings == null || settings.GetComponent<CarController>() == null
                || settings.GetComponentInParent<GhostCarView>() != null)
                throw new ArgumentException("The audio output needs the player's volume settings.", nameof(settings));
            _settings = settings;
            CacheMaster();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            AudioConfigurationChanged(false);
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            Volatile.Write(ref _master, 0f);
            Volatile.Write(ref _limiter, null);
        }

        private void LateUpdate()
        {
            CacheMaster();
        }

        private void CacheMaster()
        {
            float master = _settings != null && _settings.isActiveAndEnabled && !_settings.IsMuted
                ? _settings.MasterVolume : 0f;
            Volatile.Write(ref _master, master);
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            int rate = AudioSettings.outputSampleRate;
            Volatile.Write(ref _limiter, new AudioOutputLimiter(rate >= 8000 && rate <= 384000 ? rate : 48000));
            CacheMaster();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            AudioOutputLimiter limiter = Volatile.Read(ref _limiter);
            if (limiter == null)
                Array.Clear(data, 0, data.Length);
            else
                limiter.Process(data, channels, Volatile.Read(ref _master));
            Interlocked.Increment(ref _processedBuffers);
        }
    }
}
