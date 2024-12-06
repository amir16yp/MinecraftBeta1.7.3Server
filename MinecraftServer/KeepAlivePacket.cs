//namespace MinecraftServer;

public class KeepAlivePacket : Packet
{
    public KeepAlivePacket() : base(PacketType.KEEPALIVE)
    {
        
    }

    public override void Read(BinaryReader reader)
    {
        
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
    }
}