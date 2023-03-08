namespace Nostrid.Misc
{
    public class Tvl : List<(byte Type, byte[] Data)>
    {
        public Tvl()
        {
        }

        public Tvl(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length;)
            {
                var type = bytes[i];
                var length = bytes[i + 1];
                var data = bytes[(i + 2)..(i + 2 + length)];
                Add((type, data));
                i += 2 + length;
            }
        }

        public byte[] ToBytes()
        {
            return this.SelectMany(t => new[] { t.Type, (byte)t.Data.Length }.Concat(t.Data)).ToArray();
        }
    }
}
