using System.Collections.Generic;

namespace SimpleWebSocketServer
{
    public class WebSocketFrame
    {
        public bool Fin { get; set; }
        public bool Rsv1 { get; set; }
        public bool Rsv2 { get; set; }
        public bool Rsv3 { get; set; }
        public WebSocketFrameOpcodes Opcode { get; set; }
        public bool Mask { get; set; }
        public long PayloadLength { get; set; }
        public List<byte> MaskingKey { get; set; }
        public List<byte> ApplicationData { get; set; }
        public List<byte> UnmaskedApplicationData
        {
            get
            {
                if (!Mask) return ApplicationData;
                var value = new List<byte>();
                for (var i = 0; i < ApplicationData.Count; i++)
                {
                    value.Add((byte)(ApplicationData[i] ^ MaskingKey[i % 4]));
                }
                return value;
            }
        }
    }
}
