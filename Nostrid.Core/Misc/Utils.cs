using System.Globalization;
using System.Text;
using Jdenticon;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Reflection;

namespace Nostrid.Misc
{
    public partial class Utils
    {
        private static readonly Regex hashtagRegex = HashtagRegex();
        private static readonly Regex accountMentionPubKeyRegex = AccountMentionPubKeyRegex();
        private static readonly Regex accountMentionBech32Regex = AccountMentionBech32Regex();
        private static readonly Regex noteMentionBech32Regex = NoteMentionBech32Regex();
        private static readonly Regex onlyHashtagRegex = OnlyHashtagRegex();

        public static string ToSvgIdenticon(string hex, int size = 48)
        {
            try
            {
                return string.IsNullOrEmpty(hex) ? string.Empty : Identicon.FromHash(hex, size).ToSvg();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsHashTag(string str)
        {
            return onlyHashtagRegex.IsMatch(str.ToLower());
        }

        public static string GetHashTag(string str)
        {
            return onlyHashtagRegex.Match(str.ToLower()).Groups[1].Value;
        }

        public static IEnumerable<string> GetHashTags(string content)
        {
            return hashtagRegex.Matches(content).Select(m => m.Groups[1].Value.ToLower()).Distinct();
        }

        public static IEnumerable<string> GetAccountPubKeyMentions(string content)
        {
            return accountMentionPubKeyRegex.Matches(content).Select(m => m.Groups[1].Value.ToLower()).Distinct();
        }

        public static IEnumerable<string> GetAccountNpubMentions(string content)
        {
            return accountMentionBech32Regex.Matches(content).Select(m => m.Groups[1].Value.ToLower()).Distinct();
        }

        public static IEnumerable<string> GetNoteMentions(string content)
        {
            return noteMentionBech32Regex.Matches(content).Select(m => m.Groups[1].Value.ToLower()).Distinct();
        }

        public static bool IsValidNostrId(string id)
        {
            const string ValidChars = "0123456789abcdef";
            return !string.IsNullOrEmpty(id) && id.Length == 64 && id.All(c => ValidChars.IndexOf(c) != -1);
        }

        public static string FormatDate(DateTimeOffset dateUtc)
        {
            var now = DateTimeOffset.UtcNow;
            var diff = now - dateUtc;
            if (diff.TotalSeconds > 0)
            {
                if (diff.TotalSeconds < 60)
                {
                    return ((int)diff.TotalSeconds).ToString() + "s";
                }
                if (diff.TotalMinutes < 60)
                {
                    return ((int)diff.TotalMinutes).ToString() + "m";
                }
                if (diff.TotalHours < 24)
                {
                    return ((int)diff.TotalHours).ToString() + "h";
                }
                if (diff.TotalDays < 365)
                {
                    return ((int)diff.TotalDays).ToString() + "d";
                }
				return ((int)(diff.TotalDays / 365)).ToString() + "y";
			}
			return dateUtc.ToString("yyyy-MM-dd hh:mm:ss tt");
        }

        public static string HashWithSHA256(string value)
        {
            using var hash = SHA256.Create();
            return Convert.ToHexString(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        public static string GetCurrentVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
			AssemblyInformationalVersionAttribute versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return versionAttribute.InformationalVersion;
        }

        [GeneratedRegex("#([a-zA-Z0-9_]+)", RegexOptions.Compiled)]
        private static partial Regex HashtagRegex();

        [GeneratedRegex("@([a-f0-9]{64})", RegexOptions.Compiled)]
        private static partial Regex AccountMentionPubKeyRegex();

        [GeneratedRegex("@(npub1[qpzry9x8gf2tvdw0s3jn54khce6mua7l]{6,})", RegexOptions.Compiled)]
        private static partial Regex AccountMentionBech32Regex();

        [GeneratedRegex("(note1[qpzry9x8gf2tvdw0s3jn54khce6mua7l]{6,})", RegexOptions.Compiled)]
        private static partial Regex NoteMentionBech32Regex();

        [GeneratedRegex("^#([a-zA-Z0-9_]+)$", RegexOptions.Compiled)]
        private static partial Regex OnlyHashtagRegex();
    }
}
