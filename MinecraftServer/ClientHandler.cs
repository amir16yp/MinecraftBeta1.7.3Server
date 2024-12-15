using System.Net.Sockets;

namespace MinecraftServer;

public class ClientHandler : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly Action<Guid> _removeClientCallback;
    public readonly Guid _clientId;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private string _username;
    private bool _disposed = false;
    private static World _world;  // Add static World reference

    // Protocol positions
    private const double PLAYER_EYE_HEIGHT = 1.62;
    private const double DEFAULT_Y = 64.0;

    // Player position and movement
    private double _x = 0;
    private double _y = DEFAULT_Y;
    private double _z = 0;
    private double _stance = DEFAULT_Y + PLAYER_EYE_HEIGHT;
    private float _yaw = 0;
    private float _pitch = 0;
    private bool _onGround = true;
    private short hotbarSlotID;

    // Last known positions for movement broadcasting
    private double _lastBroadcastX;
    private double _lastBroadcastY;
    private double _lastBroadcastZ;
    private double _lastKnownX;
    private double _lastKnownZ;
    private bool _hasRotated;

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
    private bool _isDisconnecting = false;
    private readonly SemaphoreSlim _disconnectSemaphore = new SemaphoreSlim(1, 1);

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
        
        _world ??= new World();

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
                await Task.Delay(1000, _cancellationTokenSource.Token);

                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _lastKeepAlive > 60)
                {
                    await DisconnectAsync("Connection timed out");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in keep alive loop: {e.Message}");
                await DisconnectAsync();
                return;
            }
        }
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

                if (!_isDisconnecting)
                {
                    switch (packet)
                    {
                        case DisconnectPacket disconnectPacket:
                            Console.WriteLine($"Received disconnect packet from {_username}");
                            await DisconnectAsync("Client disconnected");
                            break;
                        case HandshakePacket handshakePacket:
                            HandleHandshake(handshakePacket);
                            break;

                        case LoginRequestPacket loginPacket:
                            await HandleLoginRequestAsync(loginPacket);
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

                        case HoldingChangePacket holdingChangePacket:
                            hotbarSlotID = holdingChangePacket.SlotID;
                            break;

                        default:
                            Console.WriteLine($"Received unknown packet type: {packetType}");
                            break;
                    }

                    Console.WriteLine($"Received {packetType} packet from {_username}");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Client handling error: {e.Message}");
        }
        finally
        {
            await DisconnectAsync();
        }
    }

    private void HandleHandshake(HandshakePacket handshakePacket)
    {
        if (handshakePacket.Value.Length > 16)
        {
            DisconnectAsync("Username too long").Wait();
            return;
        }

        _username = handshakePacket.Value;
        _handshakeReceived = true;
        SendPacket(new HandshakePacket("-")); // Offline mode
    }

    private async Task HandleLoginRequestAsync(LoginRequestPacket loginPacket)
    {
        if (!_handshakeReceived)
        {
            await DisconnectAsync("No handshake received");
            return;
        }

        if (_hasLoggedIn)
        {
            await DisconnectAsync("Already logged in!");
            return;
        }

        if (loginPacket.ProtocolVersion != PROTOCOL_VERSION)
        {
            await DisconnectAsync($"Outdated client! Please use Beta 1.7.3");
            return;
        }

        try
        {
            Console.WriteLine($"Processing login for {_username}");
            
            // Verify the client hasn't disconnected during cleanup
            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Console.WriteLine($"Client disconnected during login cleanup: {_username}");
                return;
            }

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

            _world.UpdatePlayerPosition(this, _x, _z);

            // Verify client is still connected after chunk sending
            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Console.WriteLine($"Client disconnected during chunk loading: {_username}");
                return;
            }

            SendPacket(new TimeUpdatePacket(0));
            SendPacket(new UpdateHealthPacket(20));

            try
            {
                SpawnExistingPlayers();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error spawning existing players for {_username}: {e.Message}");
            }

            SendPacket(new PlayerPositionAndLookPacket(
                _x,
                _stance,
                _y,
                _z,
                _yaw,
                _pitch,
                true
            ));

            _initialPositionSent = true;
            _hasLoggedIn = true;

            // Initialize last known positions
            _lastBroadcastX = _x;
            _lastBroadcastY = _y;
            _lastBroadcastZ = _z;
            _lastKnownX = _x;
            _lastKnownZ = _z;

            Console.WriteLine($"Login completed successfully for {_username}");

            try
            {
                MinecraftServer.BroadcastPacket(new ChatMessagePacket($"{_username} joined the game"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error broadcasting join message for {_username}: {e.Message}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error during login process for {_username}: {e.Message}");
            await DisconnectAsync("Login failed - Please try again");
        }
    }

    public async Task DisconnectAsync(string reason = "Disconnected")
    {
        if (!await _disconnectSemaphore.WaitAsync(0))
        {
            Console.WriteLine($"Already disconnecting {_username}, skipping duplicate disconnect");
            return;
        }

        try
        {
            if (_isDisconnecting)
            {
                return;
            }

            _isDisconnecting = true;
            Console.WriteLine($"Starting disconnect for {_username} with reason: {reason}");

            // First despawn for other players
            foreach (var client in MinecraftServer._connectedClients.Values)
            {
                if (client._clientId != _clientId && !client._isDisconnecting)
                {
                    try
                    {
                        client.SendPacket(new DestroyEntityPacket { EntityId = _entityId });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error sending destroy packet during disconnect: {e.Message}");
                    }
                }
            }
            
            // Remove from server's client list before closing connection
            try
            {
                _removeClientCallback(_clientId);
                Console.WriteLine($"Successfully removed client {_username} ({_clientId})");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in remove callback: {e.Message}");
            }

            // Then notify the client
            try
            {
                if (_client?.Connected == true)
                {
                    SendPacket(new DisconnectPacket(reason));
                    await Task.Delay(100); // Give time for packet to be sent
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error sending disconnect packet: {e.Message}");
            }

            // Cancel ongoing tasks
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error cancelling tasks: {e.Message}");
            }

            // Finally close connection
            try
            {
                _client?.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error closing client connection: {e.Message}");
            }

            // Broadcast disconnect message if logged in
            if (_hasLoggedIn)
            {
                try
                {
                    MinecraftServer.BroadcastPacket(new ChatMessagePacket($"{_username} left the game"));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error broadcasting disconnect message: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error during disconnect for {_username}: {e.Message}");
        }
        finally
        {
            _disconnectSemaphore.Release();
        }
    }

    private void SpawnExistingPlayers()
    {
        foreach (var client in MinecraftServer._connectedClients.Values)
        {
            if (client._clientId != _clientId && !client._isDisconnecting)
            {
                if (IsWithinVisibleRange(_x, _z, client._x, client._z))
                {
                    SpawnPlayersForEachOther(client);
                }
            }
        }
    }

    private bool IsWithinVisibleRange(double x1, double z1, double x2, double z2)
    {
        const double visibleRange = 16.0 * 12; // 192 blocks viewing distance
        double distance = Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(z1 - z2, 2));
        return distance <= visibleRange;
    }

    private void HandlePlayerPositionAndLook(PlayerPositionAndLookPacket posLookPacket)
    {
        if (!ValidatePosition(posLookPacket.X, posLookPacket.Y, posLookPacket.Z, posLookPacket.Stance))
        {
            return;
        }

        UpdatePlayerPosition(posLookPacket.X, posLookPacket.Y, posLookPacket.Z, posLookPacket.Stance, true);
        _world.UpdatePlayerPosition(this, posLookPacket.X, posLookPacket.Z);
        
        this._yaw = posLookPacket.Yaw;
        this._pitch = posLookPacket.Pitch;
        this._hasRotated = true;

        BroadcastPlayerPosition();
    }
    private void HandlePlayerPosition(PlayerPositionPacket posPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;

        if (!ValidatePosition(posPacket.X, posPacket.Y, posPacket.Z, posPacket.Stance))
        {
            return;
        }

        UpdatePlayerPosition(posPacket.X, posPacket.Y, posPacket.Z, posPacket.Stance, posPacket.OnGround);
        _world.UpdatePlayerPosition(this, posPacket.X, posPacket.Z);
        
        BroadcastPlayerPosition();
    }
    
    private void HandlePlayerLook(PlayerLookPacket lookPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;
        
        this._yaw = lookPacket.yaw;
        this._pitch = lookPacket.pitch;
        this._onGround = lookPacket.onGround;
        this._hasRotated = true;

        BroadcastPlayerPosition();
    }

    private void HandlePlayerFlying(PlayerFlyingPacket flyingPacket)
    {
        if (!_hasLoggedIn || !_initialPositionSent) return;
        
        this._onGround = flyingPacket.onGround;
    }

    private void HandleChatMessage(ChatMessagePacket chatPacket)
    {
        if (!_hasLoggedIn || _isDisconnecting) return;
        
        if (chatPacket.message.Length > 119)
        {
            DisconnectAsync("Chat message too long").Wait();
            return;
        }

        var broadcastMessage = new ChatMessagePacket($"<{_username}> {chatPacket.message}");
        MinecraftServer.BroadcastPacket(broadcastMessage);
    }

    private void BroadcastPlayerPosition()
    {
        if (_isDisconnecting) return;

        // Get the current absolute positions for packet creation
        int absoluteX = (int)(_x * 32);
        int absoluteY = (int)(_y * 32);
        int absoluteZ = (int)(_z * 32);
        byte rotationYaw = (byte)(_yaw * 256 / 360);
        byte rotationPitch = (byte)(_pitch * 256 / 360);

        foreach (var client in MinecraftServer._connectedClients.Values)
        {
            if (client._clientId == _clientId || client._isDisconnecting) continue;

            bool wasInRange = IsWithinVisibleRange(client._lastKnownX, client._lastKnownZ, client._x, client._z);
            bool isInRange = IsWithinVisibleRange(_x, _z, client._x, client._z);

            if (!wasInRange && isInRange)
            {
                // Player just came into view - spawn them for each other
                SpawnPlayersForEachOther(client);
            }
            else if (wasInRange && !isInRange)
            {
                // Player just went out of view - despawn them for each other
                DespawnPlayersForEachOther(client);
            }
            else if (isInRange)
            {
                // Player is in range - send movement update
                double distance = Math.Sqrt(
                    Math.Pow(_x - _lastBroadcastX, 2) +
                    Math.Pow(_y - _lastBroadcastY, 2) +
                    Math.Pow(_z - _lastBroadcastZ, 2)
                );

                if (distance > 4 || _hasRotated)
                {
                    // Use teleport for large movements or when player has rotated
                    client.SendPacket(new EntityTeleportPacket
                    {
                        EntityId = _entityId,
                        X = absoluteX,
                        Y = absoluteY,
                        Z = absoluteZ,
                        Yaw = rotationYaw,
                        Pitch = rotationPitch
                    });
                }
                else
                {
                    // Calculate relative movement (convert to byte range)
                    int deltaX = absoluteX - (int)(_lastBroadcastX * 32);
                    int deltaY = absoluteY - (int)(_lastBroadcastY * 32);
                    int deltaZ = absoluteZ - (int)(_lastBroadcastZ * 32);

                    // Check if deltas fit in byte range
                    if (deltaX >= -128 && deltaX <= 127 &&
                        deltaY >= -128 && deltaY <= 127 &&
                        deltaZ >= -128 && deltaZ <= 127)
                    {
                        client.SendPacket(new EntityRelativeMovePacket
                        {
                            EntityId = _entityId,
                            DeltaX = (sbyte)deltaX,
                            DeltaY = (sbyte)deltaY,
                            DeltaZ = (sbyte)deltaZ
                        });
                    }
                    else
                    {
                        // Fallback to teleport if deltas are too large
                        client.SendPacket(new EntityTeleportPacket
                        {
                            EntityId = _entityId,
                            X = absoluteX,
                            Y = absoluteY,
                            Z = absoluteZ,
                            Yaw = rotationYaw,
                            Pitch = rotationPitch
                        });
                    }
                }
            }
        }

        // Update last broadcast position
        _lastBroadcastX = _x;
        _lastBroadcastY = _y;
        _lastBroadcastZ = _z;
        _hasRotated = false;

        // Update last known position for range calculations
        _lastKnownX = _x;
        _lastKnownZ = _z;
    }

    private void SpawnPlayersForEachOther(ClientHandler otherClient)
    {
        if (_isDisconnecting || otherClient._isDisconnecting) return;

        // Spawn other player for this client
        SendPacket(new NamedEntitySpawnPacket
        {
            EntityId = otherClient._entityId,
            PlayerName = otherClient._username,
            X = (int)(otherClient._x * 32),
            Y = (int)(otherClient._y * 32),
            Z = (int)(otherClient._z * 32),
            Yaw = (byte)(otherClient._yaw * 256 / 360),
            Pitch = (byte)(otherClient._pitch * 256 / 360),
            CurrentItem = otherClient.hotbarSlotID
        });

        // Spawn this client for other player
        otherClient.SendPacket(new NamedEntitySpawnPacket
        {
            EntityId = _entityId,
            PlayerName = _username,
            X = (int)(_x * 32),
            Y = (int)(_y * 32),
            Z = (int)(_z * 32),
            Yaw = (byte)(_yaw * 256 / 360),
            Pitch = (byte)(_pitch * 256 / 360),
            CurrentItem = hotbarSlotID
        });
    }

    private void DespawnPlayersForEachOther(ClientHandler otherClient)
    {
        if (_isDisconnecting || otherClient._isDisconnecting) return;

        // Despawn other player for this client
        SendPacket(new DestroyEntityPacket
        {
            EntityId = otherClient._entityId
        });

        // Despawn this client for other player
        otherClient.SendPacket(new DestroyEntityPacket
        {
            EntityId = _entityId
        });
    }

    private bool ValidatePosition(double x, double y, double z, double stance)
    {
        if (_isDisconnecting) return false;

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
            DisconnectAsync("Invalid position").Wait();
            return false;
        }

        // Check for illegal stance
        double stanceHeight = stance - y;
        Console.WriteLine($"Validating position for {_username} - Y: {y:F2}, Stance: {stance:F2}, Height: {stanceHeight:F2}");

        if (stanceHeight < ILLEGAL_STANCE_MIN || stanceHeight > ILLEGAL_STANCE_MAX)
        {
            Console.WriteLine($"Illegal stance detected for {_username}! Height: {stanceHeight:F2} (Stance: {stance:F2}, Y: {y:F2})");
            DisconnectAsync("Illegal stance").Wait();
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
            DisconnectAsync("You moved too quickly :( (Hacking?)").Wait();
            return false;
        }

        // Check for illegal position
        if (Math.Abs(x) > MAX_POSITION || Math.Abs(z) > MAX_POSITION)
        {
            DisconnectAsync("Illegal position").Wait();
            return false;
        }

        if (y < MIN_Y || y > MAX_Y)
        {
            DisconnectAsync("Illegal position").Wait();
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
        if (_isDisconnecting) return;

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
        if (_isDisconnecting) return;

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
        if (_isDisconnecting) return;
        
        try
        {
            if (_client?.Connected == true)
            {
                packet.Write(_writer);
                _writer.Flush();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending packet: {e.Message}");
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
            PacketType.HOLDING_CHANGE => new HoldingChangePacket(),
            _ => throw new NotSupportedException($"Unsupported packet type: {type}")
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisconnectAsync().Wait();
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            _cancellationTokenSource?.Dispose();
            _disconnectSemaphore?.Dispose();
            _disposed = true;
        }
    }
}
