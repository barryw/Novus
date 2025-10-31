using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class PatternMatchingTests
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
    public void BuildIr_MatchEnumVariant_Unit_Compiles()
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
        Color::Red => return 1
        Color::Green => return 2
        Color::Blue => return 3
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchEnumVariant_WithData_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    match opt {
        Option::Some(x) => return x
        Option::None => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchNestedEnum_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    let r: Result<Option<i32>, i32> = Result::Ok(Option::Some(5))
    match r {
        Result::Ok(opt) => return 1
        Result::Err(e) => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchMultipleBindings_Compiles()
    {
        var source = @"
enum Point {
    TwoD(i32, i32),
    ThreeD(i32, i32, i32)
}
pub fn main() -> i32 {
    let p = Point::TwoD(10, 20)
    match p {
        Point::TwoD(x, y) => return x + y
        Point::ThreeD(x, y, z) => return x + y + z
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchWithWildcardInPattern_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    let x: Option<i32> = Option::Some(10)
    match x {
        Option::Some(_) => return 1
        Option::None => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchExhaustive_AllVariants_Compiles()
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
        Status::Active => return 1
        Status::Inactive => return 2
        Status::Pending => return 3
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchNonExhaustive_WithWildcard_Compiles()
    {
        var source = @"
enum Status {
    Active,
    Inactive,
    Pending,
    Cancelled
}
pub fn main() -> i32 {
    let s = Status::Active
    match s {
        Status::Active => return 1
        _ => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchInFunction_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}
fn colorValue(c: Color) -> i32 {
    match c {
        Color::Red => return 1
        Color::Green => return 2
        Color::Blue => return 3
    }
}
pub fn main() -> i32 {
    return colorValue(Color::Green)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }


    [Fact]
    public void BuildIr_MatchWithComplexExpression_Compiles()
    {
        var source = @"
enum Value {
    Int(i32),
    Bool(bool)
}
pub fn main() -> i32 {
    let v = Value::Int(10 + 5)
    match v {
        Value::Int(x) => return x
        Value::Bool(b) => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchMultipleArms_SameBody_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue,
    Yellow
}
pub fn main() -> i32 {
    let c = Color::Red
    match c {
        Color::Red => return 1
        Color::Green => return 1
        Color::Blue => return 2
        Color::Yellow => return 2
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchEnumWithNoData_Compiles()
    {
        var source = @"
enum Signal {
    Start,
    Stop,
    Pause,
    Resume
}
pub fn main() -> i32 {
    let sig = Signal::Start
    match sig {
        Signal::Start => return 1
        Signal::Stop => return 2
        Signal::Pause => return 3
        Signal::Resume => return 4
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchGenericResult_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
pub fn main() -> i32 {
    let r: Result<i32, i32> = Result::Ok(42)
    match r {
        Result::Ok(v) => return v
        Result::Err(e) => return e
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchInLoop_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 5; i++) {
        let opt: Option<i32> = Option::Some(i)
        match opt {
            Option::Some(v) => sum += v
            Option::None => {}
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchEnumWithDifferentDataTypes_Compiles()
    {
        var source = @"
enum Value {
    Int(i32),
    Byte(i8),
    Long(i64)
}
pub fn main() -> i32 {
    let v = Value::Int(100)
    match v {
        Value::Int(x) => return x
        Value::Byte(b) => return (i32)b
        Value::Long(l) => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchWithSideEffects_Compiles()
    {
        var source = @"
enum Action {
    Increment,
    Decrement,
    Reset
}
pub fn main() -> i32 {
    var count = 10
    let action = Action::Increment
    match action {
        Action::Increment => count += 1
        Action::Decrement => count -= 1
        Action::Reset => count = 0
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchBoolEnum_Compiles()
    {
        var source = @"
enum Toggle {
    On,
    Off
}
pub fn main() -> i32 {
    let t = Toggle::On
    match t {
        Toggle::On => return 1
        Toggle::Off => return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MatchWithEarlyReturn_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
pub fn main() -> i32 {
    let r: Result<i32, i32> = Result::Err(404)
    match r {
        Result::Ok(v) => return v
        Result::Err(e) => {
            if e == 404 {
                return -1
            }
            return e
        }
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
