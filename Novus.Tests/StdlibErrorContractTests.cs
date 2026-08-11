using System.Text.RegularExpressions;
using Xunit;

namespace Novus.Tests;

public class StdlibErrorContractTests
{
    [Fact]
    public void EveryPublicStdlibErrorImplementsError()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(stdlib, "*.novus", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (Match declaration in Regex.Matches(source, @"\bpub\s+enum\s+(\w*Error)\b"))
            {
                var name = declaration.Groups[1].Value;
                if (!Regex.IsMatch(source, $@"\bimpl\s+Error\s+for\s+{Regex.Escape(name)}\b"))
                    missing.Add($"{Path.GetRelativePath(stdlib, file)}: {name}");
            }
        }

        Assert.Empty(missing);
    }
}
