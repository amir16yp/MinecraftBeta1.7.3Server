public class NamedEntitySpawnPacket : Packet
{
    public int EntityId { get; set; }
    public string PlayerName { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte Yaw { get; set; }
    public byte Pitch { get; set; }
    public short CurrentItem { get; set; }

    public NamedEntitySpawnPacket() : base(PacketType.NAMED_ENTITY_SPAWN) { }

    public override void Read(BinaryReader reader)
    {
        EntityId = ReadInt(reader);
        PlayerName = ReadString16(reader);
        X = ReadInt(reader);
        Y = ReadInt(reader);
        Z = ReadInt(reader);
        Yaw = ReadByte(reader);
        Pitch = ReadByte(reader);
        CurrentItem = ReadShort(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityId);
        WriteString16(writer, PlayerName);
        WriteInt(writer, X);
        WriteInt(writer, Y);
        WriteInt(writer, Z);
        WriteByte(writer, Yaw);
        WriteByte(writer, Pitch);
        WriteShort(writer, CurrentItem);
    }
}