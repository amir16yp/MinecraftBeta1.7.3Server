public class DestroyEntityPacket : Packet
{
    public int EntityId { get; set; } // The Entity ID (EID) of the entity to destroy

    public DestroyEntityPacket() : base(PacketType.DESTORY_ENTITY) { }

    public override void Read(BinaryReader reader)
    {
        // Read the Entity ID from the stream
        EntityId = ReadInt(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        // Write the Packet ID (0x1D)
        base.Write(writer);

        // Write the Entity ID
        WriteInt(writer, EntityId);
    }
}