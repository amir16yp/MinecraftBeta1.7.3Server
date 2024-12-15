namespace MinecraftServer;

public class BlockChangePacket : Packet
{
    public int X { get; set; }
    public byte Y { get; set; }
    public int Z { get; set; }
    public byte BlockType { get; set; }
    public byte BlockMetadata { get; set; }

    public BlockChangePacket() : base(PacketType.BLOCK_CHANGE)
    {
    }

    public override void Read(BinaryReader reader)
    {
        X = ReadInt(reader);
        Y = ReadByte(reader);
        Z = ReadInt(reader);
        BlockType = ReadByte(reader);
        BlockMetadata = ReadByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, X);
        WriteByte(writer, Y);
        WriteInt(writer, Z);
        WriteByte(writer, BlockType);
        WriteByte(writer, BlockMetadata);
    }
}