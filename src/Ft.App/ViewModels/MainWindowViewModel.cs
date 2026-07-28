using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft.App.Services;
using Ft.Core.Compose;
using Ft.Core.Dump;
using Ft.Core.Licensing;
using Ft.Core.Logging;
using Ft.Core.Parsing;
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

    // Individual counters surfaced for the dashboard stat tiles (display only).
    [ObservableProperty]
    private string _rxText = "0";

    [ObservableProperty]
    private string _txText = "0";

    [ObservableProperty]
    private string _framesText = "0";

    [ObservableProperty]
    private string _errorsText = "0";

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

    [ObservableProperty]
    private string _composeText = string.Empty;

    [ObservableProperty]
    private string _composeError = string.Empty;

    [ObservableProperty]
    private bool _repeatEnabled;

    [ObservableProperty]
    private string _repeatMsText = "500";

    [ObservableProperty]
    private bool _filterErrorsOnly;

    [ObservableProperty]
    private string _filterPattern = string.Empty;

    [ObservableProperty]
    private bool _isLogging;

    [ObservableProperty]
    private string _logStatus = string.Empty;

    private CancellationTokenSource? _repeatCts;
    private RawLogWriter? _logWriter;
    private BytePattern? _compiledFilter;
    private AutoResponder? _autoResponder;
    private readonly List<FrameRecordViewModel> _allFrames = [];

    /// <summary>
    /// License verification public key. Skeleton: RFC 8032 test-vector key so
    /// the whole path is exercisable; replace with the production key (and
    /// keep the signing seed offline) before selling (M10+).
    /// </summary>
    private const string LicensePublicKeyHex =
        "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    [ObservableProperty]
    private string _trialStatusText = string.Empty;

    public MainWindowViewModel()
    {
        EvaluateTrial();
    }

    private void EvaluateTrial()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FrameTerm");
        string? licenseKey = null;
        string licensePath = Path.Combine(dir, "license.key");
        try
        {
            if (File.Exists(licensePath)) licenseKey = File.ReadAllText(licensePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable license file → treated as unlicensed trial.
        }

        var manager = new TrialManager(
            Path.Combine(dir, "trial.dat"),
            Convert.FromHexString(LicensePublicKeyHex),
            SystemTimeSource.Instance);
        TrialStatusText = manager.Evaluate(licenseKey).Detail;
    }

    public ObservableCollection<DumpRowViewModel> DumpRows { get; } = [];
    public ObservableCollection<FrameRecordViewModel> FrameRecords { get; } = [];
    public ObservableCollection<DumpRowViewModel> DetailRows { get; } = [];
    public ObservableCollection<FieldDisplay> DetailFields { get; } = [];
    public ObservableCollection<MacroViewModel> Macros { get; } = [];
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
        AttachPipeline(new RxPipeline(transport, config));

        IsConnected = true;
        PortSummary = summary;
        ConnectionStatus = $"Connected · {summary}";
        LastError = string.Empty;
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        RepeatEnabled = false;
        await StopLoggingAsync();
        _demoTraffic?.Dispose();
        _demoTraffic = null;

        await DetachPipelineAsync();
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

        await DetachPipelineAsync();
        AttachPipeline(new RxPipeline(_transport, config.Value));
        LastError = string.Empty;
    }

    /// <summary>Subscribe events, build the auto-responder, and start the pipeline.</summary>
    private void AttachPipeline(RxPipeline pipeline)
    {
        _pipeline = pipeline;
        pipeline.BytesFlowed += OnBytesFlowed;
        pipeline.FramesReady += OnFramesReady;
        pipeline.TransportError += OnTransportError;

        var rules = Project.BuildAutoRespondRules();
        if (!rules.IsOk)
        {
            LastError = rules.Error;
        }
        else if (rules.Value.Count > 0)
        {
            var responder = new AutoResponder(rules.Value, payload => SendOnAsync(pipeline, payload));
            responder.ComposeFailed += message =>
                Dispatcher.UIThread.Post(() => LastError = message);
            pipeline.FramesReady += responder.HandleFrames;
            _autoResponder = responder;
        }

        pipeline.Start();
    }

    private async Task SendOnAsync(RxPipeline pipeline, byte[] payload)
    {
        var sent = await pipeline.SendAsync(payload, CancellationToken.None);
        if (!sent.IsOk)
        {
            Dispatcher.UIThread.Post(() => LastError = sent.Error);
        }
    }

    private async Task DetachPipelineAsync()
    {
        if (_pipeline is null) return;
        _pipeline.BytesFlowed -= OnBytesFlowed;
        _pipeline.FramesReady -= OnFramesReady;
        _pipeline.TransportError -= OnTransportError;
        if (_autoResponder is not null)
        {
            _pipeline.FramesReady -= _autoResponder.HandleFrames;
            _autoResponder = null;
        }
        await _pipeline.StopAsync();
        _pipeline = null;
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
        _allFrames.Clear();
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

    /// <summary>Compose the current expression and send it once.</summary>
    [RelayCommand]
    public async Task SendComposedAsync()
    {
        var payload = PayloadComposer.Compose(ComposeText);
        if (!payload.IsOk)
        {
            ComposeError = payload.Error;
            return;
        }
        ComposeError = string.Empty;
        await SendAsync(payload.Value);
    }

    /// <summary>Load a macro into the compose bar and send it.</summary>
    [RelayCommand]
    public async Task RunMacroAsync(MacroViewModel macro)
    {
        ComposeText = macro.Text;
        await SendComposedAsync();
    }

    /// <summary>Fire the macro bound to a hotkey name like "F5", if any.</summary>
    public async Task<bool> RunHotkeyMacroAsync(string key)
    {
        var macro = Macros.FirstOrDefault(m =>
            m.Hotkey.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (macro is null) return false;
        await RunMacroAsync(macro);
        return true;
    }

    public void ReloadMacros()
    {
        Macros.Clear();
        foreach (var macro in Project.Macros.Take(20))
        {
            Macros.Add(new MacroViewModel(macro, RunMacroCommand));
        }
    }

    partial void OnRepeatEnabledChanged(bool value)
    {
        _repeatCts?.Cancel();
        _repeatCts?.Dispose();
        _repeatCts = null;
        if (!value) return;

        if (!int.TryParse(RepeatMsText, out int periodMs) || periodMs < 10)
        {
            ComposeError = "Repeat period must be an integer ≥ 10 ms.";
            RepeatEnabled = false;
            return;
        }

        _repeatCts = new CancellationTokenSource();
        _ = RepeatLoopAsync(periodMs, _repeatCts.Token);
    }

    private async Task RepeatLoopAsync(int periodMs, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await Dispatcher.UIThread.InvokeAsync(SendComposedAsync);
                if (!IsConnected || ComposeError.Length > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => RepeatEnabled = false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Toggled off.
        }
    }

    private void OnBytesFlowed(byte[] chunk, FrameDirection dir, DateTimeOffset ts)
    {
        // Log from the pipeline thread — TryWrite never blocks the RX path.
        _logWriter?.WriteChunk(chunk, dir, ts);
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
    }

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
        var row = new FrameRecordViewModel(record);
        _allFrames.Add(row);
        while (_allFrames.Count > MaxFrameRecords) _allFrames.RemoveAt(0);
        if (PassesFilter(row))
        {
            FrameRecords.Add(row);
            while (FrameRecords.Count > MaxFrameRecords) FrameRecords.RemoveAt(0);
        }
    }

    private bool PassesFilter(FrameRecordViewModel row)
    {
        if (FilterErrorsOnly && row.Record.ChecksumOk != false) return false;
        if (_compiledFilter is not null && !_compiledFilter.Matches(row.Record.Raw)) return false;
        return true;
    }

    private void RebuildFilteredFrames()
    {
        FrameRecords.Clear();
        foreach (var row in _allFrames)
        {
            if (PassesFilter(row)) FrameRecords.Add(row);
        }
    }

    partial void OnFilterErrorsOnlyChanged(bool value) => RebuildFilteredFrames();

    partial void OnFilterPatternChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _compiledFilter = null;
            LastError = string.Empty;
        }
        else
        {
            var parsed = BytePattern.Parse(value);
            if (!parsed.IsOk)
            {
                LastError = parsed.Error;
                return;
            }
            _compiledFilter = parsed.Value;
            LastError = string.Empty;
        }
        RebuildFilteredFrames();
    }

    /// <summary>Start writing raw traffic (pre-framing) to a log file.</summary>
    public void StartLogging(string path)
    {
        _logWriter = new RawLogWriter(path);
        IsLogging = true;
        LogStatus = $"REC {System.IO.Path.GetFileName(path)}";
    }

    public async Task StopLoggingAsync()
    {
        if (_logWriter is { } writer)
        {
            _logWriter = null;
            await writer.StopAsync();
        }
        IsLogging = false;
        LogStatus = string.Empty;
    }

    /// <summary>Persist the current session as .ftproj.</summary>
    public async Task SaveProjectAsync(string path)
    {
        var saved = await FtProjectSerializer.SaveAsync(Project, path);
        LastError = saved.IsOk ? string.Empty : saved.Error;
    }

    /// <summary>Load a .ftproj and apply it (macros immediately, pipeline if connected).</summary>
    public async Task LoadProjectAsync(string path)
    {
        var loaded = await FtProjectSerializer.LoadAsync(path);
        if (!loaded.IsOk)
        {
            LastError = loaded.Error;
            return;
        }
        Project = loaded.Value;
        ReloadMacros();
        await ApplyProjectAsync();
        LastError = string.Empty;
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
        RxText = rx.ToString("N0");
        TxText = tx.ToString("N0");
        FramesText = _frameCount.ToString("N0");
        ErrorsText = _errorCount.ToString("N0");
    }
}
