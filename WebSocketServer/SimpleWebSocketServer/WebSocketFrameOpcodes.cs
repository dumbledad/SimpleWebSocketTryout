using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleWebSocketServer
{
    public enum WebSocketFrameOpcodes
    {
        Continuation = 0x00,
        Text = 0x01,
        Binary = 0x02,
        ReservedNonControlFrame1 = 0x03,
        ReservedNonControlFrame2 = 0x04,
        ReservedNonControlFrame3 = 0x05,
        ReservedNonControlFrame4 = 0x06,
        ReservedNonControlFrame5 = 0x07,
        Close = 0x08,
        Ping = 0x09,
        Pong = 0x0A,
        ReservedFurtherControlFrame1 = 0x0B,
        ReservedFurtherControlFrame2 = 0x0C,
        ReservedFurtherControlFrame3 = 0x0D,
        ReservedFurtherControlFrame4 = 0x0E,
        ReservedFurtherControlFrame5 = 0x0F
    }
}
