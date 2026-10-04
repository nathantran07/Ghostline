using System;
using System.Threading;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource), typeof(PlayerAudioSettings))]
    public sealed class EngineAudio : MonoBehaviour
    {
        [SerializeField] private RaceManager _race;
        [Header("V12 and single-clutch gearbox")]
        [SerializeField, Min(1f)] private float _idleRpm = 1000f;
        [SerializeField, Min(1f)] private float _redlineRpm = 8500f;
        [SerializeField, Range(1, 16)] private int _gearCount = 7;
        [Tooltip("Upper speed fraction per gear; each range starts at the previous end. Last end must be 1.")]
        [SerializeField] private float[] _gearSpeedEnds = { 0.16f, 0.28f, 0.41f, 0.55f, 0.70f, 0.85f, 1f };
        [SerializeField, Min(0f)] private float _shiftGap = 0.08f;
        [SerializeField, Min(0f)] private float _rpmRiseTime = 0.12f;
        [SerializeField, Min(0f)] private float _rpmFallTime = 0.35f;
        [SerializeField, Range(0f, 0.1f)] private float _downshiftHysteresis = 0.015f;
        [SerializeField] private bool _revLimiter;
        [SerializeField, Min(0f)] private float _limiterDepth = 180f;
        [SerializeField, Min(0.1f)] private float _limiterFrequency = 18f;
        [Header("Engine timbre")]
        [Tooltip("Weights for orders 1 through 12 of rpm / 60 * 6 Hz.")]
        [SerializeField] private float[] _harmonicWeights = EngineVoiceSettings.CreateDefaultHarmonics();
        [Tooltip("Crank orders are relative to rpm / 60 Hz, below the V12 firing fundamental.")]
        [SerializeField, Range(0f, 1f)] private float _crankWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _halfOrderWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _secondCrankWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _thirdCrankWeight = 0.35f;
        [Tooltip("Scales every oscillator frequency; the RPM model and filter tracking are unchanged.")]
        [SerializeField, Min(0.01f)] private float _pitchScale = 1f;
        [Tooltip("Harmonic low-pass cutoff at idle and redline, in Hz. Applied before intake/exhaust noise.")]
        [SerializeField, Min(1f)] private float _lowPassMin = 1500f;
        [SerializeField, Min(1f)] private float _lowPassMax = 5000f;
        [SerializeField, Range(0f, 0.02f)] private float _bankDetune = 0.003f;
        [SerializeField, Range(0f, 1f)] private float _intakeExhaustNoise = 0.06f;
        [SerializeField, Min(0.001f)] private float _audioRampTime = 0.02f;
        [Header("Optional tire noise")]
        [SerializeField] private bool _tireSqueal;
        [SerializeField, Range(0f, 1f)] private float _tireVolume = 0.2f;
        [SerializeField, Min(0.1f)] private float _tireReferenceSpeed = 4f;
        private CarController _car;
        private PlayerAudioSettings _settings;
        private AudioSource _source;
        private AudioClip _carrier;
        private EngineSoundModel _model;
        private EngineSynthesizer _synthesizer;
        private Targets _targets;
        private bool _warnedNotPlaying;
        private int _renderedBuffers;
        public int RenderedBufferCount => Volatile.Read(ref _renderedBuffers);

        /// <summary>Installer migration: preserves every custom harmonic preset and other tuning field.</summary>
        public void UpgradeFactoryHarmonics()
        {
            if (EngineVoiceSettings.IsLegacyFactoryHarmonics(_harmonicWeights))
                _harmonicWeights = EngineVoiceSettings.CreateDefaultHarmonics();
        }

        // This immutable managed object contains no Unity references. The renderer is audio-thread-owned.
        private sealed class Targets
        {
            internal readonly EngineSynthesizer Synthesizer;
            internal readonly float Frequency;
            internal readonly float Loudness;
            internal readonly float Throttle;
            internal readonly float Volume;
            internal readonly float Tire;

            internal Targets(EngineSynthesizer synthesizer, EngineSoundFrame frame, float volume, float tire)
            {
                Synthesizer = synthesizer;
                Frequency = AudioMath.FiringFrequency(frame.Rpm);
                Loudness = frame.Loudness;
                Throttle = frame.EffectiveThrottle;
                Volume = volume;
                Tire = tire;
            }
        }

        public void Configure(RaceManager race)
        {
            CarController car = GetComponent<CarController>();
            if (race == null || car == null || race.PlayerCar != car || GetComponentInParent<GhostCarView>() != null)
                throw new ArgumentException("Engine audio belongs only to the configured player car.", nameof(race));
            Unsubscribe();
            _race = race;
            if (Application.isPlaying && isActiveAndEnabled)
                _race.Restarted += ResetEngine;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            _car = GetComponent<CarController>();
            _settings = GetComponent<PlayerAudioSettings>();
            _source = GetComponent<AudioSource>();
            if (_car == null || GetComponentInParent<GhostCarView>() != null
                || (_race != null && _race.PlayerCar != _car))
            {
                Debug.LogError("Ghostline EngineAudio must be on the player car, never the ghost.", this);
                enabled = false;
                return;
            }
            try
            {
                _model = new EngineSoundModel(new EngineSoundSettings(_idleRpm, _redlineRpm, _gearCount,
                    _gearSpeedEnds, _shiftGap, _rpmRiseTime, _rpmFallTime, _downshiftHysteresis,
                    _revLimiter, _limiterDepth, _limiterFrequency));
                AudioConfigurationChanged(false);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"Ghostline engine audio configuration is invalid: {exception.Message}", this);
                enabled = false;
                return;
            }
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            if (_race != null)
                _race.Restarted += ResetEngine;
        }

        private void OnDisable()
        {
            Unsubscribe();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            Volatile.Write(ref _targets, null);
            if (_source != null)
            {
                _source.Stop();
                if (_source.clip == _carrier)
                    _source.clip = null;
            }
            ReleaseCarrier();
        }

        private void Unsubscribe()
        {
            if (_race != null)
                _race.Restarted -= ResetEngine;
        }

        private void Update()
        {
            if (_model == null || _car == null || _car.Body == null)
                return;
            float throttle = _car.CanDrive && _car.InputEnabled ? _car.SmoothedThrottle : 0f;
            EngineSoundFrame frame = _model.Tick(_car.Body.linearVelocity.magnitude, throttle,
                _car.TopSpeedReference, Time.deltaTime);
            float tire = _tireSqueal ? Mathf.Clamp01(Mathf.Abs(Vector2.Dot(_car.Body.linearVelocity,
                transform.right)) / Mathf.Max(0.1f, _tireReferenceSpeed)) * Mathf.Clamp01(_tireVolume) : 0f;
            Volatile.Write(ref _targets, new Targets(_synthesizer, frame, _settings.EngineVolume, tire));
            if (!_warnedNotPlaying && _source != null && !_source.isPlaying && !AudioListener.pause)
            {
                _warnedNotPlaying = true;
                Debug.LogWarning("Ghostline engine AudioSource is not playing; synthesized engine audio cannot run. Check that the source is enabled and the audio device is available.", this);
            }
        }

        private void ResetEngine()
        {
            _model?.Reset();
            if (_model != null && _synthesizer != null)
                Volatile.Write(ref _targets, new Targets(_synthesizer, _model.Current, _settings.EngineVolume, 0f));
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            int rate = AudioSettings.outputSampleRate;
            if (rate < 8000 || rate > 384000)
                rate = 48000;
            var voice = new EngineVoiceSettings(_crankWeight, _halfOrderWeight, _secondCrankWeight,
                _thirdCrankWeight, _pitchScale, _lowPassMin, _lowPassMax, _idleRpm, _redlineRpm);
            _synthesizer = new EngineSynthesizer(rate, _harmonicWeights, _bankDetune,
                _intakeExhaustNoise, _audioRampTime, voice);
            ResetEngine();
            _source.Stop();
            ReleaseCarrier();
            _carrier = AudioClip.Create("Ghostline engine carrier (generated silence)", rate / 10, 1, rate, false);
            _carrier.hideFlags = HideFlags.DontSave;
            _carrier.SetData(new float[rate / 10], 0);
            _source.clip = _carrier;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.volume = 1f;
            _source.Play();
        }

        private void ReleaseCarrier()
        {
            if (_carrier == null)
                return;
            if (Application.isPlaying)
                Destroy(_carrier);
            else
                DestroyImmediate(_carrier);
            _carrier = null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Targets targets = Volatile.Read(ref _targets);
            if (targets == null)
                Array.Clear(data, 0, data.Length);
            else
                targets.Synthesizer.Render(data, channels, targets.Frequency, targets.Loudness,
                    targets.Throttle, targets.Volume, targets.Tire);
            Interlocked.Increment(ref _renderedBuffers);
        }
    }
}
