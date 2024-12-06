public class PlayerPositionPacket : Packet
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Stance { get; private set; }
    public double Z { get; private set; }
    public bool OnGround { get; private set; }

    public PlayerPositionPacket() : base(PacketType.PLAYER_POSITION) { }

    public PlayerPositionPacket(double x, double y, double stance, double z, bool onGround)
        : this()
    {
        X = x;
        Y = y;
        Stance = stance;
        Z = z;
        OnGround = onGround;
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Stance);
        writer.Write(Z);
        writer.Write(OnGround);
    }

    public override void Read(BinaryReader reader)
    {
        X = reader.ReadDouble();
        Y = reader.ReadDouble();
        Stance = reader.ReadDouble();
        Z = reader.ReadDouble();
        OnGround = reader.ReadBoolean();
    }
}