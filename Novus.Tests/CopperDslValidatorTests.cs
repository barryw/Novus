using Novus.Diagnostics;
using Novus.SemanticAnalysis;
using Novus.SemanticAnalysis.Validators;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for CopperDslValidator hardware validation.
/// </summary>
public class CopperDslValidatorTests
{
    private CopperDslValidator CreateValidator(ChipsetProfile chipset = ChipsetProfile.Auto)
    {
        return new CopperDslValidator { Chipset = chipset };
    }

    private SourceLocation DummyLocation => new("test.novus", 1, 1, 0, "");

    #region WAIT Position Validation

    [Fact]
    public void ValidateWait_OCS_ValidPosition_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(100, 50, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_OCS_MaxLine255_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(255, 0, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_OCS_Line256_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(256, 0, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == ErrorCodes.InvalidHardwareOperation);
    }

    [Fact]
    public void ValidateWait_ECS_Line256_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.ECS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(256, 0, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_ECS_Line312_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.ECS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(312, 0, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_ECS_Line313_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.ECS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(313, 0, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_Horizontal_MaxValue226_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(0, 226, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_Horizontal_227_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(0, 227, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWait_NegativeHorizontal_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateWait(0, -1, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region MOVE Validation

    [Fact]
    public void ValidateMove_ValidRegister_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // COLOR00 register
        var result = validator.ValidateMove(0xDFF180, 0x0F00, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMove_OutsideCustomChipRange_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // Outside $DFF000-$DFF1FF range
        var result = validator.ValidateMove(0xDFF200, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMove_DangerousRegister_COP1LCH_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // COP1LCH - dangerous to write from Copper
        var result = validator.ValidateMove(0xDFF080, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("dangerous"));
    }

    [Fact]
    public void ValidateMove_DangerousRegister_COPJMP1_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // COPJMP1 - can cause infinite loop
        var result = validator.ValidateMove(0xDFF088, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateMove_ReadOnlyRegister_VPOSR_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // VPOSR is read-only
        var result = validator.ValidateMove(0xDFF004, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("read-only"));
    }

    [Fact]
    public void ValidateMove_ReadOnlyRegister_JOY0DAT_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        // JOY0DAT is read-only
        var result = validator.ValidateMove(0xDFF00A, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region WAIT Sequence Validation

    [Fact]
    public void ValidateWaitSequence_Increasing_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>
        {
            (44, 0),
            (100, 50),
            (100, 100),
            (200, 0)
        };

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWaitSequence_SameLine_IncreasingHorizontal_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>
        {
            (100, 0),
            (100, 50),
            (100, 100),
            (100, 200)
        };

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWaitSequence_DecreasingVertical_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>
        {
            (100, 0),
            (50, 0)  // Goes backward!
        };

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("before previous"));
    }

    [Fact]
    public void ValidateWaitSequence_SameLine_DecreasingHorizontal_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>
        {
            (100, 100),
            (100, 50)  // Same line but horizontal goes backward
        };

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWaitSequence_EmptyList_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>();

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateWaitSequence_SingleEntry_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var positions = new List<(int vertical, int horizontal)>
        {
            (100, 50)
        };

        var result = validator.ValidateWaitSequence(positions, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Color Validation

    [Fact]
    public void ValidateColorMove_OCS_ValidIndex_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(0, 0x0F00, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_OCS_MaxIndex31_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(31, 0x0FFF, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_OCS_Index32_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(32, 0x0000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_AGA_Index255_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(255, 0xFFFFFF, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_AGA_Index256_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(256, 0x000000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_OCS_12BitColor_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(0, 0xFFF, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_OCS_13BitColor_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.OCS);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(0, 0x1000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_AGA_24BitColor_ReturnsTrue()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(0, 0xFFFFFF, diagnostics, DummyLocation);

        Assert.True(result);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateColorMove_AGA_25BitColor_ReportError()
    {
        var validator = CreateValidator(ChipsetProfile.AGA);
        var diagnostics = new DiagnosticBag();

        var result = validator.ValidateColorMove(0, 0x1000000, diagnostics, DummyLocation);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion
}
