using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class EnumTests
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

    [Fact]
    public void BuildIr_SimpleEnum_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Enums);
    }

    [Fact]
    public void BuildIr_EnumWithData_Compiles()
    {
        var source = @"
enum Message {
    Quit,
    Move(i32, i32),
    Write(i32)
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Enums);
    }

    [Fact]
    public void BuildIr_EnumConstruction_UnitVariant_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive
}
pub fn main() -> i32 {
    let s = Status::Active
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumConstruction_WithData_Compiles()
    {
        var source = @"
enum Point {
    TwoD(i32, i32),
    ThreeD(i32, i32, i32)
}
pub fn main() -> i32 {
    let p = Point::TwoD(10, 20)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchOnEnum_UnitVariants_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}
pub fn main() -> i32 {
    let c = Color::Red
    match c {
        Color::Red => return 1,
        Color::Green => return 2,
        Color::Blue => return 3,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchOnEnum_WithWildcard_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive,
    Pending
}
pub fn main() -> i32 {
    let s = Status::Active
    match s {
        Status::Active => return 1,
        _ => return 0,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchOnEnum_ExtractData_Compiles()
    {
        var source = @"
enum Point {
    TwoD(i32, i32),
    ThreeD(i32, i32, i32)
}
pub fn main() -> i32 {
    let p = Point::TwoD(10, 20)
    match p {
        Point::TwoD(x, y) => return x + y,
        Point::ThreeD(x, y, z) => return x + y + z,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnum_Option_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    let x: Option<i32> = Option::Some(42)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnum_Result_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
pub fn main() -> i32 {
    let r: Result<i32, i32> = Result::Ok(100)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchGenericEnum_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    let x: Option<i32> = Option::Some(42)
    match x {
        Option::Some(v) => return v,
        Option::None => return 0,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumAsParameter_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive
}
fn check(s: Status) -> i32 {
    match s {
        Status::Active => return 1,
        Status::Inactive => return 0,
    }
}
pub fn main() -> i32 {
    return check(Status::Active)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumAsReturnType_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive
}
fn getStatus() -> Status {
    return Status::Active
}
pub fn main() -> i32 {
    let s = getStatus()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NestedEnum_Compiles()
    {
        var source = @"
enum Outer {
    A,
    B
}
enum Inner {
    X,
    Y
}
pub fn main() -> i32 {
    let o = Outer::A
    let i = Inner::X
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Equal(2, module.Enums.Count);
    }

    [Fact]
    public void BuildIr_EnumInStruct_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive
}
struct User {
    status: Status,
    age: i32
}
pub fn main() -> i32 {
    let u = User {
        status: Status::Active,
        age: 25
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleEnumVariantsWithSameDataType_Compiles()
    {
        var source = @"
enum Message {
    Text(i32),
    Number(i32),
    Flag(i32)
}
pub fn main() -> i32 {
    let m = Message::Text(42)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumVariantWithMultipleFields_Compiles()
    {
        var source = @"
enum Complex {
    Triple(i32, i32, i32),
    Quad(i32, i32, i32, i32)
}
pub fn main() -> i32 {
    let c = Complex::Quad(1, 2, 3, 4)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchEnumWithMultipleBindings_Compiles()
    {
        var source = @"
enum Point {
    TwoD(i32, i32),
    ThreeD(i32, i32, i32)
}
pub fn main() -> i32 {
    let p = Point::ThreeD(1, 2, 3)
    match p {
        Point::TwoD(x, y) => return x,
        Point::ThreeD(x, y, z) => return x + y + z,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumWithMixedVariants_Compiles()
    {
        var source = @"
enum Value {
    Empty,
    Int(i32),
    Pair(i32, i32),
    Triple(i32, i32, i32)
}
pub fn main() -> i32 {
    let v1 = Value::Empty
    let v2 = Value::Int(10)
    let v3 = Value::Pair(1, 2)
    let v4 = Value::Triple(1, 2, 3)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumTagExtraction_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}
pub fn main() -> i32 {
    let c = Color::Green
    match c {
        Color::Red => return 0,
        Color::Green => return 1,
        Color::Blue => return 2,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumWithMultipleTypeParams_Compiles()
    {
        var source = @"
enum Either<L, R> {
    Left(L),
    Right(R)
}
pub fn main() -> i32 {
    let e: Either<i32, i32> = Either::Left(42)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PublicEnum_Compiles()
    {
        var source = @"
pub enum Status {
    Active,
    Inactive
}
pub fn main() -> i32 {
    let s = Status::Active
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
