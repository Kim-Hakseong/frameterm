using System.IO.Ports;

namespace Ft.Core.Transport;

/// <summary>Serial port parameters. Baud accepts any custom rate the driver takes.</summary>
public sealed record SerialSettings(
    string PortName,
    int BaudRate = 115200,
    Parity Parity = Parity.None,
    int DataBits = 8,
    StopBits StopBits = StopBits.One,
    Handshake Handshake = Handshake.None,
    bool DtrEnable = false,
    bool RtsEnable = false);
