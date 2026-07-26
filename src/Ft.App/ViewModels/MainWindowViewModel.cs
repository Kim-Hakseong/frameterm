using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft.App.Services;
using Ft.Core.Checksum;
using Ft.Core.Dump;
using Ft.Core.Framing;
using Ft.Core.Parsing;
using Ft.Core.Pipeline;
using Ft.Core.Transport;

namespace Ft.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const int MaxDumpRows = 5000;

    private ITransport? _transport;
    private RxPipeline? _pipeline;
    private DemoTraffic? _demoTraffic;
    private HexDumpBuilder _dumpBuilder = new(16);
    private long _frameCount;
    private long _errorCount;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isDemoMode;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _portSummary = "-";

    [ObservableProperty]
    private string _statsText = "RX 0 B · TX 0 B · frames 0 · errors 0 · drops 0";

    [ObservableProperty]
    private string _lastError = string.Empty;

    [ObservableProperty]
    private int _bytesPerRowIndex = 1; // 0=8, 1=16, 2=32

    [ObservableProperty]
    private DumpRowViewModel? _partialRow;

    public ObservableCollection<DumpRowViewModel> DumpRows { get; } = [];
    public string[] BytesPerRowOptions { get; } = ["8 bytes/row", "16 bytes/row", "32 bytes/row"];

    private int BytesPerRow => BytesPerRowIndex switch { 0 => 8, 2 => 32, _ => 16 };

    /// <summary>Connect over a concrete transport with the given pipeline config.</summary>
    public async Task ConnectAsync(ITransport transport, PipelineConfig config, string summary)
    {
        await DisconnectAsync();

        var opened = await transport.OpenAsync(CancellationToken.None);
        if (!opened.IsOk)
        {
            LastError = opened.Error;
            return;
        }

        _transport = transport;
        _pipeline = new RxPipeline(transport, config);
        _pipeline.BytesFlowed += OnBytesFlowed;
        _pipeline.FramesReady += OnFramesReady;
        _pipeline.TransportError += OnTransportError;
        _pipeline.Start();

        IsConnected = true;
        PortSummary = summary;
        ConnectionStatus = $"Connected · {summary}";
        LastError = string.Empty;
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        _demoTraffic?.Dispose();
        _demoTraffic = null;

        if (_pipeline is not null)
        {
            _pipeline.BytesFlowed -= OnBytesFlowed;
            _pipeline.FramesReady -= OnFramesReady;
            _pipeline.TransportError -= OnTransportError;
            await _pipeline.StopAsync();
            _pipeline = null;
        }
        if (_transport is not null)
        {
            await _transport.CloseAsync();
            _transport = null;
        }

        IsConnected = false;
        IsDemoMode = false;
        ConnectionStatus = "Disconnected";
        PortSummary = "-";
    }

    /// <summary>Demo mode: echo transport + built-in sample protocol traffic.</summary>
    [RelayCommand]
    public async Task ToggleDemoAsync()
    {
        if (IsDemoMode)
        {
            await DisconnectAsync();
            return;
        }

        var transport = new EchoFakeTransport();
        await ConnectAsync(transport, DemoPipelineConfig(), "Demo (echo + sample protocol)");
        _demoTraffic = new DemoTraffic(transport);
        _demoTraffic.Start();
        IsDemoMode = true;
    }

    /// <summary>Pipeline config matching DemoTraffic's sample protocol.</summary>
    public static PipelineConfig DemoPipelineConfig() => new()
    {
        Framer = new LengthFieldFramer(headerLen: 2, lenOffset: 1, lenSize: 1, ByteOrder.Little, lenAdjust: 2),
        ChecksumSpec = ChecksumPresets.Crc16Modbus,
        ChecksumPlacement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2),
        Fields =
        [
            new FieldSpec("seq", 2, FieldType.U8),
            new FieldSpec("temp", 3, FieldType.S16, ByteOrder.Big),
            new FieldSpec("status", 5, FieldType.U8),
        ],
        Highlights =
        [
            new HighlightRule("#9C2030", new FieldCondition("status", FieldOp.Ne, 0)),
        ],
    };

    [RelayCommand]
    public void ClearDump()
    {
        _dumpBuilder.Clear();
        DumpRows.Clear();
        PartialRow = null;
        _frameCount = 0;
        _errorCount = 0;
        UpdateStats();
    }

    partial void OnBytesPerRowIndexChanged(int value)
    {
        _dumpBuilder = new HexDumpBuilder(BytesPerRow);
        DumpRows.Clear();
        PartialRow = null;
    }

    public async Task SendAsync(byte[] payload)
    {
        if (_pipeline is null) return;
        var sent = await _pipeline.SendAsync(payload, CancellationToken.None);
        if (!sent.IsOk) LastError = sent.Error;
    }

    private void OnBytesFlowed(byte[] chunk, FrameDirection dir, DateTimeOffset ts) =>
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var row in _dumpBuilder.Append(chunk, dir, ts))
            {
                DumpRows.Add(new DumpRowViewModel(row));
            }
            while (DumpRows.Count > MaxDumpRows) DumpRows.RemoveAt(0);
            PartialRow = _dumpBuilder.PartialRow is { } partial ? new DumpRowViewModel(partial) : null;
            UpdateStats();
        });

    private void OnFramesReady(IReadOnlyList<FrameRecord> batch) =>
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var record in batch)
            {
                _frameCount++;
                if (record.ChecksumOk == false) _errorCount++;
                OnFrameRecord(record);
            }
            UpdateStats();
        });

    /// <summary>Extension point for the frame list (M6).</summary>
    protected virtual void OnFrameRecord(FrameRecord record)
    {
    }

    private void OnTransportError(string message) =>
        Dispatcher.UIThread.Post(() =>
        {
            LastError = message;
            ConnectionStatus = $"Error · {message}";
        });

    private void UpdateStats()
    {
        long rx = _pipeline?.RxBytes ?? 0;
        long tx = _pipeline?.TxBytes ?? 0;
        long drops = _pipeline?.DropCount ?? 0;
        StatsText = $"RX {rx} B · TX {tx} B · frames {_frameCount} · errors {_errorCount} · drops {drops}";
    }
}
