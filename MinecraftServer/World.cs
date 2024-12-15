using System.Collections.Concurrent;

namespace MinecraftServer;

public class World
{
    private readonly ChunkManager _chunkManager;
    private readonly ChunkGenerator _chunkGenerator;
    private readonly ConcurrentDictionary<(int x, int z), HashSet<Guid>> _chunkSubscribers;
    private readonly ConcurrentDictionary<(int x, int z), byte[]> _blockData;
    private readonly ConcurrentDictionary<(int x, int z), DateTime> _lastAccessTime;
    
    private const int VIEW_DISTANCE = 5; // In chunks (8 chunks = 128 blocks)
    private const int UNLOAD_DELAY = 30; // Seconds to wait before unloading unused chunks
    private const int CHUNK_SIZE_X = 16;
    private const int CHUNK_SIZE_Y = 128;
    private const int CHUNK_SIZE_Z = 16;
    private const int BLOCKS_PER_CHUNK = CHUNK_SIZE_X * CHUNK_SIZE_Y * CHUNK_SIZE_Z;
    private const int TOTAL_CHUNK_SIZE = BLOCKS_PER_CHUNK + ((BLOCKS_PER_CHUNK * 3) >> 1);

    public World()
    {
        _chunkManager = new ChunkManager();
        _chunkGenerator = new ChunkGenerator();
        _chunkSubscribers = new ConcurrentDictionary<(int x, int z), HashSet<Guid>>();
        _blockData = new ConcurrentDictionary<(int x, int z), byte[]>();
        _lastAccessTime = new ConcurrentDictionary<(int x, int z), DateTime>();

        Task.Run(ChunkCleanupLoop);
    }

    public void UpdatePlayerPosition(ClientHandler player, double x, double z)
    {
        int currentChunkX = (int)Math.Floor(x) >> 4;
        int currentChunkZ = (int)Math.Floor(z) >> 4;

        var chunksToLoad = new HashSet<(int x, int z)>();
        var chunksToUnload = new HashSet<(int x, int z)>();

        for (int cx = currentChunkX - VIEW_DISTANCE; cx <= currentChunkX + VIEW_DISTANCE; cx++)
        {
            for (int cz = currentChunkZ - VIEW_DISTANCE; cz <= currentChunkZ + VIEW_DISTANCE; cz++)
            {
                var chunkCoord = (cx, cz);
                chunksToLoad.Add(chunkCoord);

                _chunkSubscribers.GetOrAdd(chunkCoord, _ => new HashSet<Guid>());

                lock (_chunkSubscribers[chunkCoord])
                {
                    _chunkSubscribers[chunkCoord].Add(player._clientId);
                }
            }
        }

        foreach (var chunkCoord in _chunkSubscribers.Keys)
        {
            if (!chunksToLoad.Contains(chunkCoord))
            {
                lock (_chunkSubscribers[chunkCoord])
                {
                    if (_chunkSubscribers[chunkCoord].Contains(player._clientId))
                    {
                        _chunkSubscribers[chunkCoord].Remove(player._clientId);
                        chunksToUnload.Add(chunkCoord);
                    }
                }
            }
        }

        foreach (var chunk in chunksToLoad)
        {
            LoadChunkForPlayer(player, chunk.x, chunk.z);
        }

        foreach (var chunk in chunksToUnload)
        {
            UnloadChunkForPlayer(player, chunk.x, chunk.z);
        }
    }

    public bool SetBlock(int x, int y, int z, byte blockType, byte metadata = 0)
    {
        if (y < 0 || y >= CHUNK_SIZE_Y)
        {
            return false;
        }

        int chunkX = x >> 4;
        int chunkZ = z >> 4;
        int relativeX = x & 0xF;
        int relativeY = y;
        int relativeZ = z & 0xF;

        var chunkCoord = (chunkX, chunkZ);
        byte[] chunkData = _blockData.GetOrAdd(chunkCoord, _ => GenerateChunkData(chunkX, chunkZ));

        int blockIndex = GetBlockIndex(relativeX, relativeY, relativeZ);
        int metadataIndex = BLOCKS_PER_CHUNK + (blockIndex >> 1);

        chunkData[blockIndex] = blockType;

        byte currentMetadata = chunkData[metadataIndex];
        if ((blockIndex & 1) == 0)
        {
            chunkData[metadataIndex] = (byte)((currentMetadata & 0xF0) | (metadata & 0x0F));
        }
        else
        {
            chunkData[metadataIndex] = (byte)((currentMetadata & 0x0F) | ((metadata << 4) & 0xF0));
        }

        NotifyBlockChange(x, y, z, blockType, metadata);
        return true;
    }

    public (byte blockType, byte metadata) GetBlock(int x, int y, int z)
    {
        if (y < 0 || y >= CHUNK_SIZE_Y)
        {
            return (0, 0);
        }

        int chunkX = x >> 4;
        int chunkZ = z >> 4;
        int relativeX = x & 0xF;
        int relativeY = y;
        int relativeZ = z & 0xF;

        var chunkCoord = (chunkX, chunkZ);
        if (!_blockData.TryGetValue(chunkCoord, out byte[] chunkData))
        {
            return (0, 0);
        }

        int blockIndex = GetBlockIndex(relativeX, relativeY, relativeZ);
        int metadataIndex = BLOCKS_PER_CHUNK + (blockIndex >> 1);

        byte blockType = chunkData[blockIndex];
        byte metadata = chunkData[metadataIndex];
        
        if ((blockIndex & 1) == 0)
        {
            metadata &= 0x0F;
        }
        else
        {
            metadata = (byte)((metadata >> 4) & 0x0F);
        }

        return (blockType, metadata);
    }

    public void LoadChunkForPlayer(ClientHandler player, int x, int z)
    {
        try
        {
            player.SendPacket(new PreChunkPacket(x, z, true));

            var chunkData = GetOrGenerateChunkData(x, z);
            var compressedData = ChunkGenerator.Compress(chunkData);
            var mapChunk = new MapChunkPacket(x, 0, z, compressedData);
            player.SendPacket(mapChunk);

            _lastAccessTime.AddOrUpdate((x, z), DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error loading chunk ({x}, {z}) for player: {e.Message}");
        }
    }

    private void UnloadChunkForPlayer(ClientHandler player, int x, int z)
    {
        try
        {
            player.SendPacket(new PreChunkPacket(x, z, false));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error unloading chunk ({x}, {z}) for player: {e.Message}");
        }
    }

    private void NotifyBlockChange(int x, int y, int z, byte blockType, byte metadata)
    {
        int chunkX = x >> 4;
        int chunkZ = z >> 4;

        if (_chunkSubscribers.TryGetValue((chunkX, chunkZ), out var subscribers))
        {
            var blockChangePacket = new BlockChangePacket
            {
                X = x,
                Y = (byte)y,
                Z = z,
                BlockType = blockType,
                BlockMetadata = metadata
            };

            lock (subscribers)
            {
                foreach (var playerId in subscribers)
                {
                    if (MinecraftServer._connectedClients.TryGetValue(playerId, out var player))
                    {
                        player.SendPacket(blockChangePacket);
                    }
                }
            }
        }
    }

    private byte[] GetOrGenerateChunkData(int x, int z)
    {
        return _blockData.GetOrAdd((x, z), coordinates => GenerateChunkData(coordinates.x, coordinates.z));
    }

    private byte[] GenerateChunkData(int x, int z)
    {
        var generatedData = _chunkGenerator.GenerateFlatChunk();
        var chunkData = new byte[TOTAL_CHUNK_SIZE];
        Buffer.BlockCopy(generatedData, 0, chunkData, 0, generatedData.Length);
        return chunkData;
    }

    private int GetBlockIndex(int x, int y, int z)
    {
        return y + (z * CHUNK_SIZE_Y) + (x * CHUNK_SIZE_Y * CHUNK_SIZE_Z);
    }

    private async Task ChunkCleanupLoop()
    {
        while (true)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var chunksToCheck = _lastAccessTime.ToList();

                foreach (var chunk in chunksToCheck)
                {
                    if ((currentTime - chunk.Value).TotalSeconds > UNLOAD_DELAY)
                    {
                        var chunkCoord = chunk.Key;
                        if (_chunkSubscribers.TryGetValue(chunkCoord, out var subscribers))
                        {
                            lock (subscribers)
                            {
                                if (subscribers.Count == 0)
                                {
                                    _blockData.TryRemove(chunkCoord, out _);
                                    _lastAccessTime.TryRemove(chunkCoord, out _);
                                    _chunkSubscribers.TryRemove(chunkCoord, out _);
                                }
                            }
                        }
                    }
                }

                await Task.Delay(5000);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in chunk cleanup loop: {e.Message}");
                await Task.Delay(5000);
            }
        }
    }

    public void RemovePlayer(Guid playerId)
    {
        foreach (var subscribers in _chunkSubscribers.Values)
        {
            lock (subscribers)
            {
                subscribers.Remove(playerId);
            }
        }
    }
}