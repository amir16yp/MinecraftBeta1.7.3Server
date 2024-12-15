namespace MinecraftServer;

public class ChatMessagePacket : Packet
{
    public string message = "";
    public ChatMessagePacket(string message) : base(PacketType.CHAT_MESSAGE)
    {
        this.message = message;
    }

    public ChatMessagePacket() : base(PacketType.CHAT_MESSAGE)
    {
        
    }
    
    public override void Read(BinaryReader reader)
    {
        message = ReadString16(reader);
    }

    public override void Write(BinaryWriter writer)
    {
        base.Write(writer);
        WriteString16(writer, message);
    }
}