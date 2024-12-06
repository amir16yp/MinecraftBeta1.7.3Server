using System.Diagnostics;
using System.Net.Sockets;

public class ClientHandler
{
    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;
    private Action<Guid> _removeClientCallback;
    private Guid _clientId;
    private CancellationTokenSource _cancellationTokenSource;
    private string username;
    private double x = 0;
    private double y = 80;
    private double z = 0;
    private double stance = 67.240000009536743;
    private float yaw =0;
    private float pitch =0;
    private bool onGround =true;
    private const double MIN_Y = 32;  // Minimum Y-coordinate (ground level)
    private const double MAX_Y = 256;  // Maximum world height
    private const double WORLD_BORDER_SIZE = 30000; // Example world border size
    
    public ClientHandler(TcpClient client, Action<Guid> removeCallback)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new BinaryReader(_stream);
        _writer = new BinaryWriter(_stream);
        _removeClientCallback = removeCallback;
        _clientId = Guid.NewGuid();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void StartHandling()
    {
        Task.Run(HandleClientAsync);
    }
    
    private void UpdatePlayerPosition(double x, double y, double z, double stance, bool updateOnGround = true)
    {
        // Clamp X and Z coordinates within world border
        this.x = Math.Clamp(x, -WORLD_BORDER_SIZE, WORLD_BORDER_SIZE);
        this.z = Math.Clamp(z, -WORLD_BORDER_SIZE, WORLD_BORDER_SIZE);

        // Ensure Y coordinate is not below ground or above world height
        this.y = Math.Clamp(y, MIN_Y, MAX_Y);
    
        // If stance is provided, clamp it as well
        this.stance = Math.Clamp(stance, MIN_Y, MAX_Y);

        // Explicitly set onGround to true if Y reaches minimum height
        this.onGround = this.y <= MIN_Y;

        // Optionally, send a correction packet if the position was adjusted
        if (x != this.x || y != this.y || z != this.z)
        {
            SendPacket(new PlayerPositionAndLookPacket(
                this.x, this.y, this.stance, this.z, this.yaw, this.pitch, this.onGround
            ));
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
                if (packet is HandshakePacket)
                {
                    HandshakePacket handshakePacket = (HandshakePacket)packet; 
                    this.username = handshakePacket.Value;
                    SendPacket(new HandshakePacket("-"));
                    Console.WriteLine(handshakePacket.Value);
                }

                if (packet is LoginRequestPacket)
                {
                    LoginRequestPacket loginRequestPacket = new();
                    loginRequestPacket.MapSeed = 0L;
                    loginRequestPacket.EntityID = MinecraftServer._connectedClients.Count;
                    loginRequestPacket.Dimension = 0;
                    SendPacket(loginRequestPacket);
                    SendPacket(new PlayerPositionPacket(0, 80, 0, 0, onGround));
                    MinecraftServer.chunkManager.SendAllChunksToPlayer(this);
                }
                
                if (packet is PlayerPositionAndLookPacket playerPositionPacket)
                {
                    UpdatePlayerPosition(
                        playerPositionPacket.X, 
                        playerPositionPacket.Y, 
                        playerPositionPacket.Z, 
                        playerPositionPacket.Stance, 
                        true
                    );
    
                    this.yaw = playerPositionPacket.Yaw;
                    this.pitch = playerPositionPacket.Pitch;
    
                    Console.WriteLine($"Player {username} position: {this.x} {this.y} {this.z} OnGround: {this.onGround}");
                }

                if (packet is PlayerPositionPacket positionPacket)
                {
                    UpdatePlayerPosition(
                        positionPacket.X, 
                        positionPacket.Y, 
                        positionPacket.Z, 
                        positionPacket.Stance, 
                        positionPacket.OnGround
                    );
    
                    Console.WriteLine($"Player {username} position: {this.x} {this.y} {this.z} OnGround: {this.onGround}");
                }

                if (packet is PlayerFlyingPacket)
                {
                    PlayerFlyingPacket playerFlyingPacket = (PlayerFlyingPacket)packet;
                    this.onGround = playerFlyingPacket.onGround;
                }
                //SendPacket(ne w DisconnectPacket("nigga link AI"));
                // Process packet logic would go here
                Console.WriteLine($"Received {packetType} packet");
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

    public void SendPacket(Packet packet)
    {
        try
        {
            packet.Write(_writer);
            _writer.Flush();
        }
        catch
        {
            Disconnect();
        }
    }

    public void Disconnect()
    {
        _cancellationTokenSource.Cancel();
        _client?.Close();
        _removeClientCallback(_clientId);
    }

    private Packet CreatePacketFromType(PacketType type)
    {
        return type switch
        {
            PacketType.DISCONNECT => new DisconnectPacket(),
            PacketType.HANDSHAKE => new HandshakePacket(),
            PacketType.LOGIN_REQUEST => new LoginRequestPacket(),
            PacketType.PLAYER_POSITION_LOOK => new PlayerPositionAndLookPacket(),
            PacketType.PLAYER_POSITION => new PlayerPositionPacket(),
            PacketType.KEEPALIVE => new KeepAlivePacket(),
            PacketType.PLAYER => new PlayerPositionPacket(),
            PacketType.PLAYER_LOOK => new PlayerLookPacket(),
            _ => throw new NotSupportedException($"Unsupported packet type: {type}")
        };
    }
}
