namespace Nostrid.Model;

public class EncryptedKey
{
    public byte[]? EncryptedPk { get; set; }

    public byte[]? Iv { get; set; }

    public byte[]? PwdHashSalt { get; set; }

    public byte[]? PwdHash { get; set; }

    public int HashInterations { get; set; }
}

