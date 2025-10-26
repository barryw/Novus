namespace Novus.Diagnostics;

/// <summary>
/// Represents a location in source code
/// </summary>
public class SourceLocation
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public int Length { get; }
    public string SourceLine { get; }

    public SourceLocation(string filePath, int line, int column, int length, string sourceLine)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        Length = length;
        SourceLine = sourceLine;
    }

    public override string ToString()
    {
        return $"{FilePath}:{Line}:{Column}";
    }
}
