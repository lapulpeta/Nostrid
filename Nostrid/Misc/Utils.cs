using System.Globalization;
using System.Text;
using Jdenticon;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Reflection;

namespace Nostrid.Misc
{
    internal partial class Utils
    {
        private static readonly Regex hashtagRegex = HashtagRegex();
        private static readonly Regex accountMentionPubKeyRegex = AccountMentionPubKeyRegex();
        private static readonly Regex accountMentionBech32Regex = AccountMentionBech32Regex();
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

        public static string JavaScriptStringDecode(string value, bool removeDoubleQuotes)
        {
            StringBuilder b = new StringBuilder();
            int startIndex = 0;
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\\')
                {
                    if (count > 0)
                    {
                        b.Append(value, startIndex, count);
                        count = 0;
                    }

                    if (i < value.Length - 1)
                    {
                        var c1 = value[i + 1];
                        bool ignore = false;
                        char charToAppend;

                        var style = NumberStyles.HexNumber;
                        var cult = CultureInfo.InvariantCulture;
                        int u;

                        switch (c1)
                        {
                            case '\'':
                                charToAppend = '\'';
                                break;
                            case '\"':
                                charToAppend = '\"';
                                break;
                            case '\\':
                                charToAppend = '\\';
                                break;
                            case 'n':
                                charToAppend = '\n';
                                break;
                            case 'r':
                                charToAppend = '\r';
                                break;
                            case 'v':
                                charToAppend = '\v';
                                break;
                            case 't':
                                charToAppend = '\t';
                                break;
                            case 'b':
                                charToAppend = '\b';
                                break;
                            case 'f':
                                charToAppend = '\f';
                                break;
                            case 'u':
                                if (value[i + 2] == '{')
                                {
                                    if (value[i + 4] == '}' && int.TryParse(value.Substring(i + 3, 1), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 5;
                                        break;
                                    }
                                    else if (value[i + 5] == '}' && int.TryParse(value.Substring(i + 3, 2), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 6;
                                        break;
                                    }
                                    else if (value[i + 6] == '}' && int.TryParse(value.Substring(i + 3, 3), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 7;
                                        break;
                                    }
                                    else if (value[i + 7] == '}' && int.TryParse(value.Substring(i + 3, 4), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 8;
                                        break;
                                    }
                                    else if (value[i + 8] == '}' && int.TryParse(value.Substring(i + 3, 5), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 9;
                                        break;
                                    }
                                    else if (value[i + 9] == '}' && int.TryParse(value.Substring(i + 3, 6), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 10;
                                        break;
                                    }
                                    else
                                    {
                                        // Syntax Error
                                        throw new FormatException(@"The Unicode code point should be \u{X} ~ \u{XXXXXX}.");
                                    }
                                }
                                else
                                {
                                    if (i < value.Length - 5)
                                    {
                                        if (int.TryParse(value.Substring(i + 2, 4), style, cult, out u))
                                        {
                                            charToAppend = (char)u;
                                            i += 4;
                                            break;
                                        }
                                    }
                                }
                                charToAppend = '\\';
                                ignore = true;
                                break;
                            case 'x':
                                if (i < value.Length - 3)
                                {
                                    if (int.TryParse(value.Substring(i + 2, 2), style, cult, out u))
                                    {
                                        charToAppend = (char)u;
                                        i += 2;
                                        break;
                                    }
                                }
                                charToAppend = '\\';
                                ignore = true;
                                break;
                            default:
                                charToAppend = '\\';
                                ignore = true;

                                break;
                        }
                        if (!ignore)
                        {
                            i++;
                        }
                        startIndex = i + 1;
                        b.Append(charToAppend);
                        continue;
                    }
                }
                count++;
            }
            if (count > 0)
            {
                b.Append(value, startIndex, count);
            }
            if (removeDoubleQuotes)
            {
                if (b.Length > 0)
                {
                    if (b[0] == '"')
                    {
                        b.Remove(0, 1);
                    }
                    if (b[b.Length - 1] == '"')
                    {
                        b.Remove(b.Length - 1, 1);
                    }
                }
            }
            return b.ToString();
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

        [GeneratedRegex("#([a-z0-9_]+)", RegexOptions.Compiled)]
        private static partial Regex HashtagRegex();

        [GeneratedRegex("@([a-f0-9]{64})", RegexOptions.Compiled)]
        private static partial Regex AccountMentionPubKeyRegex();

        [GeneratedRegex("@(npub1[qpzry9x8gf2tvdw0s3jn54khce6mua7l]{6,})", RegexOptions.Compiled)]
        private static partial Regex AccountMentionBech32Regex();

        [GeneratedRegex("^#([a-z0-9_]+)$", RegexOptions.Compiled)]
        private static partial Regex OnlyHashtagRegex();
    }
}
