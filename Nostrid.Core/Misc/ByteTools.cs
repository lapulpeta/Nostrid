using Nostrid.Model;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Nostrid.Misc
{
    public class ByteTools
    {

        // NIP-19 https://github.com/nostr-protocol/nips/blob/master/19.md

        public static string StringToBech32(string str, string prefix)
        {
            var hrp = Encoding.ASCII.GetBytes(prefix);
            var bytes = Encoding.UTF8.GetBytes(str);
            return Bech32.Encode(hrp, bytes);
        }

        public static string? Bech32ToString(string bech32, string prefix)
        {
            if (!bech32.StartsWith(prefix))
            {
                return null;
            }
            var hrp = Encoding.ASCII.GetBytes(prefix);
            var bytes = Bech32.Decode(hrp, bech32);
            return Encoding.UTF8.GetString(bytes).ToLower();
        }

        public static string HexToBech32(string hex, string prefix)
        {
            var hrp = Encoding.ASCII.GetBytes(prefix);
            var bytes = Convert.FromHexString(hex);
            return Bech32.Encode(hrp, bytes);
        }

        public static byte[]? Bech32ToByteArray(string bech32, string prefix)
        {
            if (!bech32.StartsWith(prefix))
            {
                return null;
            }
            var hrp = Encoding.ASCII.GetBytes(prefix);
            var bytes = Bech32.Decode(hrp, bech32);
            return bytes;
        }

        public static string? Bech32ToHex(string bech32, string prefix)
        {
            var bytes = Bech32ToByteArray(bech32, prefix);
            if (bytes == null)
            {
                return null;
            }
            return Convert.ToHexString(bytes).ToLower();
        }

        private static readonly string[] ValidPrefixes = { "npub", "nsec", "note" };

        public static (string? Prefix, string? Hex) DecodeBech32(string bech32)
        {
            if (TryDecodeBech32(bech32, out var prefix, out var hex))
            {
                return (prefix, hex);
            }
            return (null, null);
        }

        public static bool TryDecodeBech32(string bech32, out string? prefix, out string? hex)
        {
            try
            {
                if (!string.IsNullOrEmpty(bech32))
                {
                    foreach (var pr in ValidPrefixes)
                    {
                        if (bech32.StartsWith(pr))
                        {
                            prefix = pr;
                            hex = Bech32ToHex(bech32, pr);
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            prefix = null;
            hex = null;
            return false;
        }

        private static string GetHrp(string bech32)
        {
            return bech32.Split("1")[0];
        }

        public static bool TryDecodeTvlBech32(string bech32, [NotNullWhen(true)] out TvlEntity? tvlEntity)
        {
            try
            {
                if (!string.IsNullOrEmpty(bech32))
                {
                    var pr = GetHrp(bech32);
                    var bytes = Bech32ToByteArray(bech32, pr);
                    if (bytes != null)
                    {
                        var tvl = BytesToTvl(bytes);
                        tvlEntity = pr switch
                        {
                            "nevent" => new Nevent(tvl),
                            "naddr" => new Naddr(tvl),
                            _ => null
                        };
                        if (tvlEntity != null)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            tvlEntity = null;
            return false;
        }

        private static List<(NostrTvlType, byte[])> BytesToTvl(byte[] bytes)
        {
            List<(NostrTvlType, byte[])> ret = new();
            for (int i = 0; i < bytes.Length;)
            {
                var type = bytes[i];
                var length = bytes[i + 1];
                var data = bytes[(i + 2)..(i + 2 + length)];
                ret.Add(((NostrTvlType)type, data));
                i += 2 + length;
            }
            return ret;
        }

        public static string PubkeyToNpub(string pubkey, bool shorten = false)
        {
            return PubkeyToBech32(pubkey, "npub", shorten);
        }

        public static string PubkeyToNsec(string pubkey, bool shorten = false)
        {
            return PubkeyToBech32(pubkey, "nsec", shorten);
        }

        public static string PubkeyToNote(string pubkey, bool shorten = false)
        {
            return PubkeyToBech32(pubkey, "note", shorten);
        }

        public static string PubkeyToBech32(string pubkey, string prefix, bool shorten = false)
        {
            if (!Utils.IsValidNostrId(pubkey)) return string.Empty;
            var bech32 = HexToBech32(pubkey, prefix);
            if (shorten)
            {
                bech32 = ShortenBech32(bech32, true);
            }
            return bech32;
        }

        public static string ShortenBech32(string bech32, bool skipVerify = false)
        {
            if (!skipVerify)
            {
                var (prefix, _) = DecodeBech32(bech32);
                if (prefix == null) return string.Empty;
            }
            return $"{bech32[..7]}...{bech32[^7..]}";
        }
    }
}
