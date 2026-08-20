using Novus.CCompiler;
using Novus.Assembler;
using Novus.Codegen.M68k;

namespace Novus.Commands;

public static class CCompileCommand
{
    public static int Run(CCompileOptions options)
    {
        if (!new[] { "68020", "68030", "68040", "68060" }.Contains(options.Cpu))
        {
            Console.Error.WriteLine($"error: unsupported CPU '{options.Cpu}'; Novus requires 68020 or newer");
            return Program.EXIT_USAGE;
        }
        try
        {
            var module = new CFrontend(File.ReadAllText(options.InputFile)).Parse();
            var assembly = new M68kCodeGenerator(module, [], options.Cpu).Generate();
            var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile));
            if (directory != null) Directory.CreateDirectory(directory);
            if (options.EmitAssembly)
                File.WriteAllText(options.OutputFile, assembly);
            else
                File.WriteAllBytes(options.OutputFile,
                    new M68kAssembler().Assemble(assembly, Path.GetFileNameWithoutExtension(options.InputFile) + ".s"));
            return Program.EXIT_SUCCESS;
        }
        catch (IOException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return Program.EXIT_USAGE;
        }
        catch (FormatException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return Program.EXIT_COMPILE_ERROR;
        }
    }
}
