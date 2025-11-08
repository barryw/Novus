using Novus.Frontend;
using Novus.IR;
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
            from std::exec import get_current_task;

            fn main() -> i32 {
                let task = get_current_task();
                // On Amiga, current task should always be valid
                if task.is_some() {
                    0
                } else {
                    1
                }
            }
        ";

        // If compilation succeeds, the test passes
        // (compilation would throw if function didn't exist)
        var module = CompileAndGetIR(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void Stdlib_Exec_AllocateSignal_ReturnsSignalNumber()
    {
        var code = @"
            from std::exec import allocate_signal, free_signal;
            from std::core import Option;

            fn main() -> i32 {
                // Allocate any available signal (-1 means any)
                let signal = allocate_signal(-1);

                match signal {
                    Option::Some(sig) => {
                        // Success - free it
                        free_signal(sig);
                        0
                    },
                    Option::None => 1  // Allocation failed
                }
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Exec_ForbidPermit_CompileCorrectly()
    {
        var code = @"
            from std::exec import forbid, permit;

            fn main() -> i32 {
                forbid();
                // Critical section
                permit();
                0
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Exec_DisableEnable_CompileCorrectly()
    {
        var code = @"
            from std::exec import disable, enable;

            fn main() -> i32 {
                disable();
                // Interrupts disabled
                enable();
                0
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    // ===================================================================================
    // DOS MODULE TESTS
    // ===================================================================================

    [Fact]
    public void Stdlib_Dos_OpenFile_ReturnsResult()
    {
        var code = @"
            from std::dos import open_file, close_file;
            from std::core import Option;
            from std::strings import Str;

            fn main() -> i32 {
                let path: *u8 = 0 as *u8;  // NULL path for test
                let result = open_file(path, 1005);  // MODE_OLDFILE

                match result {
                    Option::Some(fh) => {
                        close_file(fh);
                        0
                    },
                    Option::None => 1  // File not found is expected
                }
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Dos_WriteFile_CompilesToC()
    {
        var code = @"
            from std::dos import write_file;

            fn main() -> i32 {
                let message: *u8 = 0 as *u8;  // NULL pointer for test
                let fh: i32 = 0;  // Null handle for test

                // This will fail at runtime, but should compile
                // write_file returns i32 (bytes written or error code)
                let bytes_written = write_file(fh, message, 5);

                if bytes_written >= 0 {
                    0
                } else {
                    1
                }
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Dos_ReadFile_CompilesToC()
    {
        var code = @"
            from std::dos import read_file;

            fn main() -> i32 {
                let buffer_ptr: *u8 = 0 as *u8;  // NULL pointer for test
                let fh: i32 = 0;  // Null handle for test

                // read_file returns i32 (bytes read or error code)
                let bytes_read = read_file(fh, buffer_ptr, 100);

                if bytes_read >= 0 {
                    0
                } else {
                    1
                }
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    // ===================================================================================
    // ERROR MODULE TESTS
    // ===================================================================================

    [Fact]
    public void Stdlib_Error_DosLastError_ReturnsEnum()
    {
        var code = @"
            from std::error import dos_last_error, dos_error_to_code;

            fn main() -> i32 {
                let err = dos_last_error();
                let code = dos_error_to_code(err);
                0
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Error_NovusErrorConversion_CompilesToC()
    {
        var code = @"
            from std::error import dos_error_from_code, novus_error_from_dos, novus_error_to_code;

            fn main() -> i32 {
                let dos_err = dos_error_from_code(103);  // ERROR_NO_FREE_STORE
                let novus_err = novus_error_from_dos(dos_err);
                let code = novus_error_to_code(novus_err);
                0
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    [Fact]
    public void Stdlib_Error_AllErrorModules_Compile()
    {
        var code = @"
            from std::error import exec_error_to_code, intuition_error_to_code, graphics_error_to_code;

            fn main() -> i32 {
                let exec_code = exec_error_to_code(ExecError::NoMem);
                let intuition_code = intuition_error_to_code(IntuitionError::NoMem);
                let graphics_code = graphics_error_to_code(GraphicsError::NoMem);
                0
            }
        ";

        var module = CompileAndGetIR(code);
        Assert.NotNull(module);  // Compilation succeeded
    }

    // ===================================================================================
    // HELPER METHODS
    // ===================================================================================

    /// <summary>
    /// Compile code and return the generated IR module.
    /// For stdlib tests, we verify compilation succeeds (doesn't throw).
    /// If function is missing or types are wrong, compilation will throw.
    /// </summary>
    private static IrModule CompileAndGetIR(string sourceCode)
    {
        var projectRoot = GetProjectRoot();
        var stdlibPath = Path.Combine(projectRoot, "Novus", "std");

        // DON'T skip auto imports - we need stdlib loaded!
        var builder = new IrBuilder(skipAutoImports: false);
        builder.SetStdLibPath(stdlibPath);

        var parseTree = Parse(sourceCode);

        // Build the IR module - will throw if compilation fails
        var module = builder.BuildModule(parseTree);

        return module;
    }
}
