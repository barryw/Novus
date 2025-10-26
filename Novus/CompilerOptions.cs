using CommandLine;

namespace Novus;

[Verb("compile", isDefault: true, HelpText = "Compile a Novus source file to an Amiga executable")]
public class CompilerOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Input Novus source file (.novus)")]
    public string InputFile { get; set; } = "";

    [Option('o', "output", Required = false, HelpText = "Output file name (default: a.out)")]
    public string OutputFile { get; set; } = "a.out";

    [Option("cpu", Required = false, Default = "auto", HelpText = "Target CPU: auto (fat binary with runtime detection), 68000, 68020, 68030, 68040, 68060")]
    public string Cpu { get; set; } = "auto";

    [Option("fpu", Required = false, Default = "auto", HelpText = "FPU mode: auto (fat binary with runtime detection), soft (software only), 68881 (68881/68882), 68040 (built-in FPU)")]
    public string Fpu { get; set; } = "auto";

    [Option("emit-asm", Required = false, HelpText = "Emit assembly only, don't assemble/link")]
    public bool EmitAsmOnly { get; set; }

    [Option("emit-ir", Required = false, HelpText = "Emit IR (intermediate representation) to stdout")]
    public bool EmitIr { get; set; }

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation (default: /Users/barry/amiga-cc/vbcc)")]
    public string VbccPath { get; set; } = "/Users/barry/amiga-cc/vbcc";

    [Option("ndk-path", Required = false, HelpText = "Path to NDK installation (default: /Users/barry/amiga-cc/NDK3.9)")]
    public string NdkPath { get; set; } = "/Users/barry/amiga-cc/NDK3.9";

    [Option('O', "optimize", Required = false, Default = 0, HelpText = "Optimization level (0-3)")]
    public int OptimizationLevel { get; set; } = 0;

    [Option('v', "verbose", Required = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }
}
