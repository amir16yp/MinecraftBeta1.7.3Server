//namespace MinecraftServer;

public class PlayerFlyingPacket : Packet
{

    public bool onGround;
    
    public PlayerFlyingPacket() : base(PacketType.PLAYER)
    {
        
    }

    public override void Read(BinaryReader reader)
    {
        onGround = ReadBool(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        writer.Write(onGround);
    }
}