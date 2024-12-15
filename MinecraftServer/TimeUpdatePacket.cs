namespace MinecraftServer;

public class TimeUpdatePacket : Packet
{
    public long time;
    public TimeUpdatePacket(long time) : base(PacketType.TIME_UPDATE)
    {
        this.time = time;
    }

    public TimeUpdatePacket() : base(PacketType.TIME_UPDATE)
    {
        throw new NotImplementedException();
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteLong(writer, time);
    }

    public override void Read(BinaryReader reader)
    {
        throw new NotImplementedException();
    }
}