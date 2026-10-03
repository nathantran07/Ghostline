using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>Resamples supplied poses onto a fixed time grid, including exact lap endpoints.</summary>
    public sealed class GhostRecorder
    {
        private readonly float _interval;
        private readonly List<GhostSample> _samples = new List<GhostSample>();
        private GhostSample _previous;
        private int _nextSampleIndex;
        private bool _recording;

        /// <summary>Creates a recorder with a finite positive interval, defaulting to 0.05 seconds.</summary>
        public GhostRecorder(float interval = 0.05f)
        {
            NumericGuard.Finite(interval, nameof(interval));
            if (interval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(interval));
            _interval = interval;
        }

        /// <summary>Clears prior data and records the start pose at time zero.</summary>
        public void Begin(float x, float y, float rotation)
        {
            _previous = new GhostSample(0f, x, y, rotation);
            _samples.Clear();
            _samples.Add(_previous);
            _nextSampleIndex = 1;
            _recording = true;
        }

        /// <summary>Records every grid time up to a finite, nondecreasing lap time.</summary>
        public void Record(float time, float x, float y, float rotation)
        {
            if (!_recording)
                throw new InvalidOperationException("Begin a recording before supplying poses.");
            var current = new GhostSample(time, x, y, rotation);
            if (time < _previous.Time)
                throw new ArgumentOutOfRangeException(nameof(time), "Time cannot move backward.");
            float sampleTime = (float)((double)_nextSampleIndex * _interval);
            while (sampleTime <= time)
            {
                _samples.Add(GhostSample.Interpolate(_previous, current, sampleTime));
                _nextSampleIndex++;
                sampleTime = (float)((double)_nextSampleIndex * _interval);
            }
            _previous = current;
        }

        /// <summary>Includes the exact finish pose, stops recording, and returns an immutable copy.</summary>
        public GhostRecording Complete(float time, float x, float y, float rotation)
        {
            Record(time, x, y, rotation);
            var finish = new GhostSample(time, x, y, rotation);
            if (_samples[_samples.Count - 1].Time == time)
                _samples[_samples.Count - 1] = finish;
            else
                _samples.Add(finish);
            _recording = false;
            return new GhostRecording(_samples);
        }
    }
}
