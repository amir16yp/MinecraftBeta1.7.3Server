namespace MinecraftServer;

public class EntityTeleportPacket : Packet
{
    public int EntityId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte Yaw { get; set; }
    public byte Pitch { get; set; }

    public EntityTeleportPacket() : base(PacketType.ENTITY_TELEPORT) { }

    public override void Read(BinaryReader reader)
    {
        EntityId = ReadInt(reader);
        X = ReadInt(reader);
        Y = ReadInt(reader);
        Z = ReadInt(reader);
        Yaw = ReadByte(reader);
        Pitch = ReadByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityId);
        WriteInt(writer, X);
        WriteInt(writer, Y);
        WriteInt(writer, Z);
        WriteByte(writer, Yaw);
        WriteByte(writer, Pitch);
    }
}