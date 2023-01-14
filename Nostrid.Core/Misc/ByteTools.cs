using System.Text;

namespace Nostrid.Misc
{
    public class ByteTools
    {

        // NIP-19 https://github.com/nostr-protocol/nips/blob/master/19.md

        public static string HexToBech32(string hex, string prefix)
        {
            var hrp = ASCIIEncoding.ASCII.GetBytes(prefix);
            var bytes = Convert.FromHexString(hex);
            return Bech32.Encode(hrp, bytes);
        }

        public static string Bech32ToHex(string bech32, string prefix)
        {
            if (!bech32.StartsWith(prefix))
            {
                return null;
            }
            var hrp = ASCIIEncoding.ASCII.GetBytes(prefix);
            var bytes = Bech32.Decode(hrp, bech32);
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
                var (prefix, _) = ByteTools.DecodeBech32(bech32);
                if (prefix == null) return string.Empty;
            }
            return $"{bech32[..7]}...{bech32[^7..]}";
        }
    }
}
