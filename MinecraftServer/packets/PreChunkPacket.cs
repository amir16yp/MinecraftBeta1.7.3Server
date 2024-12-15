public class PreChunkPacket : Packet
{
    public int X { get; private set; }
    public int Z { get; private set; }
    public bool Mode { get; private set; }

    public PreChunkPacket(int x, int z, bool mode) : base(PacketType.PRE_CHUNK)
    {
        X = x;
        Z = z;
        Mode = mode;
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, X);
        WriteInt(writer, Z);
        WriteBool(writer, Mode);
    }

    public override void Read(BinaryReader reader)
    {
        throw new NotSupportedException("PreChunkPacket is server to client only");
    }
}