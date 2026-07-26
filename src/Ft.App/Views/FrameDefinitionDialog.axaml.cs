using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft.Core.Project;
using Ft.Core.Time;

namespace Ft.App.Views;

public partial class FrameDefinitionDialog : Window
{
    private readonly FtProject _project;
    private readonly ObservableCollection<FieldConfig> _fields;
    private readonly ObservableCollection<HighlightConfig> _highlights;

    /// <summary>True when the user applied a valid configuration.</summary>
    public bool Applied { get; private set; }

    // Designer constructor.
    public FrameDefinitionDialog() : this(new FtProject())
    {
    }

    public FrameDefinitionDialog(FtProject project)
    {
        InitializeComponent();
        _project = project;
        _fields = new ObservableCollection<FieldConfig>(project.Fields);
        _highlights = new ObservableCollection<HighlightConfig>(project.Highlights);
        FieldsGrid.ItemsSource = _fields;
        HighlightsGrid.ItemsSource = _highlights;
        LoadFromProject();
    }

    private void LoadFromProject()
    {
        var framing = _project.Framing;
        ModeCombo.SelectedIndex = framing.Mode.ToLowerInvariant() switch
        {
            "delimiter" => 1,
            "fixedlength" => 2,
            "lengthfield" => 3,
            "silencegap" => 4,
            _ => 0,
        };
        StartHexBox.Text = framing.StartHex;
        EndHexBox.Text = framing.EndHex;
        EscapeHexBox.Text = framing.EscapeHex;
        FixedLenBox.Text = framing.Length.ToString();
        HeaderLenBox.Text = framing.HeaderLen.ToString();
        LenOffsetBox.Text = framing.LenOffset.ToString();
        LenSizeBox.Text = framing.LenSize.ToString();
        LenEndianCombo.SelectedIndex = framing.Endian.Equals("BE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        LenAdjustBox.Text = framing.LenAdjust.ToString();
        GapMsBox.Text = framing.GapMs.ToString();

        var checksum = _project.Checksum;
        PresetCombo.SelectedIndex = checksum.Preset.ToUpperInvariant() switch
        {
            "CRC16_MODBUS" => 1,
            "CRC16_CCITT_FALSE" => 2,
            "CRC32" => 3,
            "CRC8" => 4,
            "XOR8" => 5,
            "SUM8" => 6,
            _ => 0,
        };
        OffsetFromEndBox.Text = checksum.OffsetFromEnd.ToString();
        ChecksumEndianCombo.SelectedIndex = checksum.ByteOrder.Equals("BE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        CoverageStartBox.Text = checksum.CoverageStart.ToString();
        CoverageEndBox.Text = checksum.CoverageEndOffsetFromEnd.ToString();

        UpdatePanelVisibility();
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e) => UpdatePanelVisibility();

    private void UpdatePanelVisibility()
    {
        if (DelimiterPanel is null) return; // during InitializeComponent
        int mode = ModeCombo.SelectedIndex;
        DelimiterPanel.IsVisible = mode == 1;
        FixedPanel.IsVisible = mode == 2;
        LengthFieldPanel.IsVisible = mode == 3;
        SilencePanel.IsVisible = mode == 4;
    }

    private void OnAddFieldClick(object? sender, RoutedEventArgs e) =>
        _fields.Add(new FieldConfig { Name = $"field{_fields.Count + 1}" });

    private void OnRemoveFieldClick(object? sender, RoutedEventArgs e)
    {
        if (FieldsGrid.SelectedItem is FieldConfig field) _fields.Remove(field);
    }

    private void OnAddHighlightClick(object? sender, RoutedEventArgs e) =>
        _highlights.Add(new HighlightConfig());

    private void OnRemoveHighlightClick(object? sender, RoutedEventArgs e)
    {
        if (HighlightsGrid.SelectedItem is HighlightConfig highlight) _highlights.Remove(highlight);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (!TryReadInt(FixedLenBox, "Frame length", out int fixedLen) ||
            !TryReadInt(HeaderLenBox, "Header length", out int headerLen) ||
            !TryReadInt(LenOffsetBox, "Len offset", out int lenOffset) ||
            !TryReadInt(LenSizeBox, "Len size", out int lenSize) ||
            !TryReadInt(LenAdjustBox, "Len adjust", out int lenAdjust) ||
            !TryReadInt(GapMsBox, "Silence gap", out int gapMs) ||
            !TryReadInt(OffsetFromEndBox, "Offset from end", out int offsetFromEnd) ||
            !TryReadInt(CoverageStartBox, "Coverage start", out int coverageStart) ||
            !TryReadInt(CoverageEndBox, "Coverage end offset", out int coverageEnd))
        {
            return;
        }

        var candidate = new FtProject
        {
            Transport = _project.Transport,
            Framing = new FramingConfig
            {
                Mode = ModeCombo.SelectedIndex switch
                {
                    1 => "Delimiter",
                    2 => "FixedLength",
                    3 => "LengthField",
                    4 => "SilenceGap",
                    _ => "None",
                },
                StartHex = StartHexBox.Text ?? string.Empty,
                EndHex = EndHexBox.Text ?? string.Empty,
                EscapeHex = EscapeHexBox.Text ?? string.Empty,
                Length = fixedLen,
                HeaderLen = headerLen,
                LenOffset = lenOffset,
                LenSize = lenSize,
                Endian = LenEndianCombo.SelectedIndex == 1 ? "BE" : "LE",
                LenAdjust = lenAdjust,
                GapMs = gapMs,
            },
            Checksum = new ChecksumConfig
            {
                Preset = (PresetCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None",
                OffsetFromEnd = offsetFromEnd,
                ByteOrder = ChecksumEndianCombo.SelectedIndex == 1 ? "BE" : "LE",
                CoverageStart = coverageStart,
                CoverageEndOffsetFromEnd = coverageEnd,
            },
            Fields = [.. _fields],
            Highlights = [.. _highlights],
            Macros = _project.Macros,
        };

        var built = candidate.BuildPipelineConfig(SystemTimeSource.Instance);
        if (!built.IsOk)
        {
            ShowError(built.Error);
            return;
        }

        _project.Framing = candidate.Framing;
        _project.Checksum = candidate.Checksum;
        _project.Fields = candidate.Fields;
        _project.Highlights = candidate.Highlights;
        Applied = true;
        Close();
    }

    private bool TryReadInt(TextBox box, string label, out int value)
    {
        if (int.TryParse(box.Text, out value)) return true;
        ShowError($"{label} must be an integer.");
        return false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
