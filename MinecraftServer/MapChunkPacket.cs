using System.IO.Compression;

public class MapChunkPacket : Packet
{
    public int XPosition { get; set; }
    public int YPosition { get; set; }
    public int ZPosition { get; set; }
    public int XSize { get; set; }
    public int YSize { get; set; }
    public int ZSize { get; set; }
    public byte[] Chunk { get; set; }
    private int chunkSize;
    public MapChunkPacket(int xPosition, int yPosition, int zPosition, byte[] chunkData) 
        : base(PacketType.MAP_CHUNK)
    {
        this.XPosition = xPosition;
        this.YPosition = yPosition;
        this.ZPosition = zPosition;

        // Assuming chunk size (this is example; adjust as necessary)
        this.XSize = 16; // Example size
        this.YSize = 128; // Example size
        this.ZSize = 16; // Example size
        this.chunkSize = chunkData.Length;
        this.Chunk = chunkData;
    }
    // Override Read method from Packet class
    public override void Read(BinaryReader reader)
    {
        
    }

    // Override Write method from Packet class
    public override void Write(BinaryWriter writer)
    {
        base.Write(writer); // Calls the base Write method to write the packet type
        WriteInt(writer, this.XPosition);
        WriteShort(writer, (short)this.YPosition);
        WriteInt(writer, this.ZPosition);
        WriteByte(writer, (byte)(this.XSize - 1));
        WriteByte(writer, (byte)(this.YSize - 1));
        WriteByte(writer, (byte)(this.ZSize - 1));
        WriteInt(writer, this.chunkSize);
        writer.Write(this.Chunk);
    }
    
    
}
