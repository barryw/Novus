using Novus.CCompiler;
using Novus.Codegen.M68k;
using Novus.Assembler;
using Novus.IR;

namespace Novus.Tests;

public sealed class CFrontendTests
{
    [Fact]
    public void LowersCArithmeticIntoExistingM68kBackend()
    {
        var module = new CFrontend("int add(int a, int b) { return a + b; }").Parse();
        var assembly = new M68kCodeGenerator(module, [], "68020").Generate();

        Assert.Single(module.Functions);
        Assert.Contains("add:", assembly);
        Assert.Contains("add.l", assembly);
        Assert.Contains("4(sp)", assembly);
        Assert.Contains("8(sp)", assembly);
        Assert.DoesNotContain("8(sp),d1", assembly);
        Assert.Contains("add.l   8(sp),d0", assembly);
        Assert.DoesNotContain("_str_file", assembly);
        Assert.DoesNotContain("link      a5", assembly);
        Assert.DoesNotContain("move.l   d0,-", assembly);
        Assert.NotEmpty(new M68kAssembler().Assemble(assembly, "add.s"));
    }

    [Fact]
    public void SupportsAmigaIntegerWidthsAndUsefulDiagnostics()
    {
        var module = new CFrontend("unsigned long mask(unsigned short value) { return value | 4; }").Parse();

        Assert.Equal("u32", module.Functions[0].ReturnType.Name);
        Assert.Equal("u16", module.Functions[0].Parameters[0].Type.Name);
        var error = Assert.Throws<FormatException>(() => new CFrontend("int nope(void) { wat; }").Parse());
        Assert.Contains("line 1", error.Message);
    }

    [Fact]
    public void LowersLocalDeclarationsAndAssignments()
    {
        var module = new CFrontend("int bump(int value) { int result = value + 1; result = result + 1; return result; }").Parse();
        var assembly = new M68kCodeGenerator(module, [], "68020").Generate();

        Assert.DoesNotContain("link      a5", assembly);
        Assert.DoesNotContain("move.l   d0,-", assembly);
        Assert.Equal(2, assembly.Split("addq.l").Length - 1);
        Assert.NotEmpty(new M68kAssembler().Assemble(assembly, "bump.s"));
    }

    [Fact]
    public void EmitsRelocatableCallsUsingTheStackAbi()
    {
        var module = new CFrontend("int external(void); int answer(void) { return external(); }").Parse();
        var assembly = new M68kCodeGenerator(module, [], "68020").Generate();
        var objectBytes = new M68kAssembler().Assemble(assembly, "caller.s");

        Assert.Contains("XREF      external", assembly);
        Assert.Contains("jsr       external", assembly);
        Assert.DoesNotContain("move.l   d0,-", assembly);
        Assert.NotEmpty(objectBytes);
    }

    [Fact]
    public void PassesCArgumentsOnTheStackAndCleansThemUp()
    {
        var module = new CFrontend("int external(int value); int caller(int value) { return external(value); }").Parse();
        var assembly = new M68kCodeGenerator(module, [], "68020").Generate();

        Assert.Contains("move.l   4(sp),d0", assembly);
        Assert.Contains("move.l   d0,-(sp)", assembly);
        Assert.Contains("addq.l    #4,sp", assembly);
        Assert.NotEmpty(new M68kAssembler().Assemble(assembly, "caller.s"));
    }

    [Fact]
    public void AdjustsFramelessParameterOffsetsWhilePushingMultipleArguments()
    {
        var module = new CFrontend("int external(int a, int b); int caller(int a, int b) { return external(a, b); }").Parse();
        var assembly = new M68kCodeGenerator(module, [], "68020").Generate();

        Assert.Equal(2, assembly.Split("move.l   8(sp),d0").Length - 1);
        Assert.Contains("addq.l    #8,sp", assembly);
        Assert.NotEmpty(new M68kAssembler().Assemble(assembly, "caller2.s"));
    }
}
