public class LoginRequestPacket : Packet
{
    public int ProtocolVersion { get; private set; }
    public string Username { get; private set; }
    public long MapSeed { get; set; }
    public byte Dimension { get; set; }

    public int EntityID { get; set; }
    public string Unknown { get; private set; }

    public LoginRequestPacket() : base(PacketType.LOGIN_REQUEST) { }

    public override void Read(BinaryReader reader)
    {
        ProtocolVersion = ReadInt(reader);
        Username = ReadString16(reader);
        MapSeed = ReadLong(reader);
        Dimension = ReadByte(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteInt(writer, EntityID);
        WriteString16(writer, Unknown ?? string.Empty);
        WriteLong(writer, MapSeed);
        WriteByte(writer, Dimension);
    }
}