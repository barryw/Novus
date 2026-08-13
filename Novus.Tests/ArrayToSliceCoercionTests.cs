using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Optimizer.Passes;
using Xunit;

namespace Novus.Tests;

public class ArrayToSliceCoercionTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: false); // Enable stdlib to get intuition
        return builder.BuildModule(tree);
    }

    [Fact]
    public void TestArrayToSliceCoercion_WindowHandleOpen()
    {
        string source = @"
from intuition import WindowHandle, TagItem

pub fn test_func() -> i32 {
    let tags = [
        TagItem { ti_Tag: 0, ti_Data: 0 },
        TagItem { ti_Tag: 0, ti_Data: 0 }
    ]

    let result = WindowHandle::open(&tags)

    return 0
}
";

        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void Slice_IndexOperator_LowersToCheckedDynamicIndex()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn first(values: Slice<i32>) -> i32 {
    return *values[0]
}
");

        var first = Assert.Single(module.Functions, function => function.Name == "first");
        var access = Assert.Single(first.BasicBlocks.SelectMany(block => block.Instructions)
            .OfType<IrIndexAccess>());
        Assert.Equal(IrBoundsCheckMode.Checked, access.BoundsCheck);
        Assert.IsType<IrVariable>(access.Length);
    }

    [Fact]
    public void Slice_RangeLowersToOneBoundsCheckAndAProvenOffset()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn middle(values: Slice<i32>, start: u32, end: u32) -> Slice<i32> {
    return values[start..end]
}
");

        var instructions = module.Functions.Single(function => function.Name == "middle")
            .BasicBlocks.SelectMany(block => block.Instructions).ToList();
        Assert.Equal(IrBoundsCheckMode.Checked,
            Assert.Single(instructions.OfType<IrSliceBoundsCheck>()).BoundsCheck);
        Assert.Empty(instructions.OfType<IrIndexAccess>());
        var returnedSlice = Assert.IsType<IrStructLiteral>(
            Assert.Single(instructions.OfType<IrReturn>()).Value);
        Assert.IsType<IrPointerOffsetValue>(returnedSlice.FieldValues["ptr"]);
    }

    [Theory]
    [InlineData("values[start..]")]
    [InlineData("values[..end]")]
    public void Slice_OpenEndedRangesCompile(string expression)
    {
        var module = BuildIr($$"""
from std::memory::slice import Slice

fn part(values: Slice<i32>, start: u32, end: u32) -> Slice<i32> {
    return {{expression}}
}
""");

        Assert.Single(module.Functions.Single(function => function.Name == "part")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrSliceBoundsCheck>());
    }

    [Fact]
    public void Slice_RangeInsideUnsafeIsExplicitlyUnchecked()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn middle(values: Slice<i32>, start: u32, end: u32) -> Slice<i32> {
    return unsafe { values[start..end] }
}
");

        var check = Assert.Single(module.Functions.Single(function => function.Name == "middle")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrSliceBoundsCheck>());
        Assert.Equal(IrBoundsCheckMode.Unchecked, check.BoundsCheck);
    }

    [Fact]
    public void Slice_IndexCheckIsEliminatedWhenRangeConditionDominatesIt()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn sum(values: Slice<i32>) -> i32 {
    var total = 0
    for index in 0..values.len() {
        total += *values[index]
    }
    return total
}
");
        var access = Assert.Single(module.Functions.Single(function => function.Name == "sum")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrIndexAccess>());
        Assert.Equal(IrBoundsCheckMode.Checked, access.BoundsCheck);

        Assert.True(new BoundsCheckEliminationPass().Run(module));
        Assert.Equal(IrBoundsCheckMode.Proven, access.BoundsCheck);
    }

    [Fact]
    public void Slice_IndexCheckRemainsWhenLoopBoundIsUnrelated()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn sum(values: Slice<i32>, limit: u32) -> i32 {
    var total = 0
    for index in 0..limit {
        total += *values[index]
    }
    return total
}
");
        var access = Assert.Single(module.Functions.Single(function => function.Name == "sum")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrIndexAccess>());

        Assert.False(new BoundsCheckEliminationPass().Run(module));
        Assert.Equal(IrBoundsCheckMode.Checked, access.BoundsCheck);
    }

    [Fact]
    public void Slice_ForInUsesProvenUncheckedPrimitiveWithoutOptionMachinery()
    {
        var module = BuildIr(@"
from std::memory::slice import Slice

fn sum(values: Slice<i32>) -> i32 {
    var total = 0
    for value in values { total += value }
    return total
}
");
        var instructions = module.Functions.Single(function => function.Name == "sum")
            .BasicBlocks.SelectMany(block => block.Instructions).ToList();

        Assert.Contains(instructions.OfType<IrCall>(), call => call.FunctionName.Contains("get_unchecked"));
        Assert.Empty(instructions.OfType<IrExtractTag>());
        Assert.Empty(instructions.OfType<IrExtractVariantData>());
    }

    [Fact]
    public void Array_ForInUsesProvenDirectIndexWithoutOptionMachinery()
    {
        var module = BuildIr(@"
fn sum(values: [i32; 3]) -> i32 {
    var total = 0
    for value in values { total += value }
    return total
}
");
        var instructions = module.Functions.Single(function => function.Name == "sum")
            .BasicBlocks.SelectMany(block => block.Instructions).ToList();

        Assert.Equal(IrBoundsCheckMode.Proven,
            Assert.Single(instructions.OfType<IrIndexAccess>()).BoundsCheck);
        Assert.Empty(instructions.OfType<IrCall>());
        Assert.Empty(instructions.OfType<IrExtractTag>());
    }

    [Fact]
    public void MutSlice_IndexAssignmentUsesCheckedIndexMutContract()
    {
        var module = BuildIr(@"
from std::memory::slice import MutSlice

fn replace(values: MutSlice<i32>, index: u32) {
    var writable = values
    writable[index] = 42
}
");
        var calls = module.Functions.Single(function => function.Name == "replace")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrCall>();

        Assert.Contains(calls, call =>
            call.FunctionName.Contains("IndexMut") && call.FunctionName.Contains("index_set"));
    }
}
