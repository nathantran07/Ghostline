using System;

namespace Ghostline.Core
{
    /// <summary>Pure acceleration, input smoothing, braking, steering and impact calculations.</summary>
    public static class DrivingMath
    {
        public static float SmoothThrottle(float current, float target, float rampTime, float deltaTime)
        {
            Unit(current, nameof(current));
            Unit(target, nameof(target));
            NumericGuard.Nonnegative(rampTime, nameof(rampTime));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            if (rampTime == 0f)
                return target;
            float step = deltaTime / rampTime;
            return current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
        }

        public static float Acceleration(float throttle, float speed, float topSpeed, float maximum, DrivingCurve curve)
        {
            Unit(throttle, nameof(throttle));
            NumericGuard.Nonnegative(speed, nameof(speed));
            Positive(topSpeed, nameof(topSpeed));
            NumericGuard.Nonnegative(maximum, nameof(maximum));
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));
            return throttle * curve.Evaluate(speed / topSpeed) * maximum;
        }

        public static float BrakeOrReverse(float speed, float braking, float reverse, float threshold, float deltaTime)
        {
            NumericGuard.Finite(speed, nameof(speed));
            NumericGuard.Nonnegative(braking, nameof(braking));
            NumericGuard.Nonnegative(reverse, nameof(reverse));
            NumericGuard.Nonnegative(threshold, nameof(threshold));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            if (deltaTime == 0f)
                return 0f;
            return speed > threshold ? -Math.Min(braking, speed / deltaTime) : -reverse;
        }

        public static float EngineBraking(float speed, float braking, float deltaTime)
        {
            NumericGuard.Finite(speed, nameof(speed));
            NumericGuard.Nonnegative(braking, nameof(braking));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            return deltaTime == 0f ? 0f : -Math.Sign(speed) * Math.Min(braking, Math.Abs(speed) / deltaTime);
        }

        public static float SteeringRate(float speed, float topSpeed, float maximum, float minimumSpeed, DrivingCurve curve)
        {
            NumericGuard.Finite(speed, nameof(speed));
            Positive(topSpeed, nameof(topSpeed));
            Positive(minimumSpeed, nameof(minimumSpeed));
            NumericGuard.Nonnegative(maximum, nameof(maximum));
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));
            float magnitude = Math.Abs(speed);
            return Math.Sign(speed) * maximum * Math.Min(1f, magnitude / minimumSpeed)
                * Math.Max(0f, curve.Evaluate(magnitude / topSpeed));
        }

        public static float RetainedSpeed(float speed, float loss)
        {
            NumericGuard.Nonnegative(speed, nameof(speed));
            Unit(loss, nameof(loss));
            return speed * (1f - loss);
        }

        public static float GripFraction(float grip, float deltaTime)
        {
            NumericGuard.Nonnegative(grip, nameof(grip));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            return 1f - (float)Math.Exp(-grip * deltaTime);
        }

        public static float DragRate(float drag, float deltaTime)
        {
            NumericGuard.Nonnegative(drag, nameof(drag));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            return deltaTime == 0f ? 0f : Math.Min(drag, 1f / deltaTime);
        }

        private static void Unit(float value, string name)
        {
            NumericGuard.Nonnegative(value, name);
            if (value > 1f)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void Positive(float value, string name)
        {
            NumericGuard.Nonnegative(value, name);
            if (value == 0f)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
