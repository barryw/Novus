using Novus.Diagnostics;
using Novus.SemanticAnalysis;
using Novus.SemanticAnalysis.Validators;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for BlitterDslValidator hardware validation.
/// </summary>
public class BlitterDslValidatorTests
{
    private BlitterDslValidator CreateValidator(ChipsetProfile chipset = ChipsetProfile.Auto)
    {
        return new BlitterDslValidator { Chipset = chipset };
    }

    private SourceLocation DummyLocation => new("test.novus", 1, 1, 0, "");

    #region Size Validation

    [Fact]
    public void ValidateSize_ValidDimensions_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(16, 100, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateSize_MaxDimensions_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(1024, 1024, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateSize_WidthTooLarge_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(1025, 100, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == ErrorCodes.BlitterSizeOutOfRange);
    }

    [Fact]
    public void ValidateSize_HeightTooLarge_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(16, 1025, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateSize_OCS_NonWordAlignedWidth_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(17, 100, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("multiple of 16"));
    }

    [Fact]
    public void ValidateSize_AGA_NonWordAlignedWidth_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateSize(17, 100, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Minterm Validation

    [Fact]
    public void ValidateMinterm_CopyA_WithSourceA_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xF0 = Copy A
        var result = validator.ValidateMinterm(0xF0, hasSourceA: true, hasSourceB: false, hasSourceC: false, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_CopyA_WithoutSourceA_ReportError()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xF0 = Copy A (requires source A)
        var result = validator.ValidateMinterm(0xF0, hasSourceA: false, hasSourceB: false, hasSourceC: false, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("Source A required"));
    }

    [Fact]
    public void ValidateMinterm_CopyB_WithSourceB_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xCC = Copy B
        var result = validator.ValidateMinterm(0xCC, hasSourceA: false, hasSourceB: true, hasSourceC: false, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_CopyC_WithSourceC_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xAA = Copy C
        var result = validator.ValidateMinterm(0xAA, hasSourceA: false, hasSourceB: false, hasSourceC: true, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_Clear_NoSourcesRequired_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0x00 = Clear (no sources required)
        var result = validator.ValidateMinterm(0x00, hasSourceA: false, hasSourceB: false, hasSourceC: false, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_Set_NoSourcesRequired_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xFF = Set (all ones, no sources required)
        var result = validator.ValidateMinterm(0xFF, hasSourceA: false, hasSourceB: false, hasSourceC: false, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_PatternFill_RequiresAllChannels()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xCA = Pattern fill (A OR (B AND C)) - uses A, B, C
        var result = validator.ValidateMinterm(0xCA, hasSourceA: true, hasSourceB: true, hasSourceC: true, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMinterm_PatternFill_MissingChannel_ReportError()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // 0xCA = Pattern fill - requires A, B, C but only A provided
        var result = validator.ValidateMinterm(0xCA, hasSourceA: true, hasSourceB: false, hasSourceC: false, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region Alignment Validation

    [Fact]
    public void ValidateAlignment_OCS_WordAligned_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateAlignment(0x00020000, "source", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateAlignment_OCS_NotWordAligned_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateAlignment(0x00020001, "source", diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == ErrorCodes.BlitterWidthNotAligned);
    }

    [Fact]
    public void ValidateAlignment_AGA_OddAddress_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateAlignment(0x00020001, "source", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Modulo Validation

    [Fact]
    public void ValidateModulo_ValidRange_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(40, "A", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_NegativeValue_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(-40, "A", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_MaxValue_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(32766, "A", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_MinValue_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(-32768, "A", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_TooLarge_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(32768, "A", diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_TooSmall_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(-32769, "A", diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateModulo_OCS_OddValue_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(41, "A", diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("word-aligned"));
    }

    [Fact]
    public void ValidateModulo_AGA_OddValue_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateModulo(41, "A", diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Line Mode Validation

    [Fact]
    public void ValidateLineMode_ShortLine_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateLineMode(0, 0, 100, 50, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateLineMode_MaxLength_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateLineMode(0, 0, 2047, 0, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateLineMode_TooLong_ReportError()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateLineMode(0, 0, 2048, 0, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("2047"));
    }

    [Fact]
    public void ValidateLineMode_DiagonalLine_UsesMaxDelta()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // Diagonal from (0,0) to (1000,2047) - uses max(dx, dy) = 2047
        var result = validator.ValidateLineMode(0, 0, 1000, 2047, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateLineMode_NegativeCoordinates_ReturnsTrue()
    {
        var validator = CreateValidator();
        var diagnostics = new DiagnosticBag();

        // Line with negative start point
        var result = validator.ValidateLineMode(-100, -50, 100, 50, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion
}
