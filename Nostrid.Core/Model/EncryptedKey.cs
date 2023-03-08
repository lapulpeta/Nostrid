using Nostrid.Misc;
using System.Buffers.Binary;

namespace Nostrid.Model;

public class EncryptedKey : TvlEntity
{
    public byte[]? EncryptedPk { get; set; }

    public byte[]? Iv { get; set; }

    public byte[]? PwdHashSalt { get; set; }

    public byte[]? PwdHash { get; set; }

    public int HashInterations { get; set; }

    public EncryptedKey()
    {
    }

    public EncryptedKey(Tvl tvl)
    {
        EncryptedPk = tvl[0].Data;
        Iv = tvl[1].Data;
        PwdHashSalt = tvl[2].Data;
        PwdHash = tvl[3].Data;
        HashInterations = BinaryPrimitives.ReadInt32LittleEndian(tvl[4].Data);
    }

    public override Tvl GetTvl()
    {
        var hashInterations = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(hashInterations, HashInterations);
        return new Tvl()
        {
            (0, EncryptedPk),
            (0, Iv),
            (0, PwdHashSalt),
            (0, PwdHash),
            (0, hashInterations),
        };
    }
}
