namespace Nostrid
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNotNullOrEmpty(this string? value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}
