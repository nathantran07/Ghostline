using System;

namespace Ghostline.Core
{
    /// <summary>Audio-thread-owned pulse voice with live, allocation-free additive A/B crossfade.</summary>
    public sealed class PulseEngineSynthesizer
    {
        private const int Oversampling = 4;
        private const int TapCount = 129;
        private readonly int _sampleRate;
        private readonly int _internalRate;
        private readonly PulseEngineSettings _settings;
        private readonly EngineVoiceSettings _voice;
        private readonly EngineSynthesizer _additive;
        private readonly PulseTrain _train;
        private readonly ResonantBandpass[] _filters = new ResonantBandpass[3];
        private readonly double[] _fast = new double[2];
        private readonly double[] _slow = new double[2];
        private readonly double[] _taps = new double[TapCount];
        private readonly double[] _history = new double[TapCount];
        private readonly double _fastTime;
        private readonly double _slowTime;
        private readonly double _fastDecay;
        private readonly double _slowDecay;
        private readonly double _pulseNormalization;
        private readonly double _resonanceNormalization;
        private readonly double _ramp;
        private readonly double _internalRamp;
        private readonly double _noiseAmount;
        private readonly double _noiseLowStep;
        private readonly double _noiseHighStep;
        private PulseRandom _noiseRandom;
        private double _frequency = 100d;
        private double _lowPassStep;
        private double _lowPass;
        private double _gain;
        private double _throttle;
        private double _tire;
        private double _blend;
        private double _noiseLow;
        private double _noiseHigh;
        private int _historyIndex;

        public PulseEngineSynthesizer(int sampleRate, PulseEngineSettings settings = null,
            EngineVoiceSettings voice = null, EngineSynthesizer additive = null,
            float noiseAmount = 0.06f, float rampTime = 0.02f)
        {
            AudioMath.SampleRate(sampleRate);
            AudioMath.Unit(noiseAmount, nameof(noiseAmount));
            NumericGuard.Nonnegative(rampTime, nameof(rampTime));
            if (rampTime == 0f)
                throw new ArgumentOutOfRangeException(nameof(rampTime));
            _sampleRate = sampleRate;
            _internalRate = sampleRate * Oversampling;
            _settings = settings ?? new PulseEngineSettings();
            _voice = voice ?? new EngineVoiceSettings();
            _additive = additive ?? new EngineSynthesizer(sampleRate, voice: _voice,
                noiseAmount: noiseAmount, rampTime: rampTime);
            _train = new PulseTrain(_internalRate, _settings);
            _noiseRandom = new PulseRandom(_settings.Seed ^ 0xA511E9B3);
            _slowTime = _settings.PulseWidth / 5d;
            _fastTime = _slowTime * 0.15d;
            _slowDecay = Math.Exp(-1d / (_internalRate * _slowTime));
            _fastDecay = Math.Exp(-1d / (_internalRate * _fastTime));
            double peakTime = Math.Log(_slowTime / _fastTime) / (1d / _fastTime - 1d / _slowTime);
            _pulseNormalization = 1d / (Math.Exp(-peakTime / _slowTime) - Math.Exp(-peakTime / _fastTime));
            double totalGain = 0d;
            for (int i = 0; i < _filters.Length; i++)
            {
                PulseResonance resonance = _settings.ResonanceAt(i);
                _filters[i] = new ResonantBandpass(_internalRate,
                    (float)Math.Min(resonance.Frequency, sampleRate * 0.45d), resonance.Q);
                totalGain += resonance.Gain;
            }
            _resonanceNormalization = 0.75d / Math.Max(1d, totalGain);
            _ramp = 1d - Math.Exp(-1d / (sampleRate * (double)rampTime));
            _internalRamp = 1d - Math.Exp(-1d / (_internalRate * (double)rampTime));
            _noiseAmount = noiseAmount;
            _noiseLowStep = 1d - Math.Exp(-2d * Math.PI * 3200d / sampleRate);
            _noiseHighStep = 1d - Math.Exp(-2d * Math.PI * 250d / sampleRate);
            _lowPassStep = LowPassStep(_voice.LowPassMin);
            CreateDecimationFilter();
        }

        public void Render(float[] buffer, int channels, float fundamental, float loudness,
            float throttle, float engineVolume, float tireAmount = 0f, bool useAdditive = false)
        {
            // The unchanged additive renderer validates all inputs and fills the caller's buffer.
            // Reusing it as the A/B input avoids allocating any scratch buffers on the audio thread.
            _additive.Render(buffer, channels, fundamental, loudness, throttle, engineVolume, tireAmount);
            double targetFrequency = (double)fundamental * _voice.PitchScale;
            float rpm = (float)Math.Min((double)fundamental * 10d, _voice.RedlineRpm);
            double targetLowPassStep = LowPassStep(_voice.CutoffAtRpm(rpm));
            for (int i = 0; i < buffer.Length; i += channels)
            {
                for (int sub = 0; sub < Oversampling; sub++)
                {
                    _frequency += (targetFrequency - _frequency) * _internalRamp;
                    // Unsupported rates are silent, rather than changing pitch or scheduling unbounded firings.
                    CylinderPulse pulse = _train.Advance(_frequency < _sampleRate * 0.4d ? _frequency : 0d);
                    double excitation = 0d;
                    for (int bank = 0; bank < 2; bank++)
                    {
                        _fast[bank] *= _fastDecay;
                        _slow[bank] *= _slowDecay;
                        if (pulse.Fired && pulse.Bank == bank)
                        {
                            _fast[bank] += pulse.Amplitude * Math.Exp(-pulse.AgeSeconds / _fastTime);
                            _slow[bank] += pulse.Amplitude * Math.Exp(-pulse.AgeSeconds / _slowTime);
                        }
                        excitation += (_slow[bank] - _fast[bank]) * _pulseNormalization;
                    }
                    excitation /= Math.Max(1d, _frequency * _settings.PulseWidth);
                    double resonated = 0d;
                    for (int filter = 0; filter < _filters.Length; filter++)
                        resonated += _filters[filter].Process(excitation) * _settings.ResonanceAt(filter).Gain;
                    _lowPassStep += (targetLowPassStep - _lowPassStep) * _internalRamp;
                    _lowPass += (resonated * _resonanceNormalization - _lowPass) * _lowPassStep;
                    _history[_historyIndex] = _lowPass;
                    _historyIndex = (_historyIndex + 1) % TapCount;
                }
                double signal = 0d;
                int history = (_historyIndex + TapCount - 1) % TapCount;
                for (int tap = 0; tap < TapCount; tap++)
                {
                    signal += _history[history] * _taps[tap];
                    history = (history + TapCount - 1) % TapCount;
                }
                _gain += (engineVolume * loudness - _gain) * _ramp;
                _throttle += (throttle - _throttle) * _ramp;
                _tire += (tireAmount * engineVolume - _tire) * _ramp;
                _blend += ((useAdditive ? 1d : 0d) - _blend) * _ramp;
                double noise = _noiseRandom.Next();
                _noiseLow += (noise - _noiseLow) * _noiseLowStep;
                _noiseHigh += (noise - _noiseHigh) * _noiseHighStep;
                double filteredNoise = _noiseLow - _noiseHigh;
                float pulseSample = AudioMath.SoftClip((float)((signal + filteredNoise * _noiseAmount * _throttle)
                    * _gain + filteredNoise * _tire * 0.22d));
                float sample = (float)(pulseSample * (1d - _blend) + buffer[i] * _blend);
                for (int c = 0; c < channels; c++)
                    buffer[i + c] = sample;
            }
        }

        private double LowPassStep(double cutoff)
        {
            return 1d - Math.Exp(-2d * Math.PI * Math.Min(cutoff, _sampleRate * 0.45d) / _internalRate);
        }

        private void CreateDecimationFilter()
        {
            // Leave a transition band before output Nyquist, including at the lowest supported rate.
            const double cutoff = 0.4d / Oversampling;
            double total = 0d;
            for (int i = 0; i < TapCount; i++)
            {
                int offset = i - (TapCount - 1) / 2;
                double sinc = offset == 0 ? 2d * cutoff : Math.Sin(2d * Math.PI * cutoff * offset) / (Math.PI * offset);
                double window = 0.42d - 0.5d * Math.Cos(2d * Math.PI * i / (TapCount - 1))
                    + 0.08d * Math.Cos(4d * Math.PI * i / (TapCount - 1));
                _taps[i] = sinc * window;
                total += _taps[i];
            }
            for (int i = 0; i < TapCount; i++)
                _taps[i] /= total;
        }
    }
}
