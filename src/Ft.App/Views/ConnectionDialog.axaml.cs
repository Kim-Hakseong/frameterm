using System.IO.Ports;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft.Core.Transport;

namespace Ft.App.Views;

public partial class ConnectionDialog : Window
{
    /// <summary>Non-null when the user confirmed with valid settings.</summary>
    public SerialSettings? Result { get; private set; }

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

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => RefreshPorts();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnConnectClick(object? sender, RoutedEventArgs e)
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

        Result = new SerialSettings(
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
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
