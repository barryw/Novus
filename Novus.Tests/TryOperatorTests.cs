using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class TryOperatorTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: false);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void TryOperator_SameErrorType_NoConversion_Compiles()
    {
        var source = @"
from std::core import Result
from amiga::sys::dos import DosError

fn may_fail() -> Result<i32, DosError> {
    return Result::Err(DosError::NotFound)
}

fn caller() -> Result<i32, DosError> {
    let value = may_fail()?
    return Result::Ok(value + 10)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var function = module.Functions.FirstOrDefault(f => f.Name == "caller");
        Assert.NotNull(function);
    }

    [Fact]
    public void TryOperator_DifferentErrorType_AutoConversion_Compiles()
    {
        var source = @"
from std::core import Result
from std::core import From
from amiga::sys::dos import DosError

enum AppError { Dos(DosError) }
impl From<DosError> for AppError {
    fn convert(consuming error: DosError) -> AppError { return AppError::Dos(error) }
}

fn dos_operation() -> Result<i32, DosError> {
    return Result::Err(DosError::NotFound)
}

fn higher_level() -> Result<i32, AppError> {
    let value = dos_operation()?
    return Result::Ok(value * 2)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var function = module.Functions.FirstOrDefault(f => f.Name == "higher_level");
        Assert.NotNull(function);

        // Verify that the From<DosError> impl is present
        var fromImpl = module.TraitImpls.FirstOrDefault(ti =>
            ti.TraitName == "From<DosError>" && ti.TypeName == "AppError");
        Assert.NotNull(fromImpl);
    }

    [Fact]
    public void TryOperator_MultipleConversions_Compiles()
    {
        var source = @"
from std::core import Result
from std::core import From
from amiga::sys::dos import DosError
from amiga::sys::exec import ExecError

enum AppError { Dos(DosError), Exec(ExecError) }
impl From<DosError> for AppError {
    fn convert(consuming error: DosError) -> AppError { return AppError::Dos(error) }
}
impl From<ExecError> for AppError {
    fn convert(consuming error: ExecError) -> AppError { return AppError::Exec(error) }
}

fn dos_op() -> Result<i32, DosError> {
    return Result::Err(DosError::NoFreeStore)
}

fn exec_op() -> Result<i32, ExecError> {
    return Result::Err(ExecError::NoMem)
}

fn combined() -> Result<i32, AppError> {
    let a = dos_op()?
    let b = exec_op()?
    return Result::Ok(a + b)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void TryOperator_InExpression_Compiles()
    {
        var source = @"
from std::core import Result
from amiga::sys::dos import DosError

fn may_fail() -> Result<i32, DosError> {
    return Result::Ok(42)
}

fn caller() -> Result<i32, DosError> {
    return Result::Ok(may_fail()? + 10)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void TryOperator_InFunctionCall_Compiles()
    {
        var source = @"
from std::core import Result
from amiga::sys::dos import DosError

fn may_fail() -> Result<i32, DosError> {
    return Result::Ok(5)
}

fn double(x: i32) -> i32 {
    return x * 2
}

fn caller() -> Result<i32, DosError> {
    return Result::Ok(double(may_fail()?))
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void TryOperator_ChainedCalls_Compiles()
    {
        var source = @"
from std::core import Result
from amiga::sys::dos import DosError

fn step1() -> Result<i32, DosError> {
    return Result::Ok(1)
}

fn step2(x: i32) -> Result<i32, DosError> {
    return Result::Ok(x + 1)
}

fn step3(x: i32) -> Result<i32, DosError> {
    return Result::Ok(x + 1)
}

fn pipeline() -> Result<i32, DosError> {
    let a = step1()?
    let b = step2(a)?
    let c = step3(b)?
    return Result::Ok(c)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void TryOperator_WithStructResult_Compiles()
    {
        var source = @"
from std::core import Result
from amiga::sys::dos import DosError

struct Point {
    x: i32,
    y: i32
}

fn get_point() -> Result<Point, DosError> {
    return Result::Ok(Point { x: 10, y: 20 })
}

fn use_point() -> Result<i32, DosError> {
    let p = get_point()?
    return Result::Ok(p.x + p.y)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var function = Assert.Single(module.Functions, f => f.Name == "use_point");
        var instructions = function.BasicBlocks.SelectMany(block => block.Instructions).ToList();
        Assert.DoesNotContain(instructions.OfType<IrLocalDecl>(), decl => decl.Name.StartsWith("%try_result_"));
        Assert.DoesNotContain(instructions.OfType<IrLocalDecl>(), decl => decl.Name.StartsWith("%try_return_err_"));
        Assert.DoesNotContain(instructions.OfType<IrStore>(), store => store.VariableName.StartsWith("%try_ok_val_"));
        Assert.Contains(instructions.OfType<IrExtractVariantData>(), extract => extract.ResultName == "p");
        Assert.Contains(instructions.OfType<IrLocalDecl>(), decl => decl.Name == "p" && decl.InitialValue == null);
    }

    [Fact]
    public void TryOperator_AllErrorSubsystems_Compile()
    {
        var source = @"
from std::core import Result
from std::core import From
from amiga::sys::dos import DosError
from amiga::sys::exec import ExecError
from amiga::sys::intuition import IntuitionError
from amiga::sys::graphics import GraphicsError

enum AppError { Dos(DosError), Exec(ExecError), Intuition(IntuitionError), Graphics(GraphicsError) }
impl From<DosError> for AppError { fn convert(consuming error: DosError) -> AppError { return AppError::Dos(error) } }
impl From<ExecError> for AppError { fn convert(consuming error: ExecError) -> AppError { return AppError::Exec(error) } }
impl From<IntuitionError> for AppError { fn convert(consuming error: IntuitionError) -> AppError { return AppError::Intuition(error) } }
impl From<GraphicsError> for AppError { fn convert(consuming error: GraphicsError) -> AppError { return AppError::Graphics(error) } }

fn dos_op() -> Result<i32, DosError> {
    return Result::Err(DosError::NotFound)
}

fn exec_op() -> Result<i32, ExecError> {
    return Result::Err(ExecError::NoMem)
}

fn intui_op() -> Result<i32, IntuitionError> {
    return Result::Err(IntuitionError::WindowOpenFailed)
}

fn gfx_op() -> Result<i32, GraphicsError> {
    return Result::Err(GraphicsError::BitMapAllocFailed)
}

fn all_ops() -> Result<i32, AppError> {
    let a = dos_op()?
    let b = exec_op()?
    let c = intui_op()?
    let d = gfx_op()?
    return Result::Ok(a + b + c + d)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify all From impls are present
        Assert.Contains(module.TraitImpls, ti => ti.TraitName == "From<DosError>" && ti.TypeName == "AppError");
        Assert.Contains(module.TraitImpls, ti => ti.TraitName == "From<ExecError>" && ti.TypeName == "AppError");
        Assert.Contains(module.TraitImpls, ti => ti.TraitName == "From<IntuitionError>" && ti.TypeName == "AppError");
        Assert.Contains(module.TraitImpls, ti => ti.TraitName == "From<GraphicsError>" && ti.TypeName == "AppError");
    }
}
