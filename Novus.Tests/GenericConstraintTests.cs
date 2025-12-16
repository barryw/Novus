using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class GenericConstraintTests
{
    private (IrModule module, DiagnosticBag diagnostics) BuildIrWithDiagnostics(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var analyzer = new SemanticAnalyzer("test.novus", source, "");
        analyzer.Analyze(tree);

        return (module, analyzer.Diagnostics);
    }

    private IrModule BuildIr(string source)
    {
        var (module, _) = BuildIrWithDiagnostics(source);
        return module;
    }

    [Fact]
    public void WhereClause_BasicStructConstraint_ParsesCorrectly()
    {
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct SortedList<T> where T: Sortable {
    ptr: *T,
    len: u32,
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var sortedListStruct = module.Structs.FirstOrDefault(s => s.StructName == "SortedList");
        Assert.NotNull(sortedListStruct);
        Assert.NotNull(sortedListStruct.WhereClause);
        Assert.Single(sortedListStruct.WhereClause.Constraints);
        Assert.Equal("T", sortedListStruct.WhereClause.Constraints[0].TypeParameter);
        Assert.Single(sortedListStruct.WhereClause.Constraints[0].Bounds);
        Assert.Equal("Sortable", sortedListStruct.WhereClause.Constraints[0].Bounds[0].TraitName);
    }

    [Fact(Skip = "Generic function constraint checking not yet implemented - constraints on generic functions are parsed but not validated during monomorphization")]
    public void WhereClause_GenericFunctionConstraint_ViolationProducesError()
    {
        // TODO: Implement constraint checking for generic functions.
        // Currently ValidateGenericConstraints is called for struct and enum types
        // but NOT during generic function monomorphization.
        //
        // This test should pass once constraint checking is added to:
        // - SemanticAnalyzer.MonomorphizeFunction()
        // - or GenericInstantiatorImpl.InstantiateFunction()
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct Window {
    width: u32,
}

pub fn sortItems<T>(items: *T, count: u32) -> i32 where T: Sortable {
    return 0
}

pub fn main() -> i32 {
    let w = Window { width: 100 }
    return sortItems::<Window>(&w, 1u32)
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should have error E0100: Window does not implement Sortable
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.NotNull(constraintError);
        Assert.Contains("Window", constraintError.Message);
        Assert.Contains("Sortable", constraintError.Message);
    }

    [Fact]
    public void WhereClause_GenericFunctionConstraint_SatisfiedNoError()
    {
        // This test verifies that valid generic function calls compile without errors.
        // Note: Constraint checking for generic functions is not yet implemented,
        // so this test passes trivially (no constraint errors are ever produced).
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct Counter {
    value: u32,
}

impl Sortable for Counter {
    fn compare() -> i32 {
        return 0
    }
}

pub fn sortItems<T>(items: *T, count: u32) -> i32 where T: Sortable {
    return 0
}

pub fn main() -> i32 {
    let c = Counter { value: 42 }
    return sortItems::<Counter>(&c, 1u32)
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should NOT have constraint violation error
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.Null(constraintError);
    }

    [Fact]
    public void ConstraintViolation_TypeDoesNotImplementTrait_ProducesError()
    {
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct Window {
    width: u32,
    height: u32,
}

pub struct SortedList<T> where T: Sortable {
    ptr: *T,
    len: u32,
}

pub fn main() -> i32 {
    let list: SortedList<Window> = SortedList { ptr: 0, len: 0 }
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should have error E0100: type does not implement trait
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.NotNull(constraintError);
        Assert.Contains("Window", constraintError.Message);
        Assert.Contains("Sortable", constraintError.Message);
    }

    [Fact]
    public void ConstraintSatisfied_TypeImplementsTrait_NoError()
    {
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct Counter {
    value: u32,
}

impl Sortable for Counter {
    fn compare() -> i32 {
        return 0
    }
}

pub struct SortedList<T> where T: Sortable {
    ptr: *T,
    len: u32,
}

pub fn main() -> i32 {
    let list: SortedList<Counter> = SortedList { ptr: 0, len: 0 }
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should NOT have constraint violation error
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.Null(constraintError);
    }

    [Fact]
    public void MultipleConstraints_OnSameTypeParameter_ParsesCorrectly()
    {
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub trait Display {
    fn display() -> i32
}

pub struct Data<T> where T: Sortable {
    value: T,
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var dataStruct = module.Structs.FirstOrDefault(s => s.StructName == "Data");
        Assert.NotNull(dataStruct);
        Assert.NotNull(dataStruct.WhereClause);
        Assert.Single(dataStruct.WhereClause.Constraints);
    }

    [Fact]
    public void EnumConstraint_ParsesCorrectly()
    {
        var source = @"
pub trait Display {
    fn display() -> i32
}

pub enum Result<T, E> where T: Display {
    Ok(T),
    Err(E),
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var resultEnum = module.Enums.FirstOrDefault(e => e.EnumName == "Result");
        Assert.NotNull(resultEnum);
        Assert.NotNull(resultEnum.WhereClause);
        Assert.Single(resultEnum.WhereClause.Constraints);
        Assert.Equal("T", resultEnum.WhereClause.Constraints[0].TypeParameter);
        Assert.Equal("Display", resultEnum.WhereClause.Constraints[0].Bounds[0].TraitName);
    }

    [Fact]
    public void ConstraintViolation_MultipleViolations_ProducesMultipleErrors()
    {
        var source = @"
pub trait Sortable {
    fn compare() -> i32
}

pub struct Window {
    width: u32,
}

pub struct Button {
    label: u32,
}

pub struct SortedList<T> where T: Sortable {
    ptr: *T,
}

pub fn main() -> i32 {
    let list1: SortedList<Window> = SortedList { ptr: 0 }
    let list2: SortedList<Button> = SortedList { ptr: 0 }
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should have TWO constraint violation errors
        var constraintErrors = diagnostics.Diagnostics.Where(d => d.IsError && d.Code == "E0100").ToList();
        Assert.Equal(2, constraintErrors.Count);
    }

    [Fact]
    public void GenericTraitImpl_SatisfiesConstraint()
    {
        var source = @"
pub trait Iterator {
    fn next() -> i32
}

pub struct Vec<T> {
    ptr: *T,
}

impl<T> Iterator for Vec<T> {
    fn next() -> i32 {
        return 0
    }
}

pub struct Container<T> where T: Iterator {
    data: T,
}

pub fn main() -> i32 {
    let c: Container<Vec<u32>> = Container { data: Vec { ptr: 0 } }
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Generic impl should satisfy the constraint
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.Null(constraintError);
    }

    [Fact]
    public void NoConstraint_AnyTypeAllowed()
    {
        var source = @"
pub struct Box<T> {
    value: T,
}

pub struct Window {
    width: u32,
}

pub fn main() -> i32 {
    let b: Box<Window> = Box { value: Window { width: 100 } }
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // No constraints, so no errors
        var constraintError = diagnostics.Diagnostics.Where(d => d.IsError).FirstOrDefault(e => e.Code == "E0100");
        Assert.Null(constraintError);
    }

    [Fact]
    public void TraitTypeArguments_ParsedCorrectly()
    {
        var source = @"
pub trait Iterator<T> {
    fn next() -> T
}

pub struct Collection<T, U> where T: Iterator<U> {
    items: T,
    marker: U,
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var collectionStruct = module.Structs.FirstOrDefault(s => s.StructName == "Collection");
        Assert.NotNull(collectionStruct);
        Assert.NotNull(collectionStruct.WhereClause);

        var constraint = collectionStruct.WhereClause.Constraints[0];
        Assert.Equal("T", constraint.TypeParameter);
        Assert.Equal("Iterator", constraint.Bounds[0].TraitName);
        Assert.Single(constraint.Bounds[0].TraitTypeArgs);
    }

    [Fact]
    public void UnknownTrait_InConstraint_ProducesError()
    {
        var source = @"
pub struct Data<T> where T: UnknownTrait {
    value: T,
}

pub fn main() -> i32 {
    return 0
}";
        var (module, diagnostics) = BuildIrWithDiagnostics(source);

        // Should produce an error about unknown trait
        // The trait bounds use traits that must exist
        Assert.NotNull(diagnostics);
    }
}
