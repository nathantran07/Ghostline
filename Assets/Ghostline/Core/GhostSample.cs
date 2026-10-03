using System;

namespace Ghostline.Core
{
    /// <summary>An immutable world-space car pose at a lap-relative time.</summary>
    [Serializable]
    public readonly struct GhostSample
    {
        /// <summary>Creates a finite pose with nonnegative seconds and rotation in degrees.</summary>
        public GhostSample(float time, float x, float y, float rotation)
        {
            NumericGuard.Nonnegative(time, nameof(time));
            NumericGuard.Finite(x, nameof(x));
            NumericGuard.Finite(y, nameof(y));
            NumericGuard.Finite(rotation, nameof(rotation));
            Time = time;
            X = x;
            Y = y;
            Rotation = rotation;
        }

        /// <summary>Gets seconds since the start crossing.</summary>
        public float Time { get; }
        /// <summary>Gets world-space horizontal position.</summary>
        public float X { get; }
        /// <summary>Gets world-space vertical position.</summary>
        public float Y { get; }
        /// <summary>Gets rotation around the Z axis in degrees.</summary>
        public float Rotation { get; }

        internal static GhostSample Interpolate(GhostSample from, GhostSample to, float time)
        {
            float fraction = (time - from.Time) / (to.Time - from.Time);
            float delta = (to.Rotation - from.Rotation) % 360f;
            if (delta > 180f)
                delta -= 360f;
            else if (delta < -180f)
                delta += 360f;
            return new GhostSample(time,
                from.X + (to.X - from.X) * fraction,
                from.Y + (to.Y - from.Y) * fraction,
                from.Rotation + delta * fraction);
        }
    }
}
