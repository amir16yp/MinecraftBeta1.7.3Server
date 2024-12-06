using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

public class ChunkGenerator
{
        public byte[] GenerateFlatChunk(int groundLevel = 0, int Size_X = 16, int Size_Y = 128, int Size_Z = 16) 
        {
        // Total number of blocks (including +1 on each dimension)
        int totalBlocks = (Size_X + 1) * (Size_Y + 1) * (Size_Z + 1);

        // Size for metadata, blockLight, and skyLight (each uses half-byte per block)
        int totalMetadataSize = totalBlocks / 2;

        // Create block arrays
        List<byte> blockTypes = new List<byte>(totalBlocks);
        List<byte> metadata = new List<byte>(totalMetadataSize);
        List<byte> blockLight = new List<byte>(totalMetadataSize);
        List<byte> skyLight = new List<byte>(totalMetadataSize);

        // Generate flat terrain and fill block types
        for (int x = 0; x <= Size_X; x++)
        {
            for (int z = 0; z <= Size_Z; z++)
            {
                for (int y = 0; y <= Size_Y; y++)
                {
                    blockTypes.Add(3);
                }
            }
        }

        // Pack metadata, block light, and sky light into half-byte per block (nibble)
        for (int i = 0; i < totalBlocks; i++)
        {
            int byteIndex = i / 2; // Each byte stores two blocks
            int nibbleIndex = i % 2; // Determine if we are setting the lower or upper nibble

            if (nibbleIndex == 0)
            {
                // Lower nibble for current block
                metadata.Add((byte)(blockTypes[i] & 0x0F)); // Low nibble (4 bits)
                blockLight.Add((byte)(0x00 & 0x0F)); // No block light (example)
                skyLight.Add((byte)(0x00 & 0x0F)); // No sky light (example)
            }
            else
            {
                // Upper nibble for the next block
                metadata[byteIndex] |= (byte)((blockTypes[i] & 0x0F) << 4); // High nibble (4 bits)
                blockLight[byteIndex] |= (byte)((0x00 & 0x0F) << 4); // High nibble (example)
                skyLight[byteIndex] |= (byte)((0x00 & 0x0F) << 4); // High nibble (example)
            }
        }

        // Combine all sections into a single byte array (blockTypes, metadata, blockLight, skyLight)
        var combinedData = new List<byte>();
        combinedData.AddRange(blockTypes);
        combinedData.AddRange(metadata);
        combinedData.AddRange(blockLight);
        combinedData.AddRange(skyLight);

        byte[] rawData = combinedData.ToArray();
        
        return Compress(rawData);
    }
        
    public static byte[] Compress(byte[] input)
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
