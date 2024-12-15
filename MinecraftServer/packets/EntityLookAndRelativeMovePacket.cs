namespace MinecraftServer;

public class EntityLookAndRelativeMovePacket : Packet
{
    public int EntityId { get; set; }
    public sbyte DeltaX { get; set; }
    public sbyte DeltaY { get; set; }
    public sbyte DeltaZ { get; set; }
    public byte Yaw { get; set; }
    public byte Pitch { get; set; }

    public EntityLookAndRelativeMovePacket() : base(PacketType.ENTITY_LOOK_RELATIVE_MOVE) { }

    public override void Read(BinaryReader reader)
    {
        EntityId = ReadInt(reader);
        DeltaX = ReadSByte(reader);
        DeltaY = ReadSByte(reader);
        DeltaZ = ReadSByte(reader);
        Yaw = ReadByte(reader);
        Pitch = ReadByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityId);
        WriteSByte(writer, DeltaX);
        WriteSByte(writer, DeltaY);
        WriteSByte(writer, DeltaZ);
        WriteByte(writer, Yaw);
        WriteByte(writer, Pitch);
    }
}