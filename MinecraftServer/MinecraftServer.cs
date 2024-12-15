namespace MinecraftServer;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class MinecraftServer
{
    private TcpListener _tcpListener;
    private CancellationTokenSource _cancellationTokenSource;
    public static ConcurrentDictionary<Guid, ClientHandler> _connectedClients;
    public static ChunkManager chunkManager = new ChunkManager();
    public int Port { get; private set; }
    public int MaxClients { get; private set; }

    public MinecraftServer(int port, int maxClients = 20)
    {
        Port = port;
        MaxClients = maxClients;
        _connectedClients = new ConcurrentDictionary<Guid, ClientHandler>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Start()
    {
        _tcpListener = new TcpListener(IPAddress.Any, Port);
        _tcpListener.Start();
        chunkManager.LoadSpawnChunks(0, 0);
        Console.WriteLine($"Server started on port {Port}");

        Task.Run(ListenForClientsAsync);
    }

    private async Task ListenForClientsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                
                if (_connectedClients.Count >= MaxClients)
                {
                    SendDisconnectPacket(tcpClient, "Server is full!");
                    continue;
                }

                var clientHandler = new ClientHandler(tcpClient, RemoveClient);
                _connectedClients[Guid.NewGuid()] = clientHandler;
                clientHandler.StartHandling();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Client accept error: {e.Message}");
            }
        }
    }

    private void RemoveClient(Guid clientId)
    {
        _connectedClients.TryRemove(clientId, out _);
    }

    private void SendDisconnectPacket(TcpClient client, string reason)
    {
        try
        {
            using (var stream = client.GetStream())
            using (var writer = new BinaryWriter(stream))
            {
                var disconnectPacket = new DisconnectPacket(reason);
                disconnectPacket.Write(writer);
            }
            client.Close();
        }
        catch { }
    }

    public static void BroadcastPacket(Packet packet)
    {
        foreach (var client in _connectedClients.Values)
        {
            client.SendPacket(packet);
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _tcpListener.Stop();
        
        foreach (var client in _connectedClients.Values)
        {
            client.DisconnectAsync().Start();
        }
        
        _connectedClients.Clear();
        Console.WriteLine("Server stopped.");
    }
}

