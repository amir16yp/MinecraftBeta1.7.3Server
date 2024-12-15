namespace MinecraftServer;

public class ChunkManager
{
    public Dictionary<(int x, int z), MapChunkPacket> loadedChunks = new();
    private ChunkGenerator chunkGenerator = new();
    
    public void SendAllChunksToPlayer(ClientHandler clientHandler)
    {
        foreach (var chunkEntry in loadedChunks.Values)
        {
            // Notify the client to load the chunk (Mode = 1 for initialization)
            clientHandler.SendPacket(new PreChunkPacket(chunkEntry.XPosition, chunkEntry.ZPosition, true)); // Mode = true for initialization (1)

            clientHandler.SendPacket(chunkEntry); // Assuming chunkEntry is a MapChunkPacket containing the chunk data
        }
    }
    
    public void LoadSpawnChunks(int centerX, int centerZ)
    {
        // Generate chunks for a 3x3 grid centered around the player's position
        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            for (int z = centerZ - 1; z <= centerZ + 1; z++)
            {
                if (!loadedChunks.ContainsKey((x, z)))
                {
                    // Generate a flat chunk for the given coordinates
                    byte[] chunkData = chunkGenerator.GenerateFlatChunk();
                    loadedChunks[(x, z)] = new MapChunkPacket(x, 0, z, chunkData); // yPosition set to 0
                }
            }
        }
    }
    
    
    public MapChunkPacket GetChunk(int x, int z)
    {
        return loadedChunks.TryGetValue((x, z), out var chunk) ? chunk : null;
    }
    
    public void UnloadChunk(int x, int z)
    {
        if (loadedChunks.ContainsKey((x, z)))
        {
            loadedChunks.Remove((x, z));
        }
    }
}