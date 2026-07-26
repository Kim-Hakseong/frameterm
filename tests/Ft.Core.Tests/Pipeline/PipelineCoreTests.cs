using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Parsing;
using Ft.Core.Pipeline;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Pipeline;

public class PipelineCoreTests
{
    private static PipelineCore ModbusCore(FakeTimeSource time) => new(
        new PipelineConfig
        {
            // Modbus RTU read response: addr func count data... crc16 — use a
            // fixed-length request frame (8 bytes) for deterministic cutting.
            Framer = new FixedLengthFramer(8),
            ChecksumSpec = ChecksumPresets.Crc16Modbus,
            ChecksumPlacement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2),
            Fields = [new FieldSpec("func", 1, FieldType.U8)],
            Highlights =
            [
                new HighlightRule("#error", new FieldCondition("func", FieldOp.Gt, 0x7F)),
                new HighlightRule("#read", BytePattern.Parse("01 03").Value),
            ],
        },
        time);

    [Fact]
    public void Rx_GoodFrame_ChecksumOkFieldsAndColor()
    {
        var core = ModbusCore(new FakeTimeSource());
        var records = core.ProcessRx(Hex.Bytes("01 03 00 00 00 0A C5 CD"));

        var record = Assert.Single(records);
        Assert.Equal(FrameDirection.Rx, record.Direction);
        Assert.True(record.ChecksumOk);
        Assert.Equal(3, record.Fields[0].Numeric);
        Assert.Equal("#read", record.Color);
        Assert.Equal(1, record.Seq);
    }

    [Fact]
    public void Rx_CorruptFrame_ChecksumFail()
    {
        var core = ModbusCore(new FakeTimeSource());
        var records = core.ProcessRx(Hex.Bytes("01 03 00 00 00 0B C5 CD"));
        Assert.False(Assert.Single(records).ChecksumOk);
    }

    [Fact]
    public void Rx_OneByteInjection_SameResults()
    {
        var core = ModbusCore(new FakeTimeSource());
        var records = new List<FrameRecord>();
        foreach (byte b in Hex.Bytes("01 03 00 00 00 0A C5 CD"))
        {
            records.AddRange(core.ProcessRx(new[] { b }));
        }
        var record = Assert.Single(records);
        Assert.True(record.ChecksumOk);
        Assert.Equal("#read", record.Color);
    }

    [Fact]
    public void Tx_EnrichedThroughSamePath()
    {
        var core = ModbusCore(new FakeTimeSource());
        var record = core.EnrichTx(Hex.Bytes("01 03 00 00 00 0A C5 CD"));
        Assert.Equal(FrameDirection.Tx, record.Direction);
        Assert.True(record.ChecksumOk);
    }

    [Fact]
    public void NoFramer_ProducesNoRecords()
    {
        var core = new PipelineCore(new PipelineConfig(), new FakeTimeSource());
        Assert.Empty(core.ProcessRx(Hex.Bytes("01 02 03")));
        Assert.Empty(core.FlushTimeouts());
    }

    [Fact]
    public void SilenceGapFramer_FlushTimeouts_EmitsViaFakeClock()
    {
        var time = new FakeTimeSource();
        var core = new PipelineCore(
            new PipelineConfig { Framer = new SilenceGapFramer(10, time) },
            time);

        Assert.Empty(core.ProcessRx(Hex.Bytes("AA BB")));
        Assert.Empty(core.FlushTimeouts());
        time.Advance(15);
        var records = core.FlushTimeouts();
        Assert.Equal(Hex.Bytes("AA BB"), Assert.Single(records).Raw);
    }

    [Fact]
    public void NoChecksumConfigured_ChecksumOkIsNull()
    {
        var core = new PipelineCore(
            new PipelineConfig { Framer = new FixedLengthFramer(2) },
            new FakeTimeSource());
        var records = core.ProcessRx(Hex.Bytes("01 02"));
        Assert.Null(Assert.Single(records).ChecksumOk);
    }
}
