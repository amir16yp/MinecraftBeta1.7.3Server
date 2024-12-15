using System.Text;

public abstract class Packet
{
    public PacketType Type { get; protected set; }

    public Packet(PacketType type)
    {
        Type = type;
    }

    // Existing string methods
    protected static string ReadString(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(64);
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    protected static void WriteString(BinaryWriter writer, string s)
    {
        byte[] bytes = new byte[64];
        byte[] stringBytes = Encoding.UTF8.GetBytes(s);
        Array.Copy(stringBytes, bytes, Math.Min(stringBytes.Length, 64));
        writer.Write(bytes);
    }

    // Byte methods
    protected static byte ReadByte(BinaryReader reader)
    {
        return reader.ReadByte();
    }

    protected static void WriteByte(BinaryWriter writer, byte value)
    {
        writer.Write(value);
    }

    // Short methods (2 bytes, big-endian)
    protected static short ReadShort(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    protected static void WriteShort(BinaryWriter writer, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes, 0, 2);
    }

    // Int methods (4 bytes, big-endian)
    protected static int ReadInt(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    protected static void WriteInt(BinaryWriter writer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes, 0, 4);
    }

    // Long methods (8 bytes, big-endian)
    protected static long ReadLong(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }

    protected static void WriteLong(BinaryWriter writer, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes, 0, 8);
    }

    // Float methods (4 bytes, big-endian)
    protected static float ReadFloat(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    protected static void WriteFloat(BinaryWriter writer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes, 0, 4);
    }

      // SByte methods (signed byte)
        protected static sbyte ReadSByte(BinaryReader reader)
        {
            return (sbyte)reader.ReadByte();
        }
    
        protected static void WriteSByte(BinaryWriter writer, sbyte value)
        {
            writer.Write((byte)value);
        }

    
    // Double methods (8 bytes, big-endian)
    protected static double ReadDouble(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    protected static void WriteDouble(BinaryWriter writer, double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes, 0, 8);
    }

    // Boolean methods
    protected static bool ReadBool(BinaryReader reader)
    {
        return reader.ReadByte() == 0x01;
    }

    protected static void WriteBool(BinaryWriter writer, bool value)
    {
        writer.Write(value ? (byte)0x01 : (byte)0x00);
    }

    // String8 method (length-prefixed UTF-8)
    protected static string ReadString8(BinaryReader reader)
    {
        short length = ReadShort(reader);
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    protected static void WriteString8(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteShort(writer, (short)bytes.Length);
        writer.Write(bytes);
    }

    // String16 method (length-prefixed UCS-2)
    protected static string ReadString16(BinaryReader reader)
    {
        short length = ReadShort(reader);
        byte[] bytes = reader.ReadBytes(length * 2);
        return Encoding.BigEndianUnicode.GetString(bytes);
    }

    protected static void WriteString16(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.BigEndianUnicode.GetBytes(value);
        WriteShort(writer, (short)(bytes.Length / 2));
        writer.Write(bytes);
    }

    public virtual void Write(BinaryWriter writer)
    {
        writer.Write((byte)Type);
    }

    public abstract void Read(BinaryReader reader);
}