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

        public static string ByteArrayToBech32(byte[] bytes, string prefix)
        {
            var hrp = Encoding.ASCII.GetBytes(prefix);
            return Bech32.Encode(hrp, bytes);
        }

        public static string HexToBech32(string hex, string prefix)
        {
            var bytes = Convert.FromHexString(hex);
            return ByteArrayToBech32(bytes, prefix);
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

        public static string? EncodeTvlBech32(TvlEntity tvlEntity)
        {
            var tvl = tvlEntity.GetTvl();
            var bytes = tvl.ToBytes();
            return tvlEntity switch
            {
                Nevent => ByteArrayToBech32(bytes, "nevent"),
                Naddr => ByteArrayToBech32(bytes, "naddr"),
                EncryptedKey => ByteArrayToBech32(bytes, "npk"),
                _ => null,
            };
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
                        tvlEntity = pr switch
                        {
                            "nevent" => new Nevent(new Tvl(bytes)),
                            "naddr" => new Naddr(new Tvl(bytes)),
                            "npk" => new EncryptedKey(new Tvl(bytes)),
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

        public static string EventIdToString(string id, bool shorten = false)
        {
            Naddr naddr;
            if (id.IsReplaceableId() && (naddr = new Naddr(id)).IsValid)
            {
                return ShortenBech32(EncodeTvlBech32(naddr), true);
            }
            else
            {
                return PubkeyToBech32(id, "note", shorten);
            }
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
