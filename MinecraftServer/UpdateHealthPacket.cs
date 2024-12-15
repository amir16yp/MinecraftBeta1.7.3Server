namespace MinecraftServer;

public class UpdateHealthPacket : Packet
{
    public short hp;
    public UpdateHealthPacket(short HP) : base(PacketType.UPDATE_HEALTH)
    {
        this.hp = HP;
    }

    public UpdateHealthPacket() : base(PacketType.UPDATE_HEALTH)
    {
        
    }

    public override void Read(BinaryReader reader)
    {
        throw new NotImplementedException();
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteShort(writer, hp);
    }
}