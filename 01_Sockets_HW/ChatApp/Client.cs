using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatApp;

public class Client(string username)
{
    private const int _bufferSize = 1024;
    private readonly Encoding _encoder = Encoding.GetEncoding("UTF-8");
    private readonly string _serverIp = "127.0.0.1";
    private readonly int _serverPort = 8888;
    private readonly string _multicastAddress = "239.255.0.1";
    private readonly int _multicastPort = 8889;
    private Task? _inputTask;
    private bool _running = true;
    private bool _disconnected = false;
    private IPEndPoint? _server;
    private Task? _tcpListenTask;
    private Socket? _tcpSocket;
    private Task? _udpListenTask;
    private Socket? _udpSocket;
    private Task? _multicastListenTask;
    private Socket? _multicastSocket;


    public async Task Run()
    {
        Thread.Sleep(1000);

        try
        {
            if (Connect())
            {
                Console.WriteLine("Connected with server!");

                _inputTask = Task.Run(HandleInput);
                _udpListenTask = Task.Run(HandleUdpMessages);
                _tcpListenTask = Task.Run(HandleTcpMessages);
                _multicastListenTask = Task.Run(HandleMulticastMessages);

                await Task.WhenAll(_inputTask, _udpListenTask, _tcpListenTask, _multicastListenTask);

            }
            else
            {
                Console.WriteLine("Connection error.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            Disconnect();
        }
    }

    private bool Connect()
    {
        try
        {
            _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _server = new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort);

            _tcpSocket.Connect(_server);
            var localEndPoint = (IPEndPoint)_tcpSocket.LocalEndPoint;

            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpSocket.Bind(new IPEndPoint(localEndPoint.Address, localEndPoint.Port));

            _multicastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _multicastSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _multicastSocket.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));
            _multicastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(_multicastAddress)));

            Console.WriteLine($"TCP endpoint: {_tcpSocket.LocalEndPoint}");
            Console.WriteLine($"UDP endpoint: {_udpSocket.LocalEndPoint}");

            SendTcpMessage(username);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Disconnect();
            return false;
        }
    }

    private void Disconnect()
    {
        if (_disconnected) return;

        try
        {
            if (_tcpSocket is { Connected: true })
            {
                _tcpSocket.Shutdown(SocketShutdown.Both);
                _tcpSocket.Close();
            }

            _udpSocket?.Close();

            _multicastSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership,
                new MulticastOption(IPAddress.Parse(_multicastAddress)));
            _multicastSocket?.Close();

            _disconnected = true;
        }
        catch (Exception ex)
        {
        }
        finally
        {
            _disconnected = true;
        }
    }

    private async Task HandleInput()
    {
        Console.WriteLine("Type your messages(U to use UDP, M to use multicast, exit to exit):");

        while (_running)
        {
            try
            {
                var message = Console.ReadLine();

                if (message != null)
                    switch (message.Trim())
                    {
                        case "U":
                            var heartArt = @"
      /\  /\
     /  \/  \
    (        )
     \      /
      \    /
       \  /
        \/";
                            SendUdpMessage($"UDP ASCII art from {username}: \n{heartArt}");
                            break;
                        case "M":
                            var starArt = @"
    *
   ***
  *****
 *******
*********
   | |";
                            SendMulticastMessage($"Multicast ASCII art from {username}: \n{starArt}");
                            break;

                        case "exit":
                            Console.WriteLine("Closing client...");
                            _running = false;
                            Disconnect();
                            break;
                        default:
                            SendTcpMessage(message);
                            break;
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(10);
        }
    }

    private async Task? HandleTcpMessages()
    {
        while (_running && _tcpSocket is { Connected: true })
            try
            {
                var message = string.Empty;

                var buffer = new byte[_bufferSize];

                var bytesRead = _tcpSocket.Receive(buffer);

                if (bytesRead > 0) message = _encoder.GetString(buffer, 0, bytesRead);

                if (!string.IsNullOrEmpty(message)) Console.WriteLine(message);

                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _running = false;
            }
    }

    private async Task? HandleUdpMessages()
    {
        while (_running)
            try
            {
                if (_udpSocket.Available > 0)
                {
                    var buffer = new byte[_bufferSize];

                    var receivedBytes = _udpSocket.Receive(buffer);

                    if (receivedBytes > 0)
                    {
                        var message = _encoder.GetString(buffer, 0, receivedBytes);
                        Console.WriteLine(message);
                    }
                }

                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _running = false;
            }
    }
    
    private async Task? HandleMulticastMessages()
    {
        while (_running)
            try
            {
                if (_multicastSocket.Available > 0)
                {
                    var buffer = new byte[_bufferSize];

                    var receivedBytes = _multicastSocket.Receive(buffer);

                    if (receivedBytes > 0)
                    {
                        var message = _encoder.GetString(buffer, 0, receivedBytes);
                        if (!message.StartsWith($"Multicast ASCII art from {username}"))
                            Console.WriteLine(message);
                    }
                }

                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _running = false;
            }
    }

    private void SendTcpMessage(string message)
    {
        if (_tcpSocket is not { Connected: true }) return;
        
        var messageBytes = _encoder.GetBytes(message);
        _tcpSocket.Send(messageBytes);
    }

    private void SendUdpMessage(string message)
    {
        if (_udpSocket == null || _server == null) return;

        var messageBytes = _encoder.GetBytes(message);
        _udpSocket.SendTo(messageBytes, _server);

    }

    private void SendMulticastMessage(string message)
    {
        if (_multicastSocket == null) return;

        var messageBytes = _encoder.GetBytes(message);

        var multicastEndPoint = new IPEndPoint(IPAddress.Parse(_multicastAddress), _multicastPort);

        _multicastSocket.SendTo(messageBytes, multicastEndPoint);
    }
}