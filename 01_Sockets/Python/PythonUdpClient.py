import socket

serverIP = "127.0.0.1"
serverPort = 9009
msg = 'Ping Python'

print('PYTHON UDP CLIENT')
client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
client.sendto(bytes(msg, 'cp1250'), (serverIP, serverPort))

buff, address = client.recvfrom(1024)
message = str(buff, 'cp1250')
print("python udp server received msg: " + message)





