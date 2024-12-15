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
        base.Write(writer);
        // X,Y,Z should be block coordinates, not chunk coordinates
        WriteInt(writer, this.XPosition * 16);  // Convert chunk coords to block coords
        WriteShort(writer, (short)this.YPosition);
        WriteInt(writer, this.ZPosition * 16);
    
        // Size values must be size-1 per protocol
        WriteByte(writer, (byte)(this.XSize - 1)); // 15 for full chunk
        WriteByte(writer, (byte)(this.YSize - 1)); // 127 for full chunk  
        WriteByte(writer, (byte)(this.ZSize - 1)); // 15 for full chunk
    
        WriteInt(writer, this.chunkSize);
        writer.Write(this.Chunk);
    }
    
    
}
