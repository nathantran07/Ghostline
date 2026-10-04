using System;

namespace Ghostline.Core
{
    public enum CurveWrap { Clamp, Loop, PingPong }

    public readonly struct CurveKey
    {
        public CurveKey(float time, float value, float inTangent = 0f, float outTangent = 0f,
            float inWeight = 1f / 3f, float outWeight = 1f / 3f, bool weightedIn = false, bool weightedOut = false)
        {
            NumericGuard.Finite(time, nameof(time));
            NumericGuard.Finite(value, nameof(value));
            if (float.IsNaN(inTangent) || float.IsNaN(outTangent))
                throw new ArgumentOutOfRangeException(nameof(inTangent));
            NumericGuard.Nonnegative(inWeight, nameof(inWeight));
            NumericGuard.Nonnegative(outWeight, nameof(outWeight));
            if (inWeight > 1f || outWeight > 1f)
                throw new ArgumentOutOfRangeException(nameof(inWeight));
            Time = time;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
            InWeight = inWeight;
            OutWeight = outWeight;
            WeightedIn = weightedIn;
            WeightedOut = weightedOut;
        }
        public float Time { get; }
        public float Value { get; }
        public float InTangent { get; }
        public float OutTangent { get; }
        public float InWeight { get; }
        public float OutWeight { get; }
        public bool WeightedIn { get; }
        public bool WeightedOut { get; }
    }

    /// <summary>Engine-independent Hermite/weighted Bezier evaluation of Inspector curve keys.</summary>
    public sealed class DrivingCurve
    {
        private readonly CurveKey[] _keys;
        private readonly CurveWrap _preWrap;
        private readonly CurveWrap _postWrap;

        public DrivingCurve(CurveKey[] keys, CurveWrap preWrap = CurveWrap.Clamp, CurveWrap postWrap = CurveWrap.Clamp)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));
            if (preWrap < CurveWrap.Clamp || preWrap > CurveWrap.PingPong
                || postWrap < CurveWrap.Clamp || postWrap > CurveWrap.PingPong)
                throw new ArgumentOutOfRangeException(nameof(preWrap));
            _keys = (CurveKey[])keys.Clone();
            for (int i = 1; i < _keys.Length; i++)
                if (_keys[i].Time <= _keys[i - 1].Time)
                    throw new ArgumentException("Curve keys must have strictly increasing times.", nameof(keys));
            _preWrap = preWrap;
            _postWrap = postWrap;
        }

        public float Evaluate(float time)
        {
            NumericGuard.Finite(time, nameof(time));
            if (_keys.Length == 0)
                return 0f;
            if (_keys.Length == 1)
                return _keys[0].Value;
            CurveKey first = _keys[0];
            CurveKey last = _keys[_keys.Length - 1];
            if (time < first.Time)
                time = Wrap(time, first.Time, last.Time, _preWrap);
            else if (time >= last.Time)
                time = Wrap(time, first.Time, last.Time, _postWrap);
            if (time <= first.Time)
                return first.Value;
            if (time >= last.Time)
                return last.Value;
            int lower = 0;
            int upper = _keys.Length - 1;
            while (upper - lower > 1)
            {
                int middle = (lower + upper) / 2;
                if (_keys[middle].Time <= time)
                    lower = middle;
                else
                    upper = middle;
            }
            CurveKey a = _keys[lower];
            CurveKey b = _keys[upper];
            if (float.IsInfinity(a.OutTangent) || float.IsInfinity(b.InTangent))
                return a.Value;
            double duration = (double)b.Time - a.Time;
            double t = (time - a.Time) / duration;
            if (!a.WeightedOut && !b.WeightedIn)
            {
                double t2 = t * t;
                double t3 = t2 * t;
                return (float)((2d * t3 - 3d * t2 + 1d) * a.Value + (t3 - 2d * t2 + t) * duration * a.OutTangent
                    + (-2d * t3 + 3d * t2) * b.Value + (t3 - t2) * duration * b.InTangent);
            }
            double outWeight = a.WeightedOut ? a.OutWeight : 1d / 3d;
            double inWeight = b.WeightedIn ? b.InWeight : 1d / 3d;
            // Weighted time controls need inversion before evaluating the value controls.
            double lo = 0d;
            double hi = 1d;
            for (int iteration = 0; iteration < 32; iteration++)
            {
                double middle = (lo + hi) * 0.5d;
                if (Bezier(0d, outWeight, 1d - inWeight, 1d, middle) < t)
                    lo = middle;
                else
                    hi = middle;
            }
            return (float)Bezier(a.Value, a.Value + a.OutTangent * duration * outWeight,
                b.Value - b.InTangent * duration * inWeight, b.Value, (lo + hi) * 0.5d);
        }

        private static float Wrap(float time, float first, float last, CurveWrap mode)
        {
            if (mode == CurveWrap.Clamp)
                return Math.Min(last, Math.Max(first, time));
            double length = (double)last - first;
            double period = mode == CurveWrap.PingPong ? length * 2d : length;
            double phase = ((double)time - first) % period;
            if (phase < 0d)
                phase += period;
            if (mode == CurveWrap.PingPong && phase > length)
                phase = period - phase;
            return (float)(first + phase);
        }

        private static double Bezier(double a, double b, double c, double d, double t)
        {
            double u = 1d - t;
            return u * u * u * a + 3d * u * u * t * b + 3d * u * t * t * c + t * t * t * d;
        }
    }
}
