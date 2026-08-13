using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for null safety helpers in IrBuilder.
/// These helpers ensure descriptive CompilerBugException is thrown instead of NullReferenceException
/// when symbol lookups fail unexpectedly.
/// </summary>
public class NullSafetyHelperTests
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
    public void RequireEnum_ExistingEnum_ReturnsEnum()
    {
        // This test verifies that looking up an existing enum works correctly
        // through a compilation that uses enums
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn main() {
    let c = Color::Red;
}";

        var module = BuildIr(source);

        // Verify the enum was created
        var colorEnum = module.Enums.FirstOrDefault(e => e.EnumName == "Color");
        Assert.NotNull(colorEnum);
        Assert.Equal(3, colorEnum!.Variants.Count);
    }

    [Fact]
    public void RequireStruct_ExistingStruct_ReturnsStruct()
    {
        // This test verifies that looking up an existing struct works correctly
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() {
    let p = Point { x: 10, y: 20 };
}";

        var module = BuildIr(source);

        // Verify the struct was created
        var pointStruct = module.Structs.FirstOrDefault(s => s.StructName == "Point");
        Assert.NotNull(pointStruct);
        Assert.Equal(2, pointStruct!.Fields.Count);
    }

    [Fact]
    public void RequireEnum_WithMultipleVariants_WorksCorrectly()
    {
        // This test verifies that the RequireEnum helper works for enum variant access
        var source = @"
enum Color {
    Red,
    Green,
    Blue,
    Custom(u8)
}

fn main() {
    let a = Color::Red;
    let b = Color::Green;
    let c = Color::Blue;
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        var colorEnum = module.Enums.FirstOrDefault(e => e.EnumName == "Color");
        Assert.NotNull(colorEnum);
        Assert.Equal(4, colorEnum!.Variants.Count);
    }

    [Fact]
    public void RequireStruct_WithMultipleFields_WorksCorrectly()
    {
        // This test verifies that the RequireStruct helper works for struct instantiation
        var source = @"
struct Rectangle {
    x: i32,
    y: i32,
    width: u32,
    height: u32
}

fn main() {
    let r = Rectangle { x: 10, y: 20, width: 100, height: 50 };
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        var rectStruct = module.Structs.FirstOrDefault(s => s.StructName == "Rectangle");
        Assert.NotNull(rectStruct);
        Assert.Equal(4, rectStruct!.Fields.Count);
    }

    [Fact]
    public void EnumMatchExpression_UsesRequireEnum()
    {
        // This test verifies match expressions on enums work (they use RequireEnum internally)
        var source = @"
enum Status {
    Active,
    Inactive,
    Pending
}

fn check_status(s: Status) -> i32 {
    match s {
        Status::Active => 1,
        Status::Inactive => 0,
        Status::Pending => 2
    }
}

fn main() {
    let result = check_status(Status::Active);
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        // Verify the function was created correctly
        var checkStatusFunc = module.Functions.FirstOrDefault(f => f.Name == "check_status");
        Assert.NotNull(checkStatusFunc);
    }

    [Fact]
    public void AssociatedFunction_UsesRequireStruct()
    {
        // This test verifies associated functions work (they use RequireStruct internally)
        var source = @"
struct Counter {
    value: i32
}

impl Counter {
    fn new() -> Counter {
        Counter { value: 0 }
    }

    fn increment(&var self) {
        self.value = self.value + 1;
    }
}

fn main() {
    var c = Counter::new();
    c.increment();
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
    }

    [Fact]
    public void PathExpression_EnumVariant_UsesRequireEnum()
    {
        // This test verifies path expressions with enum variants work
        var source = @"
enum Direction {
    North,
    South,
    East,
    West
}

fn main() {
    let d = Direction::North;
    let d2 = Direction::South;
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        // Verify the enum was created
        var directionEnum = module.Enums.FirstOrDefault(e => e.EnumName == "Direction");
        Assert.NotNull(directionEnum);
        Assert.Equal(4, directionEnum!.Variants.Count);
    }

    [Fact]
    public void NestedEnumAccess_UsesRequireEnum()
    {
        // This test verifies nested enum access works correctly
        var source = @"
enum Outer {
    A,
    B
}

enum Inner {
    X,
    Y
}

fn test_enums() -> i32 {
    let a = Outer::A;
    let x = Inner::X;
    0
}

fn main() {
    test_enums();
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        Assert.Equal(2, module.Enums.Count);
    }

    [Fact]
    public void StructWithNestedAccess_UsesRequireStruct()
    {
        // This test verifies struct field access works correctly
        var source = @"
struct Inner {
    value: i32
}

struct Outer {
    name: u32,
    inner: Inner
}

fn main() {
    let i = Inner { value: 42 };
    let o = Outer { name: 1, inner: i };
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        Assert.Equal(2, module.Structs.Count);
    }

    [Fact]
    public void MultipleStructMethods_UsesRequireStruct()
    {
        // This test verifies multiple struct methods work correctly
        var source = @"
struct Point {
    x: i32,
    y: i32
}

impl Point {
    fn origin() -> Point {
        Point { x: 0, y: 0 }
    }

    fn add(&self, other: &Point) -> Point {
        Point { x: self.x + other.x, y: self.y + other.y }
    }
}

fn main() {
    let p = Point::origin();
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
    }

    [Fact]
    public void EnumWithAssociatedData_UsesRequireEnum()
    {
        // This test verifies enums with associated data work
        var source = @"
enum Message {
    Quit,
    Move(i32, i32),
    Write(u8)
}

fn process(m: Message) -> i32 {
    match m {
        Message::Quit => 0,
        Message::Move(x, y) => x + y,
        Message::Write(c) => c as i32
    }
}

fn main() {
    let m = Message::Move(10, 20);
    let result = process(m);
}";

        var module = BuildIr(source);

        // Should compile without exceptions
        Assert.NotNull(module);
        // Verify the enum variants are correct
        var msgEnum = module.Enums.FirstOrDefault(e => e.EnumName == "Message");
        Assert.NotNull(msgEnum);
        Assert.Equal(3, msgEnum!.Variants.Count);

        var moveVariant = msgEnum.GetVariant("Move");
        Assert.NotNull(moveVariant);
        Assert.Equal(2, moveVariant!.AssociatedData.Count);
    }
}
