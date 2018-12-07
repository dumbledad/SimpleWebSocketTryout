using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleWebSocketServer
{
    public class ClientConnectedEventArgs : EventArgs
    {
        public WebSocketConnection Connection { get; set; }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public WebSocketFrameOpcodes Opcode { get; set; }
        public List<byte> UnmaskedApplicationData { get; set; }
    }
}
