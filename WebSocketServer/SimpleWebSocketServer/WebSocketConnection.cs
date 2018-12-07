using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SimpleWebSocketServer
{
    public delegate void DataReceivedEventHandler(WebSocketConnection sender, DataReceivedEventArgs e);
    public delegate void WebSocketDisconnectedEventHandler(WebSocketConnection sender, EventArgs e);

    public class WebSocketConnection
    {
        // https://tools.ietf.org/html/rfc6455
        private const string _ExtensionsPrefix = "Sec-WebSocket-Extensions:";
        private const string _ProtocolPrefix = "Sec-WebSocket-Protocol:";
        private const string _VersionPrefix = "Sec-WebSocket-Version:";
        private const string _KeyPrefix = "Sec-WebSocket-Key:";
        private const string _GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const int _BufferSize = 511;

        public event DataReceivedEventHandler DataReceivedEvent;
        public event WebSocketDisconnectedEventHandler WebSocketDisconnected;

        private string hashedKey = "";
        private List<byte> accruedBytes = new List<byte>();
        private List<WebSocketFrame> accruedFrames = new List<WebSocketFrame>();
        private byte[] buffer;

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
            lock (this)
            {
                if (buffer == null)
                {
                    buffer = new byte[_BufferSize];
                    Socket.BeginReceive(buffer, 0, _BufferSize, 0, OnRead, null);
                }
            }
        }

        private void OnRead(IAsyncResult asyncResult)
        {
            var sizeOfReceivedData = Socket.EndReceive(asyncResult);
            if (sizeOfReceivedData > 0)
            {
                WebSocketFrame resultingFrame = null;
                var parsedAccruedBytes = false;
                lock (this)
                {
                    accruedBytes.AddRange(buffer);
                    parsedAccruedBytes = TryParseAccruedBytes(out resultingFrame);
                    Array.Clear(buffer, 0, _BufferSize);
                }
                if (parsedAccruedBytes && resultingFrame != null) 
                {
                    accruedFrames.Add(resultingFrame);
                    TryBundleAccruedFrames();
                }
            }
            else // the socket is closed
            {
                OnDisconnect();
            }
        }

        private bool TryParseAccruedBytes(out WebSocketFrame result)
        {
            // https://tools.ietf.org/html/rfc6455

            //      0                   1                   2                   3
            //      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            //     +-+-+-+-+-------+-+-------------+-------------------------------+
            //     |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
            //     |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
            //     |N|V|V|V|       |S|             |   (if payload len==126/127)   |
            //     | |1|2|3|       |K|             |                               |
            //     +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
            //     |     Extended payload length continued, if payload len == 127  |
            //     + - - - - - - - - - - - - - - - +-------------------------------+
            //     |                               |Masking-key, if MASK set to 1  |
            //     +-------------------------------+-------------------------------+
            //     | Masking-key (continued)       |          Payload Data         |
            //     +-------------------------------- - - - - - - - - - - - - - - - +
            //     :                     Payload Data continued ...                :
            //     + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
            //     |                     Payload Data continued ...                |
            //     +---------------------------------------------------------------+

            result = null;
            if (accruedBytes.Count < 6) return false;
            var fin = (accruedBytes[0] & 0b10000000) != 0x00;
            var rsv1 = (accruedBytes[0] & 0b01000000) != 0x00;
            var rsv2 = (accruedBytes[0] & 0b00100000) != 0x00;
            var rsv3 = (accruedBytes[0] & 0b00010000) != 0x00;
            var opcode = (WebSocketFrameOpcodes)(accruedBytes[0] & 0b00001111);
            var mask = (accruedBytes[1] & 0b10000000) != 0x00; // "All frames sent from client to server have this bit set to 1."
            var payloadLength = (long)(accruedBytes[1] & 0b01111111);
            var offset = 2;
            //TODO: are the bytes in the payload length reveresed? The spec says "Multibyte length quantities are expressed in network byte order". I think that means big-endian (https://stackoverflow.com/a/43818683/575530), so this should work
            if (payloadLength == 126)
            {
                payloadLength = BitConverter.ToInt16(accruedBytes.ToArray(), offset);
                offset = 4;
            }
            else if (payloadLength == 127)
            {
                payloadLength = BitConverter.ToInt64(accruedBytes.ToArray(), offset);
                offset = 10;
            }
            var maskingKey = new List<byte>();
            if (mask)
            {
                maskingKey.AddRange(accruedBytes.Skip(offset).Take(4));
                offset = offset + 4;
            }
            //TODO: Since we have not allowed for the negotiation of extensions the payload is all application bytes
            if (accruedBytes.Count - offset >= payloadLength)
            {
                var applicationData = accruedBytes.Skip(offset).Take((int)payloadLength).ToList(); //HACK: What if this frame really is nonger than an int can cope with!
                result = new WebSocketFrame
                {
                    Fin = fin,
                    Rsv1 = rsv1,
                    Rsv2 = rsv2,
                    Rsv3 = rsv3,
                    Opcode = opcode,
                    Mask = mask,
                    PayloadLength = payloadLength,
                    MaskingKey = maskingKey,
                    ApplicationData = applicationData
                };
                return true;
            }
            return false;
        }

        private void TryBundleAccruedFrames()
        {
            if (accruedFrames.Count == 0) return;
            if (accruedFrames[0].Fin)
            {
                DataReceivedEvent?.Invoke(this, new DataReceivedEventArgs { Opcode = accruedFrames[0].Opcode, UnmaskedApplicationData = accruedFrames[0].UnmaskedApplicationData });
                accruedFrames.RemoveAt(0);
                if (accruedFrames.Count != 0) TryBundleAccruedFrames();
            }
            else 
            {
                var indexOfFinFrame = accruedFrames.FindIndex(frame => frame.Fin);
                if (indexOfFinFrame != -1)
                {
                    var opcode = accruedFrames[0].Opcode; //CHECK: are these all the same
                    var unmaskedApplicationData = new List<byte>();
                    for (var i = 0; i < indexOfFinFrame; i++)
                    {
                        unmaskedApplicationData.AddRange(accruedFrames[0].UnmaskedApplicationData);
                    }
                    for (var i = 0; i < indexOfFinFrame; i++) accruedFrames.RemoveAt(0);
                    DataReceivedEvent?.Invoke(this, new DataReceivedEventArgs { Opcode = opcode, UnmaskedApplicationData = unmaskedApplicationData });
                    if (accruedFrames.Count != 0) TryBundleAccruedFrames();
                }
            }
        }

        private void OnDisconnect()
        {
            lock (this)
            {
                buffer = null;
            }
            WebSocketDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }
}
