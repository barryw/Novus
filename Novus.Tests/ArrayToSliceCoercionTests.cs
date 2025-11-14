using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
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
}
