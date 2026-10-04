using System;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerAudioSettings))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private RaceManager _race;
        [SerializeField] private AudioSource _source;
        private CarController _car;
        private PlayerAudioSettings _settings;
        private AudioClip[] _clips;

        public AudioSource Source => _source;

        public void Configure(RaceManager race, AudioSource source)
        {
            CarController car = GetComponent<CarController>();
            if (race == null || car == null || race.PlayerCar != car || GetComponentInParent<GhostCarView>() != null)
                throw new ArgumentException("SFX belongs only to the configured player car.", nameof(race));
            if (source == null || source.transform == transform || !source.transform.IsChildOf(transform))
                throw new ArgumentException("SFX needs a separate source on a player child.", nameof(source));
            Unsubscribe();
            _race = race;
            _source = source;
            _car = car;
            _settings = GetComponent<PlayerAudioSettings>();
            if (Application.isPlaying && isActiveAndEnabled)
                StartAudio();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            _car = GetComponent<CarController>();
            _settings = GetComponent<PlayerAudioSettings>();
            if (_race != null && _source != null && _car != null)
                StartAudio();
        }

        private void StartAudio()
        {
            if (_race.PlayerCar != _car || GetComponentInParent<GhostCarView>() != null
                || _source.transform == transform || !_source.transform.IsChildOf(transform))
                throw new InvalidOperationException("Ghostline SFX must use the player and a separate child source.");
            Unsubscribe();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            AudioConfigurationChanged(false);
            _source.volume = _settings.SfxVolume;
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _car.WallImpacted += WallImpacted;
            _race.CountdownCue += CountdownCue;
            _race.LapCompleted += LapCompleted;
            _race.Restarted += Restarted;
        }

        private void LateUpdate()
        {
            if (_source != null && _settings != null)
                _source.volume = _settings.SfxVolume;
        }

        private void WallImpacted(float incomingSpeed)
        {
            Play(ProceduralSfxKind.WallHit, AudioMath.ImpactVolume(incomingSpeed, _car.TopSpeedReference));
        }

        private void CountdownCue(int number)
        {
            Play(number == 0 ? ProceduralSfxKind.Start : ProceduralSfxKind.Countdown, 1f);
        }

        private void LapCompleted(bool newBest)
        {
            Play(newBest ? ProceduralSfxKind.NewBestLap : ProceduralSfxKind.LapComplete, 1f);
        }

        private void Restarted()
        {
            _source?.Stop();
        }

        private void Play(ProceduralSfxKind kind, float volume)
        {
            if (!isActiveAndEnabled || _source == null || !_source.isActiveAndEnabled || _clips == null
                || _settings == null || !_settings.isActiveAndEnabled || _settings.IsMuted
                || _settings.MasterVolume == 0f || _settings.SfxVolume == 0f || volume == 0f)
                return;
            _source.PlayOneShot(_clips[(int)kind], volume);
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            _source?.Stop();
            ReleaseClips();
            int rate = AudioSettings.outputSampleRate;
            if (rate < 8000 || rate > 384000)
                rate = 48000;
            _clips = new AudioClip[5];
            for (int i = 0; i < _clips.Length; i++)
            {
                var kind = (ProceduralSfxKind)i;
                float[] samples = ProceduralSfx.Generate(kind, rate);
                AudioClip clip = AudioClip.Create("Ghostline " + kind + " (generated)", samples.Length, 1, rate, false);
                clip.hideFlags = HideFlags.DontSave;
                clip.SetData(samples, 0);
                _clips[i] = clip;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            _source?.Stop();
            ReleaseClips();
        }

        private void Unsubscribe()
        {
            if (_car != null)
                _car.WallImpacted -= WallImpacted;
            if (_race == null)
                return;
            _race.CountdownCue -= CountdownCue;
            _race.LapCompleted -= LapCompleted;
            _race.Restarted -= Restarted;
        }

        private void ReleaseClips()
        {
            if (_clips == null)
                return;
            foreach (AudioClip clip in _clips)
            {
                if (clip == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(clip);
                else
                    DestroyImmediate(clip);
            }
            _clips = null;
        }
    }
}
