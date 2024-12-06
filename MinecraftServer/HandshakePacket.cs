public class HandshakePacket : Packet
{
    public string Value { get; private set; }

    public HandshakePacket() : base(PacketType.HANDSHAKE) { }

    public HandshakePacket(string value) : base(PacketType.HANDSHAKE)
    {
        Value = value;
    }

    public override void Read(BinaryReader reader)
    {
        Value = ReadString16(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteString16(writer, Value);
    }
}