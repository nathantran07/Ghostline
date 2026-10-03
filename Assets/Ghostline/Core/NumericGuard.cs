using System;

namespace Ghostline.Core
{
    internal static class NumericGuard
    {
        internal static void Finite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }

        internal static void Nonnegative(float value, string parameterName)
        {
            Finite(value, parameterName);
            if (value < 0f)
                throw new ArgumentOutOfRangeException(parameterName, "Value must be nonnegative.");
        }
    }
}
