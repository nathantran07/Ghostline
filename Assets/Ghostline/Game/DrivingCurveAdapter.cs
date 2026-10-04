using System;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    /// <summary>Copies Inspector keys into the engine-free evaluator; never evaluates in Unity.</summary>
    public static class DrivingCurveAdapter
    {
        public static DrivingCurve ToCore(AnimationCurve curve)
        {
            if (curve == null)
                return new DrivingCurve(Array.Empty<CurveKey>());
            Keyframe[] keys = curve.keys;
            var points = new CurveKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                points[i] = new CurveKey(key.time, key.value, key.inTangent, key.outTangent,
                    key.inWeight, key.outWeight, (key.weightedMode & WeightedMode.In) != 0,
                    (key.weightedMode & WeightedMode.Out) != 0);
            }
            return new DrivingCurve(points, ConvertWrap(curve.preWrapMode), ConvertWrap(curve.postWrapMode));
        }

        private static CurveWrap ConvertWrap(WrapMode mode)
        {
            return mode == WrapMode.Loop ? CurveWrap.Loop : mode == WrapMode.PingPong ? CurveWrap.PingPong : CurveWrap.Clamp;
        }
    }
}
