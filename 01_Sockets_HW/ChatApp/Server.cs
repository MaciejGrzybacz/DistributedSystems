using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatApp;

public class Server
{
    private const int WaitingMaxCount = 16;
    private readonly int _bufferSize = 1024;
    private readonly Encoding _encoder = Encoding.GetEncoding("UTF-8");
    private readonly object _lock = new();
    private readonly List<ClientData> _clients = new();
    private Socket _serverTcpSocket;
    private Socket _serverUdpSocket;
    private bool _isRunning = true;
    private string _ipAddress = "127.0.0.1";
    private int _port = 8888;

    public void Run()
    {
        try
        {
            InitSockets();

            Task.Factory.StartNew(HandleUdpCommunicationAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(CleanupDisconnectedClientsAsync, TaskCreationOptions.LongRunning);

            _serverTcpSocket.Listen(WaitingMaxCount);

            while (_isRunning)
            {
                var client = AcceptClient();
                _ = Task.Run(async () => await HandleTcpClientAsync(client));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
        finally
        {
            Shutdown();
        }
    }

    private void InitSockets()
    {
        _serverTcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _serverTcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        _serverUdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverUdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        var serverEndPoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);

        _serverTcpSocket.Bind(serverEndPoint);
        _serverUdpSocket.Bind(serverEndPoint);

        Console.WriteLine($"TCP endpoint: {_serverTcpSocket.LocalEndPoint}");
        Console.WriteLine($"UDP endpoint: {_serverUdpSocket.LocalEndPoint}");
    }

    private ClientData AcceptClient()
    {
        var clientSocket = _serverTcpSocket.Accept();
        var username = GetUsername(clientSocket);

        var client = new ClientData
        {
            TcpSocket = clientSocket,
            UdpEndpoint = clientSocket.RemoteEndPoint,
            Username = username,
            Disconnected = false
        };

        Console.WriteLine($"Connected client: {client.Username}");

        lock (_lock)
        {
            _clients.Add(client);
        }

        return client;
    }

    private void DisconnectClient(ClientData client)
    {
        if (client.Disconnected) return;

        try
        {
            lock (_lock)
            {
                var clientIndex = _clients.FindIndex(c => c.TcpSocket == client.TcpSocket);

                if (clientIndex >= 0)
                {
                    var updatedClient = _clients[clientIndex];
                    updatedClient.Disconnected = true;
                    _clients[clientIndex] = updatedClient;
                }
            }

            Console.WriteLine($"Disconnected client: {client.Username}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing client connection: {client.Username}");
        }
    }

    private void Shutdown()
    {
        _isRunning = false;

        lock (_lock)
        {
            _clients.ForEach(DisconnectClient);

            _clients.Clear();
        }

        try
        {
            _serverTcpSocket.Shutdown(SocketShutdown.Both);
            _serverTcpSocket.Close();
            _serverUdpSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing server sockets: {ex.Message}");
        }

        Console.WriteLine("Server shutdown complete.");
    }

    private async Task CleanupDisconnectedClientsAsync()
    {
        while (_isRunning)
        {
            await Task.Delay(30000);

            lock (_lock)
            {
                _clients.RemoveAll(c => c.Disconnected);
            }
        }
    }

    private string GetUsername(Socket client)
    {
        var username = "";

        try
        {
            var buffer = new byte[_bufferSize];

            var bytesRead = client.Receive(buffer);

            if (bytesRead > 0) username = _encoder.GetString(buffer, 0, bytesRead);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting username: {ex.Message}");
        }

        return username;
    }

    private async Task HandleTcpClientAsync(ClientData client)
    {
        try
        {
            while (_isRunning && !client.Disconnected)
            {
                var buffer = new byte[_bufferSize];

                var bytesRead = client.TcpSocket.Receive(buffer);

                if (bytesRead > 0)
                {
                    var message = $"{client.Username} sent {_encoder.GetString(buffer, 0, bytesRead)}";
                    BroadcastTcpMessage(message, client);
                }

                await Task.Delay(10);
            }
        }
        finally
        {
            DisconnectClient(client);
        }
    }

    private async Task HandleUdpCommunicationAsync()
    {
        EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            while (_isRunning)
            {
                var buffer = new byte[_bufferSize];

                var receivedSize = _serverUdpSocket.ReceiveFrom(buffer, ref sender);

                if (receivedSize > 0)
                {
                    var message = _encoder.GetString(buffer, 0, receivedSize);
                    BroadcastUdpMessage(message, sender);
                }

                await Task.Delay(10);
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _clients.ForEach(client =>
                {
                    if (client.UdpEndpoint == sender) DisconnectClient(client);
                });
            }
        }
    }

    private void BroadcastTcpMessage(string message, ClientData sender)
    {
        var messageBytes = _encoder.GetBytes(message);

        lock (_lock)
        {
            foreach (var client in _clients.Where(client => !Equals(client.TcpSocket, sender.TcpSocket))
                         .Where(client => !client.Disconnected))
                try
                {
                    client.TcpSocket.Send(messageBytes);
                }
                catch (Exception ex)
                {
                    DisconnectClient(client);
                }
        }
    }

    private void BroadcastUdpMessage(string message, EndPoint sender)
    {
        var broadcastBytes = _encoder.GetBytes(message);

        lock (_lock)
        {
            foreach (var client in _clients.Where(client => !client.UdpEndpoint.Equals(sender)))
            {
                try
                {
                    _serverUdpSocket.SendTo(broadcastBytes, client.UdpEndpoint);
                }
                catch (Exception ex)
                {
                    DisconnectClient(client);
                }
            }
        }
    }
}