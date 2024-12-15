namespace MinecraftServer;
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

    // For server->client packets, the constructor should match the Write() order
    public PlayerPositionAndLookPacket(double x, double stance, double y, double z, float yaw, float pitch, bool onGround)
        : this()
    {
        X = x;
        Stance = stance;  // Stance comes before Y in server->client packets
        Y = y;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
        OnGround = onGround;
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteDouble(writer, X);         // Use protected methods that handle endianness
        WriteDouble(writer, Stance);    // from the base Packet class
        WriteDouble(writer, Y);
        WriteDouble(writer, Z);
        WriteFloat(writer, Yaw);
        WriteFloat(writer, Pitch);
        WriteBool(writer, OnGround);
    }

    public override void Read(BinaryReader reader)
    {
        X = ReadDouble(reader);
        Y = ReadDouble(reader);       // Client->server sends Y before stance
        Stance = ReadDouble(reader);
        Z = ReadDouble(reader);
        Yaw = ReadFloat(reader);
        Pitch = ReadFloat(reader);
        OnGround = ReadBool(reader);

        // Validate values immediately after reading
        if (double.IsInfinity(X) || double.IsInfinity(Y) || 
            double.IsInfinity(Z) || double.IsInfinity(Stance) ||
            double.IsNaN(X) || double.IsNaN(Y) || 
            double.IsNaN(Z) || double.IsNaN(Stance))
        {
            throw new InvalidDataException("Received invalid position values");
        }
    }
}