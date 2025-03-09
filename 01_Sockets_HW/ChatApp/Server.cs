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
    private readonly List<Socket> _tcpClients = new();
    private Socket _serverTcpSocket;
    private Socket _serverUdpSocket;

    public void Run()
    {
        var ipAddress = "127.0.0.1";
        var port = 8888;

        _serverTcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _serverTcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        _serverUdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverUdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        var serverEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        _serverTcpSocket.Bind(serverEndPoint);
        _serverUdpSocket.Bind(serverEndPoint);

        Console.WriteLine($"TCP endpoint: {_serverTcpSocket.LocalEndPoint}");
        Console.WriteLine($"UDP endpoint: {_serverUdpSocket.LocalEndPoint}");
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleUdpCommunication();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        });

        _serverTcpSocket.Listen(WaitingMaxCount);

        while (true)
        {
            var clientSocket = _serverTcpSocket.Accept();

            Console.WriteLine($"Connected client: {clientSocket.RemoteEndPoint}");

            lock (_lock)
            {
                _tcpClients.Add(clientSocket);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleTcpClientAsync(clientSocket);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });
            }
        }
    }

    private string GetUsername(Socket client)
    {
        var username = "unknown";

        try
        {
            if (client is { Connected: false }) return username;

            var buffer = new byte[_bufferSize];

            var bytesRead = client.Receive(buffer);

            if (bytesRead > 0) username = _encoder.GetString(buffer, 0, bytesRead);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return username;
    }

    private async Task HandleUdpCommunication()
    {
        try
        {
            while (true)
            {
                var buffer = new byte[_bufferSize];

                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

                var receivedSize = _serverUdpSocket.ReceiveFrom(buffer, ref clientEndPoint);

                if (receivedSize > 0)
                {
                    var message = _encoder.GetString(buffer, 0, receivedSize);
                    BroadcastUdpMessage(message, clientEndPoint);
                }

                await Task.Delay(10);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task HandleTcpClientAsync(object state)
    {
        var client = (Socket)state;

        var username = GetUsername(client);

        try
        {
            while (client is { Connected: true })
            {
                var buffer = new byte[_bufferSize];

                var bytesRead = client.Receive(buffer);

                if (bytesRead > 0)
                {
                    var message = $"{username} sent {_encoder.GetString(buffer, 0, bytesRead)}";
                    BroadcastTcpMessage(message, client);
                }

                await Task.Delay(10);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            CloseClientConnection(client, username);
        }
    }

    private void CloseClientConnection(Socket client, string name)
    {
        lock (_lock)
        {
            _tcpClients.Remove(client);
        }

        try
        {
            if (client is { Connected: true }) client.Shutdown(SocketShutdown.Both);

            Console.WriteLine($"Client disconnected: {name}");

            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing client: {ex.Message}");
        }
    }

    private void BroadcastTcpMessage(string message, Socket sender)
    {
        var broadcastBytes = _encoder.GetBytes(message);

        lock (_lock)
        {
            var disconnectedClients = new List<Socket>();
            
            foreach (var client in _tcpClients)
                if (client != sender)
                {
                    if(!client.Connected) disconnectedClients.Add(client);
                    
                    try
                    {
                        client.Send(broadcastBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        disconnectedClients.Add(client);
                    }
                }
            
            foreach (var client in disconnectedClients)
            {
                client.Close();
            }
        }
    }

    private void BroadcastUdpMessage(string message, EndPoint clientEndPoint)
    {
        var broadcastBytes = _encoder.GetBytes(message);

        lock (_lock)
        {
            foreach (var client in _tcpClients)
                if (!Equals(client.RemoteEndPoint, clientEndPoint))
                    try
                    {
                        var udpEndPoint = client.RemoteEndPoint;

                        _serverUdpSocket.SendTo(broadcastBytes, udpEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
        }
    }
}