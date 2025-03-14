using System.Net;
using System.Net.Sockets;

namespace ChatApp;

public class ClientData() : IEquatable<ClientData>
{
    public Socket TcpSocket { get; init; } = null;
    public EndPoint UdpEndpoint { get; init; } = null;
    public string Username { get; init; } = null;
    public bool Disconnected { get; set; } = false;

    public bool Equals(ClientData other)
    {
        return TcpSocket.Equals(other.TcpSocket) && UdpEndpoint.Equals(other.UdpEndpoint) && Username == other.Username;
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TcpSocket, UdpEndpoint, Username);
    }
}
