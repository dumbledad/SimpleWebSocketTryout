using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var opcode = accruedBytes[0] & 0b00001111;
            var mask = (accruedBytes[1] & 0b10000000) != 0x00; // "All frames sent from client to server have this bit set to 1."
            var payloadLength = (long)(accruedBytes[1] & 0b01111111);
            var offset = 2;
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
            var maskingKey = (uint)0;
            if (mask)
            {
                maskingKey = BitConverter.ToUInt32(accruedBytes.ToArray(), offset);
                offset = offset + 4;
            }
            //TODO: Since we have not allowed for the negotiation of extensions the payload is all application bytes
            if (accruedBytes.Count - offset >= payloadLength)
            {
                var applicationData = accruedBytes.Skip(offset).Take((int)payloadLength); //HACK: What if this frame really is nonger than an int can cope with!
            }
            //TODO: Save frame and clear out the bytes we've used up
            blah
        }

        private void TryBundleAccruedFrames()
        {
            throw new NotImplementedException();
        }

        private void OnDisconnect()
        {
            lock (this)
            {
                buffer = null;
            }
            WebSocketDisconnected?.Invoke(this, EventArgs.Empty);
        }

        List<byte[]> DecodeWebsocketFrame(Byte[] bytes)
        {
            // https://stackoverflow.com/a/25558586/575530
            List<Byte[]> ret = new List<Byte[]>();
            int offset = 0;
            while (offset + 6 < bytes.Length)
            {
                // format: 0==ascii/binary 1=length-0x80, byte 2,3,4,5=key, 6+len=message, repeat with offset for next...
                int len = bytes[offset + 1] - 0x80;

                if (len <= 125)
                {

                    //String data = Encoding.UTF8.GetString(bytes);
                    //Debug.Log("len=" + len + "bytes[" + bytes.Length + "]=" + ByteArrayToString(bytes) + " data[" + data.Length + "]=" + data);
                    Debug.WriteLine("len=" + len + " offset=" + offset);
                    Byte[] key = new Byte[] { bytes[offset + 2], bytes[offset + 3], bytes[offset + 4], bytes[offset + 5] };
                    Byte[] decoded = new Byte[len];
                    for (int i = 0; i < len; i++)
                    {
                        int realPos = offset + 6 + i;
                        decoded[i] = (Byte)(bytes[realPos] ^ key[i % 4]);
                    }
                    offset += 6 + len;
                    ret.Add(decoded);
                }
                else
                {
                    int a = bytes[offset + 2];
                    int b = bytes[offset + 3];
                    len = (a << 8) + b;
                    //Debug.Log("Length of ws: " + len);

                    Byte[] key = new Byte[] { bytes[offset + 4], bytes[offset + 5], bytes[offset + 6], bytes[offset + 7] };
                    Byte[] decoded = new Byte[len];
                    for (int i = 0; i < len; i++)
                    {
                        int realPos = offset + 8 + i;
                        decoded[i] = (Byte)(bytes[realPos] ^ key[i % 4]);
                    }

                    offset += 8 + len;
                    ret.Add(decoded);
                }
            }
            return ret;
        }
    }
}
