using System.Diagnostics.CodeAnalysis;

namespace Nostrid
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}
