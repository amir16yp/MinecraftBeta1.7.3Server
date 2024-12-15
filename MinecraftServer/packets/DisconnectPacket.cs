public class DisconnectPacket : Packet
{
    public string Reason { get; private set; }

    public DisconnectPacket() : base(PacketType.DISCONNECT) { }

    public DisconnectPacket(string reason) : base(PacketType.DISCONNECT)
    {
        Reason = reason;
    }

    public override void Read(BinaryReader reader)
    {
        Reason = ReadString16(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteString16(writer, Reason ?? string.Empty);
    }   
}