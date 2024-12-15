namespace MinecraftServer;
using System.Net.Sockets;

public class ClientHandler
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly Action<Guid> _removeClientCallback;
    private readonly Guid _clientId;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private string _username;

    // Protocol positions
    private const double PLAYER_EYE_HEIGHT = 1.62;  // Standard eye height in Minecraft
    private const double DEFAULT_Y = 64.0;  // Default spawn height

    public double _x = 0;
    private double _y = DEFAULT_Y;  // Starting Y position
    public double _z = 0;
    private double _stance = DEFAULT_Y + PLAYER_EYE_HEIGHT;  // Must be Y + eye height (1.62)
    private float _yaw = 0;
    private float _pitch = 0;
    private bool _onGround = true;

    private int _entityId;

    // Constants for validation
    private const double ILLEGAL_STANCE_MIN = 0.1;
    private const double ILLEGAL_STANCE_MAX = 1.65;
    private const double MAX_MOVE_DISTANCE = 100;
    private const double MAX_POSITION = 3.2E7;
    private const double MIN_Y = 0;
    private const double MAX_Y = 256;
    private const int PROTOCOL_VERSION = 14;

    private long _lastKeepAlive;
    private bool _hasLoggedIn = false;
    private bool _initialPositionSent = false;
    private bool _handshakeReceived = false;

    public ClientHandler(TcpClient client, Action<Guid> removeCallback)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new BinaryReader(_stream);
        _writer = new BinaryWriter(_stream);
        _removeClientCallback = removeCallback;
        _clientId = Guid.NewGuid();
        _cancellationTokenSource = new CancellationTokenSource();
        _lastKeepAlive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _entityId = MinecraftServer._connectedClients.Count + 1;
    }

    public void StartHandling()
    {
        Task.Run(HandleClientAsync);
        Task.Run(KeepAliveLoopAsync);
    }

    private async Task KeepAliveLoopAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                SendPacket(new KeepAlivePacket());
                await Task.Delay(1000);

                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _lastKeepAlive > 60)
                {
                    Disconnect("Connection timed out");
                    return;
                }
            }
            catch
            {
                Disconnect();
                return;
            }
        }
    }
    
    public void SpawnOtherPlayerAsNamedEntity(int entityId, string playerName, int x, int y, int z, byte yaw, byte pitch, short currentItem)
    {
        // Ensure the entity ID and username are not the client's own
        if (entityId == _entityId || playerName == _username)
        {
            Console.WriteLine($"Skipping spawn for own EID or username: {playerName} (EID: {entityId})");
            return;
        }

        // Create the Named Entity Spawn packet
        var namedEntitySpawnPacket = new NamedEntitySpawnPacket
        {
            EntityId = entityId,
            PlayerName = playerName,
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch,
            CurrentItem = currentItem
        };

        // Send the packet to the client
        SendPacket(namedEntitySpawnPacket);
    }

    private async Task HandleClientAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                PacketType packetType = (PacketType)_reader.ReadByte();
                Packet packet = CreatePacketFromType(packetType);
                packet.Read(_reader);

                _lastKeepAlive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                switch (packet)
                {
                    case HandshakePacket handshakePacket:
                        HandleHandshake(handshakePacket);
                        break;

                    case LoginRequestPacket loginPacket:
                        HandleLoginRequest(loginPacket);
                        break;

                    case PlayerPositionAndLookPacket posLookPacket:
                        HandlePlayerPositionAndLook(posLookPacket);
                        break;

                    case PlayerPositionPacket posPacket:
                        HandlePlayerPosition(posPacket);
                        break;

                    case PlayerLookPacket lookPacket:
                        HandlePlayerLook(lookPacket);
                        break;

                    case PlayerFlyingPacket flyingPacket:
                        HandlePlayerFlying(flyingPacket);
                        break;

                    case ChatMessagePacket chatPacket:
                        HandleChatMessage(chatPacket);
                        break;

                    default:
                        Console.WriteLine($"Received unknown packet type: {packetType}");
                        break;
                }

                Console.WriteLine($"Received {packetType} packet from {_username}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Client handling error: {e.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    private void HandleHandshake(HandshakePacket handshakePacket)
    {
        if (handshakePacket.Value.Length > 16)
        {
            Disconnect("Username too long");
            return;
        }
        _username = handshakePacket.Value;
        _handshakeReceived = true;
        SendPacket(new HandshakePacket("-")); // Offline mode
    }

    private void HandleLoginRequest(LoginRequestPacket loginPacket)
    {
        if (!_handshakeReceived)
        {
            Disconnect("No handshake received");
            return;
        }

        if (_hasLoggedIn)
        {
            Disconnect("Already logged in!");
            return;
        }

        if (loginPacket.ProtocolVersion != PROTOCOL_VERSION)
        {
            Disconnect($"Outdated client! Please use Beta 1.7.3");
            return;
        }

        try
        {
            Console.WriteLine($"Processing login for {_username}");

            // Initialize with safe default values
            _x = 0.0;
            _y = DEFAULT_Y;
            _stance = _y + PLAYER_EYE_HEIGHT;
            _z = 0.0;
            _yaw = 0.0f;
            _pitch = 0.0f;

            var loginResponse = new LoginRequestPacket
            {
                EntityID = _entityId,
                MapSeed = 0,
                Dimension = 0
            };
            SendPacket(loginResponse);
            SendPacket(new SpawnPositionPacket((int)_x, (int)_y, (int)_z));
            //MinecraftServer.chunkManager.LoadChunksAroundPlayer(this, 5);
            MinecraftServer.chunkManager.SendAllChunksToPlayer(this);
            // Send spawn position

            // Send initial time and health
            SendPacket(new TimeUpdatePacket(0));
            SendPacket(new UpdateHealthPacket(20));

            foreach (var client in MinecraftServer._connectedClients.Values)
            {
                if (client._clientId != _clientId) // Skip the current client
                {
                    // Check if the other player is within visible range
                    if (IsWithinVisibleRange(client._x, client._z, _x, _z))
                    {
                        client.SpawnOtherPlayerAsNamedEntity(
                            _entityId, // Entity ID of the new player
                            _username, // Username of the new player
                            (int)_x,   // X position
                            (int)_y,   // Y position
                            (int)_z,   // Z position
                            0,         // Yaw (rotation)
                            0,         // Pitch (rotation)
                            0          // Current item (0 for no item)
                        );
                    }
                }
            }

            // Also, spawn all other players for the new player
            foreach (var client in MinecraftServer._connectedClients.Values)
            {
                if (client._clientId != _clientId) // Skip the current client
                {
                    // Check if the new player is within visible range of the other player
                    if (IsWithinVisibleRange(_x, _z, client._x, client._z))
                    {
                        SpawnOtherPlayerAsNamedEntity(
                            client._entityId, // Entity ID of the other player
                            client._username, // Username of the other player
                            (int)client._x,   // X position
                            (int)client._y,   // Y position
                            (int)client._z,   // Z position
                            0,                // Yaw (rotation)
                            0,                // Pitch (rotation)
                            0                 // Current item (0 for no item)
                        );
                    }
                }
            }

            
            // Send initial position - THIS IS THE CRITICAL ADDITION
            SendPacket(new PlayerPositionAndLookPacket(
                _x,        // x
                _stance,   // stance (must come before y for server->client packets)
                _y,        // y
                _z,        // z
                _yaw,      // yaw
                _pitch,    // pitch
                true       // onGround
            ));

            _initialPositionSent = true;
            _hasLoggedIn = true;
            Console.WriteLine($"Login completed for {_username}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error during login: {e.Message}");
            Disconnect("Login failed");
        }
    }

    private bool IsWithinVisibleRange(double x1, double z1, double x2, double z2)
    {
        // Define the visible range (e.g., 16 blocks)
        const double visibleRange = 16.0 * 12;

        // Calculate the distance between the two players
        double distance = Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(z1 - z2, 2));

        // Return true if the distance is within the visible range
        return distance <= visibleRange;
    }

    private void HandlePlayerPositionAndLook(PlayerPositionAndLookPacket posLookPacket)
    {
        // Validate the position
        if (!ValidatePosition(posLookPacket.X, posLookPacket.Y, posLookPacket.Z, posLookPacket.Stance))
        {
            return;
        }

        // Update the client's position
        UpdatePlayerPosition(posLookPacket.X, posLookPacket.Y, posLookPacket.Z, posLookPacket.Stance, true);
        this._yaw = posLookPacket.Yaw;
        this._pitch = posLookPacket.Pitch;

        // Check if the player has moved out of visible range of any other player
        foreach (var client in MinecraftServer._connectedClients.Values)
        {
            if (client._clientId != _clientId) // Skip the current client
            {
                if (!IsWithinVisibleRange(_x, _z, client._x, client._z))
                {
                    // Send a Destroy Entity packet to the other player
                    client.SendPacket(new DestroyEntityPacket
                    {
                        EntityId = _entityId // Entity ID of the moving player
                    });
                }
            }
        }
    }
    private void HandlePlayerPosition(PlayerPositionPacket posPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;

        if (!ValidatePosition(posPacket.X, posPacket.Y, posPacket.Z, posPacket.Stance))
        {
            return;
        }

        UpdatePlayerPosition(posPacket.X, posPacket.Y, posPacket.Z, posPacket.Stance, posPacket.OnGround);

        // Load chunks around the player
       // MinecraftServer.chunkManager.LoadChunksAroundPlayer(this);
    }
    private void HandlePlayerLook(PlayerLookPacket lookPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;
        
        this._yaw = lookPacket.yaw;
        this._pitch = lookPacket.pitch;
        this._onGround = lookPacket.onGround;
    }

    private void HandlePlayerFlying(PlayerFlyingPacket flyingPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;
        
        this._onGround = flyingPacket.onGround;
    }

    private void HandleChatMessage(ChatMessagePacket chatPacket)
    {
        if (!_hasLoggedIn) return;
        
        if (chatPacket.message.Length > 119)
        {
            Disconnect("Chat message too long");
            return;
        }

        var broadcastMessage = new ChatMessagePacket($"<{_username}> {chatPacket.message}");
        MinecraftServer.BroadcastPacket(broadcastMessage);
    }

private bool ValidatePosition(double x, double y, double z, double stance)
{
    Console.WriteLine($"Validating position for {_username}: Y={y:F2}, Stance={stance:F2}, StanceHeight={stance - y:F2}");

    if (!_hasLoggedIn || !_initialPositionSent)
    {
        Console.WriteLine("Ignoring position validation - login sequence not complete");
        return false;
    }

    // Check for NaN or infinity values
    if (double.IsInfinity(x) || double.IsInfinity(y) ||
        double.IsInfinity(z) || double.IsInfinity(stance) ||
        double.IsNaN(x) || double.IsNaN(y) ||
        double.IsNaN(z) || double.IsNaN(stance))
    {
        Console.WriteLine("Invalid position values detected (NaN or Infinity)");
        Disconnect("Invalid position");
        return false;
    }

    // Check for illegal stance
    double stanceHeight = stance - y;
    Console.WriteLine($"Validating position for {_username} - Y: {y:F2}, Stance: {stance:F2}, Height: {stanceHeight:F2}");

    if (stanceHeight < ILLEGAL_STANCE_MIN || stanceHeight > ILLEGAL_STANCE_MAX)
    {
        Console.WriteLine($"Illegal stance detected for {_username}! Height: {stanceHeight:F2} (Stance: {stance:F2}, Y: {y:F2})");
        Disconnect("Illegal stance");
        return false;
    }

    // Check for moving too quickly
    double moveDistance = Math.Sqrt(
        Math.Pow(_x - x, 2) +
        Math.Pow(_y - y, 2) +
        Math.Pow(_z - z, 2)
    );

    if (moveDistance > MAX_MOVE_DISTANCE)
    {
        Console.WriteLine($"Too quick movement detected for {_username}! Distance: {moveDistance:F2}");
        Disconnect("You moved too quickly :( (Hacking?)");
        return false;
    }

    // Check for illegal position
    if (Math.Abs(x) > MAX_POSITION || Math.Abs(z) > MAX_POSITION)
    {
        Disconnect("Illegal position");
        return false;
    }

    if (y < MIN_Y || y > MAX_Y)
    {
        Disconnect("Illegal position");
        return false;
    }

    // Check if the player is in a loaded chunk
    int chunkX = (int)x >> 4;
    int chunkZ = (int)z >> 4;

    if (!MinecraftServer.chunkManager.loadedChunks.ContainsKey((chunkX, chunkZ)))
    {
        Console.WriteLine($"Player {_username} is outside loaded chunks. Teleporting to spawn.");
        TeleportToSpawn();
        return false;
    }

    return true;
}

private void TeleportToSpawn()
{
    // Teleport the player to the spawn position
    _x = 0;
    _y = DEFAULT_Y;
    _z = 0;
    _stance = _y + PLAYER_EYE_HEIGHT;
    _yaw = 0;
    _pitch = 0;

    var teleportPacket = new PlayerPositionAndLookPacket(
        _x,
        _stance,
        _y,
        _z,
        _yaw,
        _pitch,
        true
    );
    SendPacket(teleportPacket);

    Console.WriteLine($"Teleported {_username} to spawn.");
}

    private void UpdatePlayerPosition(double x, double y, double z, double stance, bool updateOnGround = true)
    {
        Console.WriteLine($"Updating position - Old Y: {_y:F2}, Old Stance: {_stance:F2}, Old Height: {_stance - _y:F2}");
        Console.WriteLine($"Updating position - New Y: {y:F2}, New Stance: {stance:F2}, New Height: {stance - y:F2}");
        
        this._x = x;
        this._y = y;
        this._stance = stance;
        this._z = z;

        if (updateOnGround)
        {
            this._onGround = y <= MIN_Y;
        }
    }

    public void SendPacket(Packet packet)
    {
        try
        {
            packet.Write(_writer);
            _writer.Flush();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending packet: {e.Message}");
        }
    }

    public void Disconnect(string reason = "Disconnected")
    {
        try
        {
            SendPacket(new DisconnectPacket(reason));
        }
        catch
        {
            // Ignore send errors during disconnect
        }
        finally
        {
            _cancellationTokenSource.Cancel();
            _client?.Close();
            _removeClientCallback(_clientId);
        }
    }

    private Packet CreatePacketFromType(PacketType type)
    {
        return type switch
        {
            PacketType.KEEPALIVE => new KeepAlivePacket(),
            PacketType.LOGIN_REQUEST => new LoginRequestPacket(),
            PacketType.HANDSHAKE => new HandshakePacket(),
            PacketType.CHAT_MESSAGE => new ChatMessagePacket(),
            PacketType.TIME_UPDATE => new TimeUpdatePacket(),
            PacketType.SPAWN_POSITION => new SpawnPositionPacket(),
            PacketType.UPDATE_HEALTH => new UpdateHealthPacket(),
            PacketType.PLAYER => new PlayerFlyingPacket(),
            PacketType.PLAYER_POSITION => new PlayerPositionPacket(),
            PacketType.PLAYER_LOOK => new PlayerLookPacket(),
            PacketType.PLAYER_POSITION_LOOK => new PlayerPositionAndLookPacket(),
            PacketType.DISCONNECT => new DisconnectPacket(),
            _ => throw new NotSupportedException($"Unsupported packet type: {type}")
        };
    }
}