using System.Text;
using Novus.Diagnostics;
using Xunit;

namespace Novus.Tests;

[CollectionDefinition("CompilerLogger", DisableParallelization = true)]
public sealed class CompilerLoggerCollection;

/// <summary>
/// Tests for the CompilerLogger infrastructure.
/// </summary>
[Collection("CompilerLogger")]
public class CompilerLoggerTests : IDisposable
{
    private readonly StringWriter _output;
    private readonly StringWriter _errorOutput;

    public CompilerLoggerTests()
    {
        _output = new StringWriter();
        _errorOutput = new StringWriter();
        CompilerLogger.SetOutput(_output, _errorOutput);
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.All, useColor: false);
    }

    public void Dispose()
    {
        CompilerLogger.Reset();
        _output.Dispose();
        _errorOutput.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Info_WritesToOutput()
    {
        CompilerLogger.Info(LogCategory.Build, "Test message");

        var output = _output.ToString();
        Assert.Contains("Test message", output);
    }

    [Fact]
    public void Info_WithFormat_InterpolatesCorrectly()
    {
        CompilerLogger.Info(LogCategory.Build, "Compiling {0} files", 5);

        var output = _output.ToString();
        Assert.Contains("Compiling 5 files", output);
    }

    [Fact]
    public void Debug_IncludesCategoryPrefix()
    {
        CompilerLogger.Debug(LogCategory.IR, "IR debug info");

        var output = _output.ToString();
        Assert.Contains("[DEBUG:IR]", output);
        Assert.Contains("IR debug info", output);
    }

    [Fact]
    public void QuietLevel_SuppressesInfoMessages()
    {
        CompilerLogger.Configure(LogLevel.Quiet, LogCategory.All);

        CompilerLogger.Info(LogCategory.Build, "Should not appear");

        var output = _output.ToString();
        Assert.Empty(output);
    }

    [Fact]
    public void QuietLevel_StillShowsErrors()
    {
        CompilerLogger.Configure(LogLevel.Quiet, LogCategory.All, useColor: false);

        CompilerLogger.Error("Error message");

        var output = _errorOutput.ToString();
        Assert.Contains("ERROR: Error message", output);
    }

    [Fact]
    public void NormalLevel_SuppressesDebugMessages()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All);

        CompilerLogger.Debug(LogCategory.IR, "Debug info");

        var output = _output.ToString();
        Assert.Empty(output);
    }

    [Fact]
    public void NormalLevel_ShowsInfoMessages()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All);

        CompilerLogger.Info(LogCategory.Build, "Info message");

        var output = _output.ToString();
        Assert.Contains("Info message", output);
    }

    [Fact]
    public void DisabledCategory_SuppressesMessages()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.Build); // Only Build enabled

        CompilerLogger.Info(LogCategory.IR, "IR message");
        CompilerLogger.Info(LogCategory.Build, "Build message");

        var output = _output.ToString();
        Assert.DoesNotContain("IR message", output);
        Assert.Contains("Build message", output);
    }

    [Fact]
    public void Warning_WritesToErrorOutput()
    {
        CompilerLogger.Warning(LogCategory.Build, "Warning message");

        var errorOutput = _errorOutput.ToString();
        Assert.Contains("WARNING: Warning message", errorOutput);
    }

    [Fact]
    public void Warning_WithFormat_InterpolatesCorrectly()
    {
        CompilerLogger.Warning(LogCategory.Build, "{0} files need updating", 3);

        var errorOutput = _errorOutput.ToString();
        Assert.Contains("3 files need updating", errorOutput);
    }

    [Fact]
    public void Step_AddsArrowPrefix()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All, useColor: false);
        CompilerLogger.Step(LogCategory.Build, "Processing file.novus");

        var output = _output.ToString();
        Assert.Contains("->", output.Replace("\u2192", "->")); // Handle unicode arrow
        Assert.Contains("Processing file.novus", output);
    }

    [Fact]
    public void Section_FormatsHeader()
    {
        CompilerLogger.Section(LogCategory.Build, "Compilation");

        var output = _output.ToString();
        Assert.Contains("=== Compilation ===", output);
    }

    [Fact]
    public void IsEnabled_ReturnsTrueWhenLevelAndCategoryMatch()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.Build | LogCategory.IR);

        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Build));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Debug, LogCategory.IR));
    }

    [Fact]
    public void IsEnabled_ReturnsFalseWhenLevelTooHigh()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All);

        Assert.False(CompilerLogger.IsEnabled(LogLevel.Debug, LogCategory.Build));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Build));
    }

    [Fact]
    public void IsEnabled_ReturnsFalseWhenCategoryDisabled()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.Build);

        Assert.False(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.IR));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Build));
    }

    [Fact]
    public void Verbose_OnlyShownAtVerboseLevel()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All);
        CompilerLogger.Verbose(LogCategory.Build, "Verbose 1");

        CompilerLogger.Configure(LogLevel.Verbose, LogCategory.All);
        CompilerLogger.Verbose(LogCategory.Build, "Verbose 2");

        var output = _output.ToString();
        Assert.DoesNotContain("Verbose 1", output);
        Assert.Contains("Verbose 2", output);
    }

    [Fact]
    public void Success_FormatsWithOkPrefix()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All, useColor: false);
        CompilerLogger.Success(LogCategory.Build, "Build complete");

        var output = _output.ToString();
        Assert.Contains("OK: Build complete", output);
    }

    [Fact]
    public void TimedOperation_LogsCompletionTime()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.All);

        using (CompilerLogger.BeginTimed(LogCategory.Build, "Test operation"))
        {
            Thread.Sleep(10); // Small delay to ensure measurable time
        }

        var output = _output.ToString();
        Assert.Contains("Test operation...", output);
        Assert.Contains("Test operation completed in", output);
        Assert.Contains("ms", output);
    }

    [Fact]
    public void MultipleCategories_CanBeCombined()
    {
        var combined = LogCategory.Build | LogCategory.IR | LogCategory.CodeGen;
        CompilerLogger.Configure(LogLevel.Debug, combined);

        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Build));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.IR));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.CodeGen));
        Assert.False(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Optimization));
    }

    [Fact]
    public void AllCategory_EnablesEverything()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.All);

        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Build));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.IR));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Toolchain));
        Assert.True(CompilerLogger.IsEnabled(LogLevel.Normal, LogCategory.Generics));
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        CompilerLogger.Configure(LogLevel.Quiet, LogCategory.None);

        CompilerLogger.Reset();

        // After reset, should be at Normal level with All categories
        Assert.Equal(LogLevel.Normal, CompilerLogger.Level);
        Assert.Equal(LogCategory.All, CompilerLogger.EnabledCategories);
    }

    [Fact]
    public void DebugSection_OnlyShownAtDebugLevel()
    {
        CompilerLogger.Configure(LogLevel.Normal, LogCategory.All);
        CompilerLogger.DebugSection(LogCategory.Build, "Debug Section 1");

        CompilerLogger.Configure(LogLevel.Debug, LogCategory.All);
        CompilerLogger.DebugSection(LogCategory.Build, "Debug Section 2");

        var output = _output.ToString();
        Assert.DoesNotContain("Debug Section 1", output);
        Assert.Contains("=== DEBUG: Debug Section 2 ===", output);
    }

    [Fact]
    public async Task Log_ThreadSafe_NoExceptions()
    {
        CompilerLogger.Configure(LogLevel.Debug, LogCategory.All);

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    CompilerLogger.Info(LogCategory.Build, "Task {0} message {1}", taskId, j);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // If we get here without exceptions, thread safety is working
        var output = _output.ToString();
        Assert.NotEmpty(output);
    }
}
