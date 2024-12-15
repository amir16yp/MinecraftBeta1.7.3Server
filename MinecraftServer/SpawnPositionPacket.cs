namespace MinecraftServer;

public class SpawnPositionPacket : Packet
{
    public int x;
    public int y;
    public int z;
    
    public SpawnPositionPacket(int x, int y, int z) : base(PacketType.SPAWN_POSITION)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public SpawnPositionPacket() : base(PacketType.SPAWN_POSITION)
    {
        
    }

    public override void Read(BinaryReader reader)
    {
        throw new NotImplementedException();
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, x);
        WriteInt(writer, y);
        WriteInt(writer, z);
    }
}