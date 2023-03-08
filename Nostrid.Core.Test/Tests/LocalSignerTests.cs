using NNostr.Client;
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

    }
}