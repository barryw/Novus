using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Optimizer.Passes;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for module-level optimization passes:
/// - DeadFunctionEliminationPass
/// - Function inlining with @inline/@noinline attributes
/// </summary>
public class ModuleLevelOptimizationTests
{
    private IrModule BuildIR(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    #region DeadFunctionEliminationPass Tests

    [Fact]
    public void DeadFunctionElimination_EmptyModule_NoChanges()
    {
        var pass = new DeadFunctionEliminationPass();
        var module = new IrModule();

        var changed = pass.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void DeadFunctionElimination_OnlyMainFunction_NoChanges()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var pass = new DeadFunctionEliminationPass();
        var initialCount = module.Functions.Count;

        var changed = pass.Run(module);

        Assert.False(changed);
        Assert.Equal(initialCount, module.Functions.Count);
    }

    [Fact]
    public void DeadFunctionElimination_UnusedFunction_Removed()
    {
        var source = @"
pub fn main() -> i32 {
    return used_function()
}

fn used_function() -> i32 {
    return 42
}

fn unused_function() -> i32 {
    return 99
}";

        var module = BuildIR(source);
        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        Assert.True(changed);
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused_function");
        Assert.Contains(module.Functions, f => f.Name == "main");
        Assert.Contains(module.Functions, f => f.Name == "used_function");
    }

    [Fact]
    public void DeadFunctionElimination_TransitivelyUsedFunctions_NotRemoved()
    {
        var source = @"
pub fn main() -> i32 {
    return level1()
}

fn level1() -> i32 {
    return level2()
}

fn level2() -> i32 {
    return level3()
}

fn level3() -> i32 {
    return 123
}

fn unused() -> i32 {
    return 456
}";

        var module = BuildIR(source);
        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        Assert.True(changed);
        Assert.Contains(module.Functions, f => f.Name == "main");
        Assert.Contains(module.Functions, f => f.Name == "level1");
        Assert.Contains(module.Functions, f => f.Name == "level2");
        Assert.Contains(module.Functions, f => f.Name == "level3");
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused");
    }

    [Fact]
    public void DeadFunctionElimination_ExportedFunction_NotRemoved()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);

        // Add an exported but unused function manually
        var exportedFunc = new IrFunction("exported_unused", IrIntType.I32, Visibility.Public);
        exportedFunc.IsExported = true;
        exportedFunc.BasicBlocks.Add(new IrBasicBlock("entry"));
        module.AddFunction(exportedFunc);

        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        // exported_unused should NOT be removed even though it's not called
        Assert.False(changed);
        Assert.Contains(module.Functions, f => f.Name == "exported_unused");
    }

    [Fact]
    public void DeadFunctionElimination_PublicFunction_NotRemoved()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);

        // Add a public but unused function manually
        var publicFunc = new IrFunction("public_unused", IrIntType.I32, Visibility.Public);
        publicFunc.BasicBlocks.Add(new IrBasicBlock("entry"));
        module.AddFunction(publicFunc);

        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        // public_unused should NOT be removed (it's public, may be called externally)
        Assert.False(changed);
        Assert.Contains(module.Functions, f => f.Name == "public_unused");
    }

    [Fact]
    public void DeadFunctionElimination_ExternFunction_NotRemoved()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);

        // Add an extern function manually
        var externFunc = new IrFunction("extern_func", IrIntType.I32, Visibility.Private, isExtern: true);
        module.AddFunction(externFunc);

        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        // extern_func should NOT be removed (it's a declaration, not a definition)
        Assert.False(changed);
        Assert.Contains(module.Functions, f => f.Name == "extern_func");
    }

    [Fact]
    public void DeadFunctionElimination_MultipleUnusedFunctions_AllRemoved()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}

fn unused1() -> i32 {
    return 1
}

fn unused2() -> i32 {
    return unused3()
}

fn unused3() -> i32 {
    return 3
}";

        var module = BuildIR(source);
        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        Assert.True(changed);
        Assert.Contains(module.Functions, f => f.Name == "main");
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused1");
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused2");
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused3");
    }

    [Fact]
    public void DeadFunctionElimination_CallGraphWithCycle_HandlesCorrectly()
    {
        var source = @"
pub fn main() -> i32 {
    return used1()
}

fn used1() -> i32 {
    return used2()
}

fn used2() -> i32 {
    return used1()  // Cycle
}

fn unused() -> i32 {
    return 99
}";

        var module = BuildIR(source);
        var pass = new DeadFunctionEliminationPass();

        var changed = pass.Run(module);

        Assert.True(changed);
        Assert.Contains(module.Functions, f => f.Name == "main");
        Assert.Contains(module.Functions, f => f.Name == "used1");
        Assert.Contains(module.Functions, f => f.Name == "used2");
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused");
    }

    [Fact]
    public void DeadFunctionElimination_Name_ReturnsCorrectName()
    {
        var pass = new DeadFunctionEliminationPass();

        Assert.Equal("Dead Function Elimination", pass.Name);
    }

    #endregion

    #region Inline Attribute Tests

    [Fact]
    public void InlineExpansion_WithInlineAttribute_AlwaysInlines()
    {
        var source = @"
fn small() -> i32 {
    return 1
}

pub fn main() -> i32 {
    return small()
}";

        var module = BuildIR(source);

        // Manually add @inline attribute to small function
        var smallFunc = module.Functions.First(f => f.Name == "small");
        var attrs = new AttributeCollection();
        attrs.Add(new AttributeInfo(KnownAttributes.Inline, new Novus.Diagnostics.SourceLocation("test", 1, 1, 1, "")));
        smallFunc.Attributes = attrs;

        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // small should be inlined due to @inline attribute
        Assert.True(changed);
    }

    [Fact]
    public void InlineExpansion_WithNoInlineAttribute_NeverInlines()
    {
        var source = @"
fn small() -> i32 {
    return 1
}

pub fn main() -> i32 {
    return small()
}";

        var module = BuildIR(source);

        // Manually add @noinline attribute to small function
        var smallFunc = module.Functions.First(f => f.Name == "small");
        var attrs = new AttributeCollection();
        attrs.Add(new AttributeInfo(KnownAttributes.NoInline, new Novus.Diagnostics.SourceLocation("test", 1, 1, 1, "")));
        smallFunc.Attributes = attrs;

        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // small should NOT be inlined due to @noinline attribute
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansion_LargeFunctionWithInlineAttribute_Inlines()
    {
        var source = @"
fn large() -> i32 {
    var a: i32 = 1
    var b: i32 = 2
    var c: i32 = 3
    var d: i32 = 4
    var e: i32 = 5
    return a + b + c + d + e
}

pub fn main() -> i32 {
    return large()
}";

        var module = BuildIR(source);

        // Manually add @inline attribute to large function
        var largeFunc = module.Functions.First(f => f.Name == "large");
        var attrs = new AttributeCollection();
        attrs.Add(new AttributeInfo(KnownAttributes.Inline, new Novus.Diagnostics.SourceLocation("test", 1, 1, 1, "")));
        largeFunc.Attributes = attrs;

        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // large should be inlined despite size, due to @inline attribute
        Assert.True(changed);
    }

    [Fact]
    public void InlineExpansion_RecursiveWithInlineAttribute_DoesNotInline()
    {
        var source = @"
pub fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}

pub fn main() -> i32 {
    return factorial(5)
}";

        var module = BuildIR(source);

        // Manually add @inline attribute to factorial
        var factorialFunc = module.Functions.First(f => f.Name == "factorial");
        var attrs = new AttributeCollection();
        attrs.Add(new AttributeInfo(KnownAttributes.Inline, new Novus.Diagnostics.SourceLocation("test", 1, 1, 1, "")));
        factorialFunc.Attributes = attrs;

        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // factorial should NOT be inlined (recursive functions never inline)
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansion_ExportedFunction_DoesNotInline()
    {
        var source = @"
pub fn exported() -> i32 {
    return 42
}

pub fn main() -> i32 {
    return exported()
}";

        var module = BuildIR(source);

        // Mark exported as exported
        var exportedFunc = module.Functions.First(f => f.Name == "exported");
        exportedFunc.IsExported = true;

        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // exported should NOT be inlined (it's exported, may be called externally)
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansion_PublicFunction_DoesNotInline()
    {
        var source = @"
pub fn public_func() -> i32 {
    return 42
}

pub fn main() -> i32 {
    return public_func()
}";

        var module = BuildIR(source);

        // public_func is already public due to 'pub' keyword
        var pass = new Novus.Transforms.Passes.InlineExpansionPass();

        var changed = pass.Transform(module);

        // public_func should NOT be inlined (it's public)
        Assert.False(changed);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_InliningThenDeadCodeElimination()
    {
        var source = @"
fn helper1() -> i32 {
    return 10
}

fn helper2() -> i32 {
    return 20
}

fn unused_helper() -> i32 {
    return 30
}

pub fn main() -> i32 {
    return helper1() + helper2()
}";

        var module = BuildIR(source);

        // First inline small functions
        var inlinePass = new Novus.Transforms.Passes.InlineExpansionPass();
        var inlineChanged = inlinePass.Transform(module);
        Assert.True(inlineChanged);

        // After inlining, helper1 and helper2 might be unused
        // (depends on if they're called elsewhere)

        // Then eliminate dead functions
        var dcePass = new DeadFunctionEliminationPass();
        var dceChanged = dcePass.Run(module);

        // unused_helper should be removed
        Assert.DoesNotContain(module.Functions, f => f.Name == "unused_helper");
        Assert.Contains(module.Functions, f => f.Name == "main");
    }

    [Fact]
    public void IntegrationTest_ComplexCallGraph()
    {
        var source = @"
pub fn main() -> i32 {
    return chain1()
}

fn chain1() -> i32 {
    return chain2() + chain3()
}

fn chain2() -> i32 {
    return leaf1()
}

fn chain3() -> i32 {
    return leaf2()
}

fn leaf1() -> i32 {
    return 1
}

fn leaf2() -> i32 {
    return 2
}

fn orphan1() -> i32 {
    return orphan2()
}

fn orphan2() -> i32 {
    return 99
}";

        var module = BuildIR(source);

        // First inline small leaf functions
        var inlinePass = new Novus.Transforms.Passes.InlineExpansionPass();
        inlinePass.Transform(module);

        // Then eliminate dead functions
        var dcePass = new DeadFunctionEliminationPass();
        var changed = dcePass.Run(module);

        Assert.True(changed);
        // orphan1 and orphan2 should be removed
        Assert.DoesNotContain(module.Functions, f => f.Name == "orphan1");
        Assert.DoesNotContain(module.Functions, f => f.Name == "orphan2");
        // All functions in the main call chain should remain
        Assert.Contains(module.Functions, f => f.Name == "main");
    }

    #endregion
}
