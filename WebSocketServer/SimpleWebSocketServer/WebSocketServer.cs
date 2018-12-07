using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleWebSocketServer
{
    public delegate void ClientConnectedEventHandler(WebSocketServer sender, ClientConnectedEventArgs e);

    public class WebSocketServer
    {
        public event ClientConnectedEventHandler ClientConnected;

        private List<WebSocketConnection> clients = new List<WebSocketConnection>();

        public int Port { get; private set; }
        public Socket ListenerSocker { get; private set; }
        public IEnumerable<WebSocketConnection> Clients
        {
            get
            {
                return new ReadOnlyCollection<WebSocketConnection>(clients);
            }
        }
        public int BufferSize { get; set; } = 511;

        public WebSocketServer(int port) => Port = port;

        public void Start()
        {
            ListenerSocker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            ListenerSocker.Bind(new IPEndPoint(IPAddress.Any, Port));
            ListenerSocker.Listen(100);
            Debug.WriteLine($"websocket server stated on {ListenerSocker.LocalEndPoint}");
            ListenerSocker.BeginAccept(new AsyncCallback(OnClientConnect), null);
        }

        private void OnClientConnect(IAsyncResult asyn)
        {
            var clientSocket = ListenerSocker.EndAccept(asyn);

            var lines = new List<string>();

            using (var stream = new NetworkStream(clientSocket))
            using (var reader = new StreamReader(stream))
            {
                string line = reader.ReadLine();
                while (line != "")
                {
                    lines.Add(line);
                    Debug.WriteLine($"websocket client sent {line}");
                    line = reader.ReadLine();
                }
            }

            var client = new WebSocketConnection(clientSocket, lines);
            clients.Add(client);
            SendOpeningHandShake(client);
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs { Connection = client });
            client.BeginReceive();
        }

        private void SendOpeningHandShake(WebSocketConnection client)
        {
            using (var stream = new NetworkStream(client.Socket))
            using (var writer = new StreamWriter(stream))
            {
                //TODO: When we send a handshake to the client we are not acknowledging and thus agreeing to any of the requested extensions or protocols. These could be implemented and agreed to, e.g. compression.
                writer.WriteLine("HTTP/1.1 101 Web Socket Protocol Handshake");
                writer.WriteLine("Upgrade: WebSocket");
                writer.WriteLine("Connection: Upgrade");
                writer.WriteLine($"Sec-WebSocket-Accept: {client.HashedKey}");
                writer.WriteLine("");
            }
        }
    }
}
