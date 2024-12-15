namespace MinecraftServer;

public class HoldingChangePacket : Packet
{
    public short SlotID;
    public HoldingChangePacket() : base(PacketType.HOLDING_CHANGE)
    {
            
    }

    public override void Read(BinaryReader reader)
    {
        SlotID = ReadShort(reader);
    }
}