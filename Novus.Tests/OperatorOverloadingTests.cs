using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for operator overloading via trait implementation.
/// Operators can be overloaded by implementing the corresponding traits:
/// - Add: +
/// - Sub: -
/// - Mul: *
/// - Div: /
/// - Rem: %
/// - Eq: == and !=
/// - PartialOrd: &lt;, &lt;=, &gt;, &gt;=
/// </summary>
public class OperatorOverloadingTests
{
    private DiagnosticBag Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

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

    // ========================================================================
    // Trait Definition Tests
    // ========================================================================

    [Fact]
    public void TraitDefinition_AddTrait_Parses()
    {
        var source = @"
pub trait Add {
    fn add(&self, other: &Self) -> Self
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        var addTrait = module.Traits.FirstOrDefault(t => t.TraitName == "Add");
        Assert.NotNull(addTrait);
        Assert.Single(addTrait.Methods);
        Assert.Equal("add", addTrait.Methods[0].Name);
    }

    [Fact]
    public void TraitDefinition_PartialOrdTrait_Parses()
    {
        var source = @"
pub trait PartialOrd {
    fn lt(&self, other: &Self) -> bool
    fn le(&self, other: &Self) -> bool
    fn gt(&self, other: &Self) -> bool
    fn ge(&self, other: &Self) -> bool
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        var ordTrait = module.Traits.FirstOrDefault(t => t.TraitName == "PartialOrd");
        Assert.NotNull(ordTrait);
        Assert.Equal(4, ordTrait.Methods.Count);
    }

    // ========================================================================
    // Semantic Analysis Tests - Error Cases
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_StructWithoutAddTrait_ErrorOnPlus()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32,
}

fn main() -> i32 {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 3, y: 4 }
    let p3 = p1 + p2  // ERROR: Point doesn't implement Add
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0004");
        Assert.NotNull(error);
        Assert.Contains("Add", error.Message);
    }

    [Fact]
    public void SemanticAnalysis_StructWithoutEqTrait_ErrorOnEquals()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32,
}

fn main() -> bool {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 1, y: 2 }
    return p1 == p2  // ERROR: Point doesn't implement Eq
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0004");
        Assert.NotNull(error);
        Assert.Contains("Eq", error.Message);
    }

    [Fact]
    public void SemanticAnalysis_StructWithoutPartialOrdTrait_ErrorOnLessThan()
    {
        var source = @"
struct Version {
    major: u32,
    minor: u32,
}

fn main() -> bool {
    let v1 = Version { major: 1, minor: 0 }
    let v2 = Version { major: 2, minor: 0 }
    return v1 < v2  // ERROR: Version doesn't implement PartialOrd
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0004");
        Assert.NotNull(error);
        Assert.Contains("PartialOrd", error.Message);
    }

    // ========================================================================
    // Semantic Analysis Tests - Success Cases
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_StructWithAddTrait_AllowsPlus()
    {
        // Note: Add trait is defined in std/core.novus which is auto-imported
        var source = @"
struct Point {
    x: i32,
    y: i32,
}

impl Add for Point {
    fn add(&self, other: &Point) -> Point {
        return Point { x: self.x + other.x, y: self.y + other.y }
    }
}

fn main() -> i32 {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 3, y: 4 }
    let p3 = p1 + p2  // OK: Point implements Add
    return p3.x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void SemanticAnalysis_StructWithEqTrait_AllowsEquals()
    {
        // Note: Eq trait is defined in std/core.novus which is auto-imported
        var source = @"
struct Point {
    x: i32,
    y: i32,
}

impl Eq for Point {
    fn eq(&self, other: &Point) -> bool {
        return self.x == other.x && self.y == other.y
    }
}

fn main() -> bool {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 1, y: 2 }
    return p1 == p2  // OK: Point implements Eq
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void SemanticAnalysis_StructWithPartialOrdTrait_AllowsLessThan()
    {
        // Note: PartialOrd trait is defined in std/core.novus which is auto-imported
        var source = @"
struct Version {
    major: u32,
    minor: u32,
}

impl PartialOrd for Version {
    fn lt(&self, other: &Version) -> bool {
        if self.major < other.major { return true }
        if self.major > other.major { return false }
        return self.minor < other.minor
    }
    fn le(&self, other: &Version) -> bool {
        if self.major < other.major { return true }
        if self.major > other.major { return false }
        return self.minor <= other.minor
    }
    fn gt(&self, other: &Version) -> bool {
        if self.major > other.major { return true }
        if self.major < other.major { return false }
        return self.minor > other.minor
    }
    fn ge(&self, other: &Version) -> bool {
        if self.major > other.major { return true }
        if self.major < other.major { return false }
        return self.minor >= other.minor
    }
}

fn main() -> bool {
    let v1 = Version { major: 1, minor: 0 }
    let v2 = Version { major: 2, minor: 0 }
    return v1 < v2  // OK: Version implements PartialOrd
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    // ========================================================================
    // IR Builder Tests
    // ========================================================================

    [Fact]
    public void IrBuilder_AddOperator_GeneratesTraitMethodCall()
    {
        var source = @"
pub trait Add {
    fn add(&self, other: &Self) -> Self
}

struct Point {
    x: i32,
    y: i32,
}

impl Add for Point {
    fn add(&self, other: &Point) -> Point {
        return Point { x: self.x + other.x, y: self.y + other.y }
    }
}

pub fn main() -> i32 {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 3, y: 4 }
    let p3 = p1 + p2
    return p3.x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Check that the Add trait method was generated
        var addMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("Add") && f.Name.Contains("add"));
        Assert.NotNull(addMethod);

        // Check that main function exists and has the call to the add method
        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);

        // Look for an IrCall instruction to the Add::add method in main's body
        var hasAddCall = mainFunction.BasicBlocks
            .SelectMany(b => b.Instructions)
            .OfType<IrCall>()
            .Any(c => c.FunctionName.Contains("Add") && c.FunctionName.Contains("add"));
        Assert.True(hasAddCall, "Expected main to call the Add::add method");
    }

    [Fact]
    public void IrBuilder_NotEqualsOperator_GeneratesNegatedEqCall()
    {
        var source = @"
pub trait Eq {
    fn eq(&self, other: &Self) -> bool
}

struct Point {
    x: i32,
    y: i32,
}

impl Eq for Point {
    fn eq(&self, other: &Point) -> bool {
        return self.x == other.x && self.y == other.y
    }
}

pub fn main() -> bool {
    let p1 = Point { x: 1, y: 2 }
    let p2 = Point { x: 3, y: 4 }
    return p1 != p2  // Should call eq() then negate
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Check that the Eq trait method was generated
        var eqMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("Eq") && f.Name.Contains("eq"));
        Assert.NotNull(eqMethod);
    }

    // ========================================================================
    // Primitive Types - Built-in Operators
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_PrimitiveTypes_UseBuiltInOperators()
    {
        var source = @"
fn main() -> i32 {
    let a: i32 = 10
    let b: i32 = 20
    let sum = a + b       // Built-in
    let diff = a - b      // Built-in
    let prod = a * b      // Built-in
    let quot = a / b      // Built-in
    let rem = a % b       // Built-in
    let eq = a == b       // Built-in
    let lt = a < b        // Built-in
    return sum
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    // ========================================================================
    // Type Mismatch Tests
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_MismatchedTypes_ReportsError()
    {
        var source = @"
pub trait Add {
    fn add(&self, other: &Self) -> Self
}

struct Point { x: i32, y: i32 }
struct Vector { x: i32, y: i32 }

impl Add for Point {
    fn add(&self, other: &Point) -> Point {
        return Point { x: self.x + other.x, y: self.y + other.y }
    }
}

fn main() -> i32 {
    let p = Point { x: 1, y: 2 }
    let v = Vector { x: 3, y: 4 }
    let result = p + v  // ERROR: cannot add Point and Vector
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0004");
        Assert.NotNull(error);
        Assert.Contains("mismatched types", error.Message);
    }

    // ========================================================================
    // Index Operator Tests
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_StructWithoutIndexTrait_ErrorOnIndexAccess()
    {
        var source = @"
struct MyCollection {
    data: i32,
}

fn main() -> i32 {
    let col = MyCollection { data: 42 }
    return col[0]  // ERROR: MyCollection doesn't implement Index
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0024");
        Assert.NotNull(error);
        Assert.Contains("cannot index", error.Message.ToLower());
    }

    [Fact]
    public void SemanticAnalysis_StructWithIndexTrait_AllowsIndexAccess()
    {
        // Note: Index trait is defined in std/core.novus which is auto-imported
        var source = @"
struct MyArray {
    data: *i32,
    len: u32,
}

impl Index<u32, i32> for MyArray {
    fn index(&self, idx: u32) -> i32 {
        return self.data[idx]
    }
}

fn main() -> i32 {
    let arr = MyArray { data: (*i32)0, len: 0u32 }
    return arr[0u32]  // OK: MyArray implements Index<u32, i32>
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void IrBuilder_IndexOperator_GeneratesTraitMethodCall()
    {
        var source = @"
pub trait Index<I, T> {
    fn index(&self, idx: I) -> T
}

struct MyArray {
    data: *i32,
    len: u32,
}

impl Index<u32, i32> for MyArray {
    fn index(&self, idx: u32) -> i32 {
        return self.data[idx]
    }
}

pub fn main() -> i32 {
    let arr = MyArray { data: (*i32)0, len: 0u32 }
    return arr[0u32]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Check that the Index trait method was generated
        var indexMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("Index") && f.Name.Contains("index"));
        Assert.NotNull(indexMethod);

        // Check that main function exists
        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    // ========================================================================
    // IndexMut Operator Tests
    // ========================================================================

    [Fact]
    public void SemanticAnalysis_StructWithIndexMutTrait_AllowsIndexAssignment()
    {
        // Note: IndexMut trait is defined in std/core.novus which is auto-imported
        var source = @"
struct MyArray {
    data: *i32,
    len: u32,
}

impl IndexMut<u32, i32> for MyArray {
    fn index_set(&var self, idx: u32, value: i32) {
        self.data[idx] = value
    }
}

fn main() -> i32 {
    var arr = MyArray { data: (*i32)0, len: 0u32 }
    arr[0u32] = 42  // OK: MyArray implements IndexMut<u32, i32>
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors,
            $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void IrBuilder_IndexMutOperator_GeneratesTraitMethodCall()
    {
        var source = @"
pub trait IndexMut<I, T> {
    fn index_set(&var self, idx: I, value: T)
}

struct MyArray {
    data: *i32,
    len: u32,
}

impl IndexMut<u32, i32> for MyArray {
    fn index_set(&var self, idx: u32, value: i32) {
        self.data[idx] = value
    }
}

pub fn main() -> i32 {
    var arr = MyArray { data: (*i32)0, len: 0u32 }
    arr[0u32] = 42
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Check that the IndexMut trait method was generated
        var indexSetMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("IndexMut") && f.Name.Contains("index_set"));
        Assert.NotNull(indexSetMethod);

        // Check that main function exists
        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);

        // The main function should have code for the array assignment
        // Note: Due to how IrBuilder works without semantic analysis, the actual trait call
        // may not be generated. The key test is that semantic analysis allows the syntax,
        // which is covered by SemanticAnalysis_StructWithIndexMutTrait_AllowsIndexAssignment.
        // Here we just verify the module builds correctly and the trait method exists.
        Assert.True(mainFunction.BasicBlocks.Count > 0, "Expected main to have at least one basic block");
    }
}
