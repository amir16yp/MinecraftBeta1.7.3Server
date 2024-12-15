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
        WriteDouble(writer, X);
        WriteDouble(writer, Y);
        WriteDouble(writer, Stance);
        WriteDouble(writer, Z);
        WriteBool(writer, OnGround);
    }

    public override void Read(BinaryReader reader)
    {
        X = ReadDouble(reader);
        Y = ReadDouble(reader);
        Stance = ReadDouble(reader);
        Z = ReadDouble(reader);
        OnGround = ReadBool(reader);
    }
}