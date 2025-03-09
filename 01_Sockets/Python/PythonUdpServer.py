import socket

from numpy.f2py.auxfuncs import throw_error

serverPort = 9009
serverSocket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
serverSocket.bind(('', serverPort))
buff = []

print('PYTHON UDP SERVER')

while True:
    buff, address = serverSocket.recvfrom(1024)
    message = str(buff, 'cp1250')
    print("python udp server received msg: " + message)

    if 'python' in message.lower():
        serverSocket.sendto(bytes("Pong Python", 'cp1250'), address)
    elif 'java' in message.lower():
        serverSocket.sendto(bytes("Pong Java", 'cp1250'), address)
    else:
        continue