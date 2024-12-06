public class PlayerPositionAndLookPacket : Packet
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Stance { get; private set; }
    public double Z { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool OnGround { get; private set; }

    public PlayerPositionAndLookPacket() : base(PacketType.PLAYER_POSITION_LOOK) { }

    public PlayerPositionAndLookPacket(double x, double y, double stance, double z, float yaw, float pitch, bool onGround)
        : this()
    {
        X = x;
        Y = y;
        Stance = stance;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
        OnGround = onGround;
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        writer.Write(X);
        writer.Write(Stance);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write(Yaw);
        writer.Write(Pitch);
        writer.Write(OnGround);
    }

    public override void Read(BinaryReader reader)
    {
        X = reader.ReadDouble();
        Stance = reader.ReadDouble();
        Y = reader.ReadDouble();
        Z = reader.ReadDouble();
        Yaw = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        OnGround = reader.ReadBoolean();
    }
}