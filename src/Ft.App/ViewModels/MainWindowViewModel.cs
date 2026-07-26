using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft.App.Services;
using Ft.Core.Dump;
using Ft.Core.Pipeline;
using Ft.Core.Project;
using Ft.Core.Time;
using Ft.Core.Transport;

namespace Ft.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const int MaxDumpRows = 5000;
    private const int MaxFrameRecords = 10000;

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

    [ObservableProperty]
    private bool _isRawView;

    [ObservableProperty]
    private FrameRecordViewModel? _selectedFrame;

    public ObservableCollection<DumpRowViewModel> DumpRows { get; } = [];
    public ObservableCollection<FrameRecordViewModel> FrameRecords { get; } = [];
    public ObservableCollection<DumpRowViewModel> DetailRows { get; } = [];
    public ObservableCollection<FieldDisplay> DetailFields { get; } = [];
    public string[] BytesPerRowOptions { get; } = ["8 bytes/row", "16 bytes/row", "32 bytes/row"];

    /// <summary>The editable session project (framing/checksum/fields/highlights/macros).</summary>
    public FtProject Project { get; set; } = new();

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

    /// <summary>Connect using the current project's framing/checksum/field config.</summary>
    public async Task ConnectWithProjectAsync(ITransport transport, string summary)
    {
        var config = Project.BuildPipelineConfig(SystemTimeSource.Instance);
        if (!config.IsOk)
        {
            LastError = config.Error;
            return;
        }
        await ConnectAsync(transport, config.Value, summary);
    }

    /// <summary>Re-apply the project config to a live connection (frame def edited).</summary>
    public async Task ApplyProjectAsync()
    {
        if (_transport is null || _pipeline is null)
        {
            return;
        }
        var config = Project.BuildPipelineConfig(SystemTimeSource.Instance);
        if (!config.IsOk)
        {
            LastError = config.Error;
            return;
        }

        _pipeline.BytesFlowed -= OnBytesFlowed;
        _pipeline.FramesReady -= OnFramesReady;
        _pipeline.TransportError -= OnTransportError;
        await _pipeline.StopAsync();

        _pipeline = new RxPipeline(_transport, config.Value);
        _pipeline.BytesFlowed += OnBytesFlowed;
        _pipeline.FramesReady += OnFramesReady;
        _pipeline.TransportError += OnTransportError;
        _pipeline.Start();
        LastError = string.Empty;
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

        Project = DemoProject();
        var transport = new EchoFakeTransport();
        await ConnectWithProjectAsync(transport, "Demo (echo + sample protocol)");
        _demoTraffic = new DemoTraffic(transport);
        _demoTraffic.Start();
        IsDemoMode = true;
    }

    /// <summary>Project describing DemoTraffic's sample protocol.</summary>
    public static FtProject DemoProject() => new()
    {
        Framing = new FramingConfig
        {
            Mode = "LengthField",
            HeaderLen = 2,
            LenOffset = 1,
            LenSize = 1,
            Endian = "LE",
            LenAdjust = 2,
        },
        Checksum = new ChecksumConfig
        {
            Preset = "CRC16_MODBUS",
            OffsetFromEnd = 2,
            ByteOrder = "LE",
            CoverageStart = 0,
            CoverageEndOffsetFromEnd = 2,
        },
        Fields =
        [
            new FieldConfig { Name = "seq", Offset = 2, Type = "u8" },
            new FieldConfig { Name = "temp", Offset = 3, Type = "s16", Endian = "BE" },
            new FieldConfig { Name = "status", Offset = 5, Type = "u8" },
        ],
        Highlights =
        [
            new HighlightConfig { Field = "status", Op = "!=", Value = 0, Color = "#9C2030" },
        ],
    };

    [RelayCommand]
    public void ClearDump()
    {
        _dumpBuilder.Clear();
        DumpRows.Clear();
        PartialRow = null;
        FrameRecords.Clear();
        SelectedFrame = null;
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

    private void OnFrameRecord(FrameRecord record)
    {
        FrameRecords.Add(new FrameRecordViewModel(record));
        while (FrameRecords.Count > MaxFrameRecords) FrameRecords.RemoveAt(0);
    }

    partial void OnSelectedFrameChanged(FrameRecordViewModel? value)
    {
        DetailRows.Clear();
        DetailFields.Clear();
        if (value is null) return;

        var builder = new HexDumpBuilder(16);
        foreach (var row in builder.Append(value.Record.Raw, value.Record.Direction, value.Record.Timestamp))
        {
            DetailRows.Add(new DumpRowViewModel(row));
        }
        if (builder.PartialRow is { } partial) DetailRows.Add(new DumpRowViewModel(partial));

        foreach (var field in value.Record.Fields)
        {
            DetailFields.Add(new FieldDisplay(field.Name, field.Display));
        }
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
