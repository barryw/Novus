using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class ResultMainTests
{
    private static string StdLibPath => PathUtility.FindStdLibPath()
        ?? throw new InvalidOperationException("Novus standard library not found");

    private const string ValidProgram = """
        from std::core import Result, Error

        enum AppError { Failed }

        impl Error for AppError {
            fn message(&self) -> *u8 {
                return "Something failed"
            }
        }

        fn fail() -> Result<(), AppError> {
            Result::Err(AppError::Failed)
        }

        fn main() -> Result<(), AppError> {
            fail()?
            Result::Ok(())
        }
        """;

    private static NovusParser.CompilationUnitContext Parse(string source)
    {
        var lexer = new NovusLexer(new AntlrInputStream(source));
        var parser = new NovusParser(new AngleBracketTokenStream(lexer));
        return parser.compilationUnit();
    }

    private static DiagnosticBag Analyze(string source)
    {
        var analyzer = new SemanticAnalyzer("test.novus", source, StdLibPath);
        analyzer.Analyze(Parse(source));
        return analyzer.Diagnostics;
    }

    private static (IrModule Module, IrBuilder Builder) Build(string source)
    {
        var builder = new IrBuilder(skipAutoImports: false);
        builder.SetStdLibPath(StdLibPath);
        builder.SetInputFilePath("test.novus");
        return (builder.BuildModule(Parse(source)), builder);
    }

    [Fact]
    public void ResultMain_IsLoweredToI32WrapperThatReportsFailure()
    {
        var (module, builder) = Build(ValidProgram);

        Assert.False(builder.Diagnostics.HasErrors);
        var main = module.GetFunction("main");
        Assert.Equal("i32", main?.ReturnType.Name);
        Assert.Equal("Result<(), AppError>", module.GetFunction("__novus_user_main")?.ReturnType.Name);
        Assert.Contains(main!.BasicBlocks.SelectMany(block => block.Instructions),
            instruction => instruction is IrReturn { Value: IrConstant { Value: 20 } });

        var code = new CCodeGenerator(module, builder.StringLiterals, "68020", "soft", BuildMode.Release).Generate();
        Assert.Contains("__novus_user_main", code);
        Assert.Contains("__novus_program_failed", code);
    }

    [Theory]
    [InlineData("Result<i32, AppError>", ErrorCodes.InvalidMainResult)]
    [InlineData("Result<(), i32>", ErrorCodes.TraitNotImplemented)]
    public void InvalidResultMain_IsRejected(string returnType, string errorCode)
    {
        var source = $$"""
            from std::core import Result, Error

            enum AppError { Failed }
            impl Error for AppError {
                fn message(&self) -> *u8 { return "failed" }
            }

            fn main() -> {{returnType}} {
                panic("not reached")
            }
            """;

        Assert.Contains(Analyze(source).Diagnostics, diagnostic => diagnostic.Code == errorCode);
    }

    [Fact]
    public void TryOperator_InI32Main_IsRejected()
    {
        var source = """
            from std::core import Result

            fn fail() -> Result<(), i32> { Result::Err(1) }
            fn main() -> i32 {
                fail()?
                0
            }
            """;

        Assert.Contains(Analyze(source).Diagnostics, diagnostic => diagnostic.Code == ErrorCodes.TryOperatorInvalidContext);
    }

    [Fact]
    public void TryOperator_ErrorReturn_PreservesDropCleanup()
    {
        var source = """
            from std::core import Result, Drop

            struct Guard { value: i32 }
            impl Drop for Guard {
                fn drop(&var self) { self.value = 0 }
            }

            fn fail() -> Result<(), i32> { Result::Err(1) }
            fn propagate() -> Result<(), i32> {
                let guard = Guard { value: 42 }
                fail()?
                Result::Ok(())
            }
            """;

        var (module, builder) = Build(source);
        var function = module.GetFunction("propagate");

        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Contains(function!.DeferredBlocks.SelectMany(block => block.Instructions),
            instruction => instruction is IrDropInPlace { ElementType: IrStructType { StructName: "Guard" } });
        Assert.Contains(function.BasicBlocks.Where(block => block.Label.StartsWith("try_err_"))
                .SelectMany(block => block.Instructions),
            instruction => instruction is IrReturn);
    }

    [Fact]
    public void ThrowawayBinding_DropsOwnedResult()
    {
        var source = """
            from std::core import Drop

            struct Guard { value: i32 }
            impl Drop for Guard {
                fn drop(&var self) { self.value = 0 }
            }

            fn make() -> Guard { Guard { value: 42 } }
            fn discard() { let _ = make() }
            """;

        var (module, builder) = Build(source);
        var function = module.GetFunction("discard");

        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Contains(function!.BasicBlocks.SelectMany(block => block.Instructions),
            instruction => instruction is IrDropInPlace { ElementType: IrStructType { StructName: "Guard" } });
    }

    [Fact]
    public void BorrowedMethodOnOwnedTemporary_PreservesDropCleanup()
    {
        var source = """
            from std::core import Drop

            struct Guard { value: i32 }
            impl Guard {
                fn read(&self) -> i32 { self.value }
                fn add(&var self, value: i32) { self.value = self.value + value }
            }
            impl Drop for Guard {
                fn drop(&var self) { self.value = 0 }
            }

            fn make() -> Guard { Guard { value: 42 } }
            fn inspect() -> i32 { make().read() }
            struct Bucket { guard: Guard }
            fn mutate(bucket: &var Bucket, other: &Guard) {
                bucket.guard.add(other.read())
            }
            """;

        var (module, builder) = Build(source);
        var function = module.GetFunction("inspect");

        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Contains(function!.LocalVariables, local => local.Name.StartsWith("_borrowed_temp_"));
        Assert.Contains(function.DeferredBlocks.SelectMany(block => block.Instructions),
            instruction => instruction is IrDropInPlace { ElementType: IrStructType { StructName: "Guard" } });

        var mutate = module.GetFunction("mutate")!;
        Assert.DoesNotContain(mutate.LocalVariables, local => local.Name.StartsWith("_borrowed_temp_"));
        Assert.Contains(mutate.BasicBlocks.SelectMany(block => block.Instructions).OfType<IrCall>(),
            call => call.Arguments.Any(argument =>
                argument is IrBorrowValue { BorrowedValue: IrFieldReference { FieldName: "guard" } }));
    }

    [Fact]
    public void ResultMatch_MovesOwnedPayloadWithoutDroppingSourceTwice()
    {
        var source = """
            from std::core import Result, Drop

            struct Guard { value: i32 }
            impl Drop for Guard {
                fn drop(&var self) { self.value = 0 }
            }

            fn consume(result: Result<Guard, i32>) {
                match result {
                    Result::Ok(guard) => {},
                    Result::Err(_) => {}
                }
            }
            """;

        var (module, builder) = Build(source);
        var instructions = module.GetFunction("consume")!.BasicBlocks
            .SelectMany(block => block.Instructions);

        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Contains(instructions, instruction => instruction is IrMemberStore
        {
            FieldName: "tag",
            Value: IrConstant { Value: -1 }
        });
    }

    [Fact]
    public void ResultMatch_BorrowedOwnedPayloadWildcard_DoesNotMovePayload()
    {
        var source = """
            from std::core import Result, Drop

            struct Guard { value: i32 }
            impl Drop for Guard {
                fn drop(&var self) { self.value = 0 }
            }

            fn inspect(result: &Result<Guard, i32>) {
                match result {
                    Result::Ok(_) => {},
                    Result::Err(_) => {}
                }
            }
            """;

        Assert.DoesNotContain(Analyze(source).Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(Build(source).Builder.Diagnostics.HasErrors);
    }
}
