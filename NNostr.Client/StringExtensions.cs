using System.Text;
using NBitcoin.Secp256k1;

namespace NNostr.Client
{
    public static class StringExtensions
    {
        public static byte[] DecodHexData(this string encoded)
        {
            return Convert.FromHexString(encoded);
        }

        public static string ComputeSignature(this string rawData, ECPrivKey privKey)
        {
            var bytes = rawData.ComputeSha256Hash();
            var buf = new byte[64];
            privKey.SignBIP340(bytes).WriteToSpan(buf);
            return Convert.ToHexString(buf).ToLower();
        }

        public static byte[] ComputeSha256Hash(this string rawData)
        {
            // Create a SHA256   
            using var sha256Hash = System.Security.Cryptography.SHA256.Create();
            // ComputeHash - returns byte array  
            return sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        }

        public static string ToHex(this byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLower();
        }


    }
}