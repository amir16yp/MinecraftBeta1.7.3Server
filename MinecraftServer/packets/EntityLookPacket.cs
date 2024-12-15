namespace MinecraftServer;

public class EntityLookPacket : Packet
{
    public int EntityId { get; set; }
    public byte Yaw { get; set; }
    public byte Pitch { get; set; }

    public EntityLookPacket() : base(PacketType.ENTITY_LOOK) { }

    public override void Read(BinaryReader reader)
    {
        EntityId = ReadInt(reader);
        Yaw = ReadByte(reader);
        Pitch = ReadByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityId);
        WriteByte(writer, Yaw);
        WriteByte(writer, Pitch);
    }
}