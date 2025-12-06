using Novus.Diagnostics;
using Novus.HIR;
using Novus.IR;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for HirValidator - validates HIR instructions before lowering.
/// </summary>
public class HirValidatorTests
{
    #region Copper List Validation Tests

    [Fact]
    public void ValidateCopperList_ValidWaitPositions_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(100, IrIntType.I32)),
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(150, IrIntType.I32)),
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(200, IrIntType.I32)),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateCopperList_WaitPositionOutOfRange_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics, ChipsetProfile.OCS);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            // Vertical position 400 is out of PAL range (0-312)
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(400, IrIntType.I32)),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateCopperList_NonMonotonicWaits_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(200, IrIntType.I32)),
            // Wait for earlier line than previous - invalid!
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(100, IrIntType.I32)),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("before previous WAIT"));
    }

    [Fact]
    public void ValidateCopperList_MoveToReadOnlyRegister_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            // DMACONR (0xDFF002) is read-only
            new HirCopperInstruction.Move(
                new IrConstant(0xDFF002, IrIntType.I32),
                new IrConstant(0x1234, IrIntType.I32)
            ),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("read-only"));
    }

    [Fact]
    public void ValidateCopperList_MoveToDangerousRegister_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            // COP1LCH (0xDFF080) is dangerous from copper list
            new HirCopperInstruction.Move(
                new IrConstant(0xDFF080, IrIntType.I32),
                new IrConstant(0x0000, IrIntType.I32)
            ),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("dangerous"));
    }

    [Fact]
    public void ValidateCopperList_ValidMoveToColorRegister_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            // COLOR00 (0xDFF180) is a valid writable register
            new HirCopperInstruction.Move(
                new IrConstant(0xDFF180, IrIntType.I32),
                new IrConstant(0x0F00, IrIntType.I32)
            ),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateCopperList_SkipPositionOutOfRange_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics, ChipsetProfile.OCS);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Skip(new IrConstant(300, IrIntType.I32), new IrConstant(100, IrIntType.I32)),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateCopperList_RuntimeValues_SkipsValidation()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        // Use a variable instead of a constant - can't validate at compile time
        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Wait(
                new IrVariable("x", IrIntType.I32),
                new IrVariable("y", IrIntType.I32)
            ),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        // Runtime values pass validation (can't check at compile time)
        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    #endregion

    #region Blitter Job Validation Tests

    [Fact]
    public void ValidateBlitterJob_ValidSimpleCopy_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "source", new IrVariable("src", new IrPointerType(IrIntType.U8)) },
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
            { "minterm", new IrConstant(0xF0, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateBlitterJob_WidthOutOfRange_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(2000, IrIntType.I32) }, // Out of range (max 1024)
            { "height", new IrConstant(64, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("width"));
    }

    [Fact]
    public void ValidateBlitterJob_HeightOutOfRange_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(0, IrIntType.I32) }, // Out of range (min 1)
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("height"));
    }

    [Fact]
    public void ValidateBlitterJob_NoDestination_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "source", new IrVariable("src", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("destination"));
    }

    [Fact]
    public void ValidateBlitterJob_WidthNotAlignedForOCS_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics, ChipsetProfile.OCS);

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(17, IrIntType.I32) }, // Not multiple of 16
            { "height", new IrConstant(64, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("multiple of 16"));
    }

    [Fact]
    public void ValidateBlitterJob_MintermRequiresMissingChannels_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        // Minterm 0xCA (cookie cut) requires A, B, and C channels
        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "source", new IrVariable("src", new IrPointerType(IrIntType.U8)) }, // Only channel A
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
            { "minterm", new IrConstant(0xCA, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("missing channels"));
    }

    [Fact]
    public void ValidateBlitterJob_CookieCutWithAllChannels_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        // Minterm 0xCA (cookie cut) requires A, B, and C channels
        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "sourcea", new IrVariable("srcA", new IrPointerType(IrIntType.U8)) },
            { "sourceb", new IrVariable("srcB", new IrPointerType(IrIntType.U8)) },
            { "sourcec", new IrVariable("srcC", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
            { "minterm", new IrConstant(0xCA, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    #endregion

    #region Async Function Validation Tests

    [Fact]
    public void ValidateAsyncFunction_ValidFunction_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "fetch_data",
            ReturnType = IrIntType.I32,
        };
        asyncFn.Parameters.Add(new IrParameter("url", new IrPointerType(IrIntType.U8)));
        asyncFn.AwaitPoints.Add(new AwaitPoint { StateNumber = 0, ResultVariable = "result" });
        asyncFn.AwaitPoints.Add(new AwaitPoint { StateNumber = 1, ResultVariable = "final" });

        var module = new IrModule();
        module.HirInstructions.Add(asyncFn);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateAsyncFunction_EmptyName_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "",
            ReturnType = IrIntType.I32,
        };

        var module = new IrModule();
        module.HirInstructions.Add(asyncFn);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("empty name"));
    }

    [Fact]
    public void ValidateAsyncFunction_NonSequentialStates_ReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var asyncFn = new HirAsyncFunction
        {
            FunctionName = "fetch_data",
            ReturnType = IrIntType.I32,
        };
        // Non-sequential state numbers
        asyncFn.AwaitPoints.Add(new AwaitPoint { StateNumber = 0, ResultVariable = "a" });
        asyncFn.AwaitPoints.Add(new AwaitPoint { StateNumber = 2, ResultVariable = "b" }); // Gap!
        asyncFn.AwaitPoints.Add(new AwaitPoint { StateNumber = 3, ResultVariable = "c" });

        var module = new IrModule();
        module.HirInstructions.Add(asyncFn);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, e => e.Message.Contains("non-sequential"));
    }

    #endregion

    #region Module Validation Tests

    [Fact]
    public void ValidateModule_EmptyModule_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var module = new IrModule();

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateModule_MultipleValidInstructions_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(100, IrIntType.I32)),
        });

        var blitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            { "dest", new IrVariable("dst", new IrPointerType(IrIntType.U8)) },
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);
        module.HirInstructions.Add(blitter);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    [Fact]
    public void ValidateModule_MixedValidAndInvalid_ReturnsFalse()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var validCopper = new HirCopperList(new List<HirCopperInstruction>
        {
            new HirCopperInstruction.Wait(new IrConstant(0, IrIntType.I32), new IrConstant(100, IrIntType.I32)),
        });

        var invalidBlitter = new HirBlitterJob(new Dictionary<string, IrValue>
        {
            // Missing destination
            { "width", new IrConstant(64, IrIntType.I32) },
            { "height", new IrConstant(64, IrIntType.I32) },
        });

        var module = new IrModule();
        module.HirInstructions.Add(validCopper);
        module.HirInstructions.Add(invalidBlitter);

        var result = validator.ValidateModule(module);

        Assert.False(result);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ValidateInstruction_UnknownInstructionType_ReturnsTrue()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        // Create a mock HirInstruction subclass
        var unknown = new MockHirInstruction();

        var result = validator.ValidateInstruction(unknown);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    private class MockHirInstruction : HirInstruction
    {
        public override List<IrInstruction> Lower() => new();
    }

    #endregion

    #region Cast Value Tests

    [Fact]
    public void ValidateCopperList_CastConstant_EvaluatesCorrectly()
    {
        var diagnostics = new DiagnosticBag();
        var validator = new HirValidator(diagnostics);

        var copper = new HirCopperList(new List<HirCopperInstruction>
        {
            // Use a cast value wrapping a constant
            new HirCopperInstruction.Wait(
                new IrCastValue(new IrConstant(0, IrIntType.I32), IrIntType.I32, IrIntType.I32),
                new IrCastValue(new IrConstant(100, IrIntType.I32), IrIntType.I32, IrIntType.I32)
            ),
        });

        var module = new IrModule();
        module.HirInstructions.Add(copper);

        var result = validator.ValidateModule(module);

        Assert.True(result);
        Assert.Empty(diagnostics.Diagnostics);
    }

    #endregion
}
