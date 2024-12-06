//namespace MinecraftServer;

public class PlayerLookPacket : Packet
{

    public float yaw;
    public float pitch;
    public bool onGround;
    
    public PlayerLookPacket() : base(PacketType.PLAYER_LOOK)
    {
    }

    public override void Read(BinaryReader reader)
    {
        yaw = ReadFloat(reader);
        pitch = ReadFloat(reader);
        onGround = ReadBool(reader);
    }
}