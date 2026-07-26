using System.IO.Ports;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft.Core.Transport;

namespace Ft.App.Views;

public partial class ConnectionDialog : Window
{
    /// <summary>Ready-to-open transport when the user confirmed valid settings.</summary>
    public ITransport? Transport { get; private set; }

    /// <summary>Short description for the status bar, e.g. "COM3 @ 115200".</summary>
    public string Summary { get; private set; } = string.Empty;

    public ConnectionDialog()
    {
        InitializeComponent();
        RefreshPorts();
    }

    private void RefreshPorts()
    {
        string? selected = PortCombo.SelectedItem as string;
        var ports = SerialTransport.GetPortNames();
        PortCombo.ItemsSource = ports;
        int index = selected is null ? -1 : Array.IndexOf(ports, selected);
        PortCombo.SelectedIndex = index >= 0 ? index : ports.Length > 0 ? 0 : -1;
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SerialPanel is null) return; // during InitializeComponent
        int mode = ModeCombo.SelectedIndex;
        SerialPanel.IsVisible = mode == 0;
        TcpPanel.IsVisible = mode != 0;
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => RefreshPorts();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        switch (ModeCombo.SelectedIndex)
        {
            case 1 or 2:
            {
                if (!int.TryParse(TcpPortBox.Text, out int tcpPort) || tcpPort is < 1 or > 65535)
                {
                    ShowError("TCP port must be 1..65535.");
                    return;
                }
                if (ModeCombo.SelectedIndex == 1)
                {
                    string host = HostBox.Text?.Trim() ?? string.Empty;
                    if (host.Length == 0)
                    {
                        ShowError("Host is required for TCP client mode.");
                        return;
                    }
                    Transport = new TcpClientTransport(host, tcpPort);
                    Summary = $"tcp://{host}:{tcpPort}";
                }
                else
                {
                    Transport = new TcpServerTransport(tcpPort);
                    Summary = $"listen :{tcpPort}";
                }
                Close();
                return;
            }
            default:
                ConnectSerial();
                return;
        }
    }

    private void ConnectSerial()
    {
        if (PortCombo.SelectedItem is not string port || string.IsNullOrWhiteSpace(port))
        {
            ShowError("Select a serial port (or use Demo mode from the toolbar).");
            return;
        }
        if (!int.TryParse(BaudBox.Text, out int baud) || baud <= 0)
        {
            ShowError("Baud rate must be a positive integer.");
            return;
        }

        var settings = new SerialSettings(
            port,
            baud,
            (Parity)ParityCombo.SelectedIndex,
            DataBits: 5 + DataBitsCombo.SelectedIndex,
            StopBits: StopBitsCombo.SelectedIndex switch
            {
                1 => StopBits.OnePointFive,
                2 => StopBits.Two,
                _ => StopBits.One,
            },
            Handshake: FlowCombo.SelectedIndex switch
            {
                1 => Handshake.XOnXOff,
                2 => Handshake.RequestToSend,
                _ => Handshake.None,
            },
            DtrEnable: DtrCheck.IsChecked == true,
            RtsEnable: RtsCheck.IsChecked == true);
        Transport = new SerialTransport(settings);
        Summary = $"{settings.PortName} @ {settings.BaudRate}";
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
