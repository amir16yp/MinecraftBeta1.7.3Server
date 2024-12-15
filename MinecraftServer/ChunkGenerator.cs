using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

public class ChunkGenerator
{
public byte[] GenerateFlatChunk(int groundLevel = 60)
{
    const int Size_X = 16;
    const int Size_Y = 128;
    const int Size_Z = 16;

    int totalBlocks = Size_X * Size_Y * Size_Z;
    List<byte> blockTypes = new List<byte>(totalBlocks);
    List<byte> metadata = new List<byte>(totalBlocks / 2);
    List<byte> blockLight = new List<byte>(totalBlocks / 2);
    List<byte> skyLight = new List<byte>(totalBlocks / 2);

    // Fill arrays using protocol's index calculation:
    // index = y + (z * Size_Y) + (x * Size_Y * Size_Z)
    for (int x = 0; x < Size_X; x++)
    {
        for (int z = 0; z < Size_Z; z++)
        {
            for (int y = 0; y < Size_Y; y++)
            {
                int index = y + (z * Size_Y) + (x * Size_Y * Size_Z);

                // Block type
                byte blockType = 0;
                if (y == groundLevel) blockType = 2; // Grass
                else if (y < groundLevel) blockType = 3; // Dirt

                blockTypes.Add(blockType);

                // Metadata, blockLight and skyLight are nibble arrays
                if (index % 2 == 0)
                {
                    metadata.Add(0);
                    blockLight.Add(0);
                    skyLight.Add(0xF0); // High nibble is for the current block, set to maximum light
                }
                else
                {
                    int arrayIndex = index / 2;
                    metadata[arrayIndex] |= 0;
                    blockLight[arrayIndex] |= 0;
                    skyLight[arrayIndex] |= 0x0F; // Low nibble is for the current block, set to maximum light
                }
            }
        }
    }

    // Combine arrays in protocol order
    var combinedData = new List<byte>();
    combinedData.AddRange(blockTypes);     // Block type array
    combinedData.AddRange(metadata);       // Block metadata array (nibbles)
    combinedData.AddRange(blockLight);     // Block light array (nibbles)
    combinedData.AddRange(skyLight);       // Sky light array (nibbles)

    return Compress(combinedData.ToArray());
}    public static byte[] Compress(byte[] input)
    {
        if (input == null || input.Length == 0)
            return input;

        try
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = new ZLibStream(  outputStream, CompressionLevel.Optimal, true))
                {
                    compressionStream.Write(input, 0, input.Length);
                }

                return outputStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compression error: {ex.Message}");
            throw;
        }
    }


}
