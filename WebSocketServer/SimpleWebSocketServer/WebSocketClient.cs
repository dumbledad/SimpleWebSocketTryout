using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SimpleWebSocketServer
{
    public delegate void DataReceivedEventHandler(WebSocketConnection sender, EventArgs e);
    public delegate void WebSocketDisconnectedEventHandler(WebSocketConnection sender, EventArgs e);

    public class WebSocketConnection
    {
        // https://tools.ietf.org/html/rfc6455
        private const string _ExtensionsPrefix = "Sec-WebSocket-Extensions:";
        private const string _ProtocolPrefix = "Sec-WebSocket-Protocol:";
        private const string _VersionPrefix = "Sec-WebSocket-Version:";
        private const string _KeyPrefix = "Sec-WebSocket-Key:";
        private const string _GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public event DataReceivedEventHandler DataReceivedEvent;
        public event WebSocketDisconnectedEventHandler WebSocketDisconnected;

        private string hashedKey = "";
        private List<byte> bytesReceived = new List<byte>();

        public List<string> RequestedExtensions { get; private set; } = new List<string>();
        public List<string> RequestedProtocols { get; private set; } = new List<string>();
        public string Version { get; private set; }
        public string Key { get; private set; }
        public Socket Socket { get; private set; }
        public string HashedKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(hashedKey))
                {
                    using (var sha1 = new SHA1Managed())
                    {
                        hashedKey = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(Key + _GUID)));
                    }
                }
                return hashedKey;
            }
        }
        public int BufferSize { get; set; } = 511;

        public WebSocketConnection(Socket socket, IEnumerable<string> clientHandshakeLines)
        {
            Socket = socket;

            var requestedExtensionsLine = clientHandshakeLines.FirstOrDefault(line => line.StartsWith(_ExtensionsPrefix));
            RequestedExtensions = requestedExtensionsLine.Substring(_ExtensionsPrefix.Length).Split(';').Select(fragment => fragment.Trim()).ToList();

            var requestedProtocolsLine = clientHandshakeLines.FirstOrDefault(line => line.StartsWith(_ProtocolPrefix));
            RequestedExtensions = requestedProtocolsLine.Substring(_ProtocolPrefix.Length).Split(';').Select(fragment => fragment.Trim()).ToList();

            var versionLine = clientHandshakeLines.FirstOrDefault(line => line.StartsWith(_VersionPrefix));
            Version = versionLine.Substring(_VersionPrefix.Length).Trim();

            var keyLine = clientHandshakeLines.FirstOrDefault(line => line.StartsWith(_KeyPrefix));
            Key = versionLine.Substring(_KeyPrefix.Length).Trim();
        }

        public void BeginReceive()
        {
            Socket.BeginReceive(new byte[BufferSize], 0, BufferSize, 0, OnRead, null);
        }

        private void OnRead(IAsyncResult asyncResult)
        {
            var sizeOfReceivedData = Socket.EndReceive(asyncResult);
            if (sizeOfReceivedData > 0)
            {
                //TODO: get bytes and parse out frame
            }
            else // the socket is closed
            {
                WebSocketDisconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool TryParseBytes(IEnumerable<byte> bytes, out WebSocketFrame result)
        {
            WebSocketFrame
        }
    }
}
