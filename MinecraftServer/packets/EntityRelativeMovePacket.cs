namespace MinecraftServer;

public class EntityRelativeMovePacket : Packet
{
    public int EntityId { get; set; }
    public sbyte DeltaX { get; set; }
    public sbyte DeltaY { get; set; }
    public sbyte DeltaZ { get; set; }

    public EntityRelativeMovePacket() : base(PacketType.ENTITY_MOVE) { }

    public override void Read(BinaryReader reader)
    {
        EntityId = ReadInt(reader);
        DeltaX = ReadSByte(reader);
        DeltaY = ReadSByte(reader);
        DeltaZ = ReadSByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityId);
        WriteSByte(writer, DeltaX);
        WriteSByte(writer, DeltaY);
        WriteSByte(writer, DeltaZ);
    }
}