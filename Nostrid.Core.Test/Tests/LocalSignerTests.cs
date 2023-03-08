using NNostr.Client;
using Nostrid.Misc;
using Nostrid.Model;
using System.Security.Cryptography;

namespace Nostrid.Core.Test
{
    [TestClass]
    public class LocalSignerTests
    {
        [TestMethod]
        public async Task TestEncryptPk()
        {
            var aesEncryptor = new AesEncryptor();
            var localSignerFactory = new LocalSignerFactory(aesEncryptor);

            var pk = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
            var pwd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
            var encryptedPk = await localSignerFactory.EncryptPrivKey(pk, pwd);
            var decryptedPk = await localSignerFactory.DecryptPrivKey(encryptedPk, pwd);
            Assert.AreEqual(pk, decryptedPk);

            decryptedPk = await localSignerFactory.DecryptPrivKey(encryptedPk, pwd + "!");
            Assert.AreEqual(null, decryptedPk);
        }

        [TestMethod]
        public async Task TestEncryptPkBech32()
        {
            var aesEncryptor = new AesEncryptor();
            var localSignerFactory = new LocalSignerFactory(aesEncryptor);

            var pk = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
            var pwd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
            var encryptedPk = await localSignerFactory.EncryptPrivKey(pk, pwd);


            var bech32 = ByteTools.EncodeTvlBech32(encryptedPk);
            Assert.IsNotNull(bech32);
            Assert.IsTrue(ByteTools.TryDecodeTvlBech32(bech32, out var tvlEntity));

            var encryptedPkFromBech32 = tvlEntity as EncryptedKey;
            Assert.IsNotNull(encryptedPkFromBech32);

            var decryptedPk = await localSignerFactory.DecryptPrivKey(encryptedPkFromBech32, pwd);
            Assert.AreEqual(pk, decryptedPk);
        }

    }
}