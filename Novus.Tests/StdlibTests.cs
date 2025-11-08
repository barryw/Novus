using Novus.Frontend;
using Novus.Parser;
using Antlr4.Runtime;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// End-to-end tests for Novus standard library functions.
/// Each test compiles a Novus program that uses a stdlib function and verifies correct IR generation.
/// </summary>
public class StdlibTests
{
    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Novus.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new Exception("Could not find project root");
    }

    private static NovusParser.CompilationUnitContext Parse(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokens);
        return parser.compilationUnit();
    }
    // ===================================================================================
    // EXEC MODULE TESTS
    // ===================================================================================

    [Fact]
    public void Stdlib_Exec_GetCurrentTask_ReturnsValidTask()
    {
        var code = @"
            import std::exec;

            fn main() -> i32 {
                let task = exec::get_current_task();
                // On Amiga, current task should always be valid
                if task.is_some() {
                    0
                } else {
                    1
                }
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("get_current_task", result);
        Assert.Contains("Option", result);
    }

    [Fact]
    public void Stdlib_Exec_AllocateSignal_ReturnsSignalNumber()
    {
        var code = @"
            import std::exec;

            fn main() -> i32 {
                // Allocate any available signal (-1 means any)
                let signal = exec::allocate_signal(-1);

                match signal {
                    Option::Some(sig) => {
                        // Success - free it
                        exec::free_signal(sig);
                        0
                    },
                    Option::None => 1  // Allocation failed
                }
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("allocate_signal", result);
        Assert.Contains("free_signal", result);
    }

    [Fact]
    public void Stdlib_Exec_ForbidPermit_CompileCorrectly()
    {
        var code = @"
            import std::exec;

            fn main() -> i32 {
                exec::forbid();
                // Critical section
                exec::permit();
                0
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("forbid", result);
        Assert.Contains("permit", result);
    }

    [Fact]
    public void Stdlib_Exec_DisableEnable_CompileCorrectly()
    {
        var code = @"
            import std::exec;

            fn main() -> i32 {
                exec::disable();
                // Interrupts disabled
                exec::enable();
                0
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("disable", result);
        Assert.Contains("enable", result);
    }

    // ===================================================================================
    // DOS MODULE TESTS
    // ===================================================================================

    [Fact]
    public void Stdlib_Dos_OpenFile_ReturnsResult()
    {
        var code = @"
            import std::dos;
            import std::error;

            fn main() -> i32 {
                let path = ""RAM:test.txt"";
                let result = dos::open_file(path as *u8, 1005);  // MODE_OLDFILE

                match result {
                    Result::Ok(fh) => {
                        dos::close_file(fh);
                        0
                    },
                    Result::Err(err) => 1  // File not found is expected
                }
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("open_file", result);
        Assert.Contains("Result", result);
    }

    [Fact]
    public void Stdlib_Dos_WriteFile_CompilesToC()
    {
        var code = @"
            import std::dos;

            fn main() -> i32 {
                let message = ""Hello"";
                let fh: *u8 = 0 as *u8;  // Null handle for test

                // This will fail at runtime, but should compile
                let write_result = dos::write_file(fh, message as *u8, 5);

                match write_result {
                    Result::Ok(bytes) => 0,
                    Result::Err(err) => 1
                }
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("write_file", result);
    }

    [Fact]
    public void Stdlib_Dos_ReadFile_CompilesToC()
    {
        var code = @"
            import std::dos;

            fn main() -> i32 {
                let buffer: [u8; 100] = [0; 100];
                let fh: *u8 = 0 as *u8;  // Null handle for test

                let read_result = dos::read_file(fh, &buffer[0] as *u8, 100);

                match read_result {
                    Result::Ok(bytes) => 0,
                    Result::Err(err) => 1
                }
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("read_file", result);
    }

    // ===================================================================================
    // ERROR MODULE TESTS
    // ===================================================================================

    [Fact]
    public void Stdlib_Error_DosLastError_ReturnsEnum()
    {
        var code = @"
            import std::error;

            fn main() -> i32 {
                let err = error::dos_last_error();
                let code = error::dos_error_to_code(err);
                0
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("dos_last_error", result);
        Assert.Contains("dos_error_to_code", result);
    }

    [Fact]
    public void Stdlib_Error_NovusErrorConversion_CompilesToC()
    {
        var code = @"
            import std::error;

            fn main() -> i32 {
                let dos_err = error::dos_error_from_code(103);  // ERROR_NO_FREE_STORE
                let novus_err = error::novus_error_from_dos(dos_err);
                let code = error::novus_error_to_code(novus_err);
                0
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("dos_error_from_code", result);
        Assert.Contains("novus_error_from_dos", result);
        Assert.Contains("novus_error_to_code", result);
    }

    [Fact]
    public void Stdlib_Error_AllErrorModules_Compile()
    {
        var code = @"
            import std::error;

            fn main() -> i32 {
                let exec_code = error::exec_error_to_code(ExecError::NoMem);
                let intuition_code = error::intuition_error_to_code(IntuitionError::NoMem);
                let graphics_code = error::graphics_error_to_code(GraphicsError::NoMem);
                0
            }
        ";

        var result = CompileAndGetIR(code);
        Assert.Contains("exec_error_to_code", result);
        Assert.Contains("intuition_error_to_code", result);
        Assert.Contains("graphics_error_to_code", result);
    }

    // ===================================================================================
    // HELPER METHODS
    // ===================================================================================

    /// <summary>
    /// Compile code and return the generated IR for inspection.
    /// For stdlib tests, we primarily verify compilation succeeds and correct functions are called.
    /// </summary>
    private static string CompileAndGetIR(string sourceCode)
    {
        var projectRoot = GetProjectRoot();
        var stdlibPath = Path.Combine(projectRoot, "Novus", "std");

        var builder = new IrBuilder();
        builder.SetStdLibPath(stdlibPath);

        var parseTree = Parse(sourceCode);

        // Build the IR module
        var module = builder.BuildModule(parseTree);

        // Convert to string representation for assertions
        var irString = module.ToString() ?? "";

        return irString;
    }
}
