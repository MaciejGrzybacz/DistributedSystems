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
    private Task? _inputTask;
    private bool _running = true;
    private IPEndPoint? _server;
    private Task? _tcpListenTask;
    private Socket? _tcpSocket;
    private Task? _udpListenTask;
    private Socket? _udpSocket;

    public void Run()
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

                Task.WaitAll(_inputTask, _udpListenTask, _tcpListenTask);
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

    private async Task? HandleTcpMessages()
    {
        while (_running && _tcpSocket is { Connected: true })
            try
            {
                var message = ReceiveTcpMessage();

                if (!string.IsNullOrEmpty(message)) Console.WriteLine(message);

                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                if (_tcpSocket is not { Connected: true }) break;
            }
    }

    private async Task? HandleUdpMessages()
    {
        if (_udpSocket == null) return;

        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[_bufferSize];

        while (_running)
            try
            {
                if (_udpSocket.Available > 0)
                {
                    var receivedBytes = _udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);

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
                Console.WriteLine($"UDP error: {ex.Message}");
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

            Console.WriteLine($"TCP endpoint: {_tcpSocket.LocalEndPoint}");
            Console.WriteLine($"UDP endpoint: {_udpSocket.LocalEndPoint}");
            
            SendTcpMessage(username);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private void Disconnect()
    {
        try
        {
            if(_tcpSocket is {Connected: true}) 
            {            
                _tcpSocket.Shutdown(SocketShutdown.Both);
                _tcpSocket.Close();
            }
            
            if (_udpSocket is { Connected: true })
            {
                _udpSocket.Shutdown(SocketShutdown.Both);
                _udpSocket.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void SendTcpMessage(string message)
    {
        if (_tcpSocket is not { Connected: true }) return;
        try
        {
            var messageBytes = _encoder.GetBytes(message);
            _tcpSocket.Send(messageBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private string ReceiveTcpMessage()
    {
        var message = string.Empty;

        if (_tcpSocket is not { Connected: true }) return message;
        var buffer = new byte[_bufferSize];

        var bytesRead = _tcpSocket.Receive(buffer);

        if (bytesRead > 0) message = _encoder.GetString(buffer, 0, bytesRead);

        return message;
    }

    private async Task HandleInput()
    {
        Console.WriteLine("Type your messages(U to use UDP, exit to exit):");

        while (_running)
        {
            try
            {
                var message = Console.ReadLine();
        
                if (message != null)
                {
                    switch (message.Trim())
                    {
                        case "U":
                            SendUdpMessage($"UDP message from {username}");
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

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(10);
            
        }
    }

    private void SendUdpMessage(string message)
    {
        if (_udpSocket == null || _server == null) return;

        try
        {
            var messageBytes = _encoder.GetBytes(message);
            _udpSocket.SendTo(messageBytes, _server);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}