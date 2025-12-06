using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;
using System.Linq;

namespace Novus.Tests;

/// <summary>
/// Tests for inline assembly support in Novus.
/// Tests parsing, IR generation, and C code generation for asm blocks.
/// </summary>
public class InlineAssemblyTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    #region Basic Parsing Tests

    [Fact]
    public void InlineAsm_SimpleNoInputsNoOutputs_Parses()
    {
        var source = @"
pub fn nop_fn() {
    unsafe asm() {
        ""nop""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        var function = module.Functions.FirstOrDefault(f => f.Name == "nop_fn");
        Assert.NotNull(function);
    }

    [Fact]
    public void InlineAsm_WithSingleInput_Parses()
    {
        var source = @"
pub fn use_value(x: u32) {
    unsafe asm(x) {
        ""move.l %x,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithMultipleInputs_Parses()
    {
        var source = @"
pub fn add_values(a: u32, b: u32) {
    unsafe asm(a, b) {
        ""add.l %a,%b""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithExplicitRegisterBindings_Parses()
    {
        var source = @"
pub fn use_specific_regs(x: u32, y: *u8) {
    unsafe asm(x in d0, y in a0) {
        ""move.l %x,(%y)""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithSingleReturn_Parses()
    {
        var source = @"
pub fn get_value() -> u32 {
    return unsafe asm() -> u32 {
        ""move.l #42,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithReturnInSpecificRegister_Parses()
    {
        var source = @"
pub fn get_value() -> u32 {
    return unsafe asm() -> u32 in d0 {
        ""move.l #42,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithVolatile_Parses()
    {
        var source = @"
pub fn hardware_read() -> u16 {
    return unsafe asm() -> u16 in d0 volatile {
        ""move.w 0xDFF00A,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithClobbers_Parses()
    {
        var source = @"
pub fn clobbering_fn(x: u32) -> u32 {
    return unsafe asm(x in d0) -> u32 clobbers(d2, d3) {
        ""move.l %x,d2""
        ""add.l d2,d2""
        ""move.l d2,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithMemoryClobber_Parses()
    {
        var source = @"
pub fn memory_barrier() {
    unsafe asm() volatile clobbers(memory) {
        ""nop""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithMixedClobbers_Parses()
    {
        var source = @"
pub fn complex_fn(ptr: *u8, value: u32) {
    unsafe asm(ptr in a0, value in d0) volatile clobbers(d2, memory) {
        ""move.l %value,d2""
        ""move.l d2,(%ptr)""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Multi-line Instruction Tests

    [Fact]
    public void InlineAsm_MultipleInstructions_Parses()
    {
        var source = @"
pub fn swap_bytes(value: u32) -> u32 {
    return unsafe asm(value in d0) -> u32 {
        ""ror.w #8,d0""
        ""swap d0""
        ""ror.w #8,d0""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void InlineAsm_WithLabels_Parses()
    {
        var source = @"
pub fn countdown(n: u32) -> u32 {
    return unsafe asm(n in d0) -> u32 {
        ""tst.l d0""
        ""beq.s done""
        ""loop: subq.l #1,d0""
        ""bne.s loop""
        ""done:""
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    #endregion

    #region IR Generation Tests

    [Fact]
    public void InlineAsm_GeneratesIrInlineAsmInstruction()
    {
        var source = @"
pub fn test_fn(x: u32) -> u32 {
    return unsafe asm(x in d0) -> u32 {
        ""add.l d0,d0""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        // Check that an IrInlineAsm instruction was generated
        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Single(inlineAsm.Inputs);
        Assert.Single(inlineAsm.Outputs);
        Assert.Single(inlineAsm.Instructions);
    }

    [Fact]
    public void InlineAsm_InputsHaveCorrectRegisterBindings()
    {
        var source = @"
pub fn test_fn(a: u32, b: *u8) {
    unsafe asm(a in d1, b in a1) {
        ""nop""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Equal(2, inlineAsm.Inputs.Count);

        var inputA = inlineAsm.Inputs.FirstOrDefault(i => i.Name == "a");
        Assert.NotNull(inputA);
        Assert.Equal(M68kRegister.D1, inputA.Register);

        var inputB = inlineAsm.Inputs.FirstOrDefault(i => i.Name == "b");
        Assert.NotNull(inputB);
        Assert.Equal(M68kRegister.A1, inputB.Register);
    }

    [Fact]
    public void InlineAsm_OutputHasCorrectType()
    {
        var source = @"
pub fn test_fn() -> u16 {
    return unsafe asm() -> u16 in d0 {
        ""move.w #100,d0""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Single(inlineAsm.Outputs);
        Assert.Equal(M68kRegister.D0, inlineAsm.Outputs[0].Register);
        Assert.IsType<IrIntType>(inlineAsm.Outputs[0].Type);
    }

    [Fact]
    public void InlineAsm_VolatileFlagSetCorrectly()
    {
        var source = @"
pub fn test_fn() {
    unsafe asm() volatile {
        ""nop""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.True(inlineAsm.IsVolatile);
    }

    [Fact]
    public void InlineAsm_ClobbersRecordedCorrectly()
    {
        var source = @"
pub fn test_fn() {
    unsafe asm() clobbers(d2, d3, a2, memory) {
        ""nop""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Contains(M68kRegister.D2, inlineAsm.Clobbers);
        Assert.Contains(M68kRegister.D3, inlineAsm.Clobbers);
        Assert.Contains(M68kRegister.A2, inlineAsm.Clobbers);
        Assert.True(inlineAsm.ClobbersMemory);
    }

    #endregion

    #region Parameter Substitution Tests

    [Fact]
    public void InlineAsm_SubstitutesParametersCorrectly()
    {
        var inlineAsm = new IrInlineAsm
        {
            Inputs =
            {
                new IrAsmInput("value", new IrVariable("value", IrIntType.I32), IrIntType.I32, M68kRegister.D0),
                new IrAsmInput("ptr", new IrVariable("ptr", new IrPointerType(IrIntType.U8)), new IrPointerType(IrIntType.U8), M68kRegister.A0)
            },
            Instructions = { "move.l %value,(%ptr)" }
        };

        var substituted = inlineAsm.SubstituteParameters("move.l %value,(%ptr)");
        Assert.Equal("move.l d0,(a0)", substituted);
    }

    [Fact]
    public void InlineAsm_SubstitutionPreservesNonParameterText()
    {
        var inlineAsm = new IrInlineAsm
        {
            Inputs =
            {
                new IrAsmInput("x", new IrVariable("x", IrIntType.I32), IrIntType.I32, M68kRegister.D0)
            },
            Instructions = { "add.l #100,%x" }
        };

        var substituted = inlineAsm.SubstituteParameters("add.l #100,%x");
        Assert.Equal("add.l #100,d0", substituted);
    }

    #endregion

    #region Default Register Inference Tests

    [Fact]
    public void InlineAsm_InfersDataRegisterForIntegerType()
    {
        var source = @"
pub fn test_fn(x: u32) {
    unsafe asm(x) {
        ""nop""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Single(inlineAsm.Inputs);
        // First integer param goes to d0
        Assert.Equal(M68kRegister.D0, inlineAsm.Inputs[0].Register);
    }

    [Fact]
    public void InlineAsm_InfersAddressRegisterForPointerType()
    {
        var source = @"
pub fn test_fn(ptr: *u8) {
    unsafe asm(ptr) {
        ""nop""
    }
}";
        var module = BuildIr(source);
        var function = module.Functions.FirstOrDefault(f => f.Name == "test_fn");
        Assert.NotNull(function);

        var inlineAsm = function.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrInlineAsm>()
            .FirstOrDefault();

        Assert.NotNull(inlineAsm);
        Assert.Single(inlineAsm.Inputs);
        // First pointer param goes to a0
        Assert.Equal(M68kRegister.A0, inlineAsm.Inputs[0].Register);
    }

    #endregion
}
