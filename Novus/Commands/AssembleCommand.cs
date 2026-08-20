using Novus.Assembler;

namespace Novus.Commands;

public static class AssembleCommand
{
    public static int Run(AssembleOptions options)
    {
        if (!new[] { "68020", "68030", "68040", "68060" }.Contains(options.Cpu))
        {
            Console.Error.WriteLine($"error: unsupported CPU '{options.Cpu}'; Novus requires 68020 or newer");
            return Program.EXIT_USAGE;
        }
        try
        {
            var source = File.ReadAllText(options.InputFile);
            var result = new M68kAssembler().Assemble(source, Path.GetFileName(options.InputFile));
            var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile));
            if (directory != null) Directory.CreateDirectory(directory);
            File.WriteAllBytes(options.OutputFile, result);
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
