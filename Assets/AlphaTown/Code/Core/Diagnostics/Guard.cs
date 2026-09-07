using System;

namespace AlphaTown.Core.Diagnostics
{
    /// <summary>Argument checks for the seams between systems. Cheap, and they fail loudly in tests.</summary>
    public static class Guard
    {
        public static T NotNull<T>(T value, string parameterName) where T : class
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            return value;
        }

        public static string NotNullOrEmpty(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Must not be null or empty.", parameterName);
            return value;
        }

        public static int Positive(int value, string parameterName)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "Must be greater than zero.");
            return value;
        }

        public static int NotNegative(int value, string parameterName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "Must not be negative.");
            return value;
        }
    }
}
