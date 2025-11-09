using Novus.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Tomlyn;
using Xunit;

namespace Novus.Tests.LanguageServer;

public class TomlSchemaValidatorTests
{
    #region Project.toml Tests

    [Fact]
    public void ValidProjectToml_NoErrors()
    {
        var toml = @"
[package]
name = ""myapp""
version = ""1.0.0""
type = ""cli""

[build]
target_cpu = ""68020""
fpu = ""auto""
optimization_level = 2
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MissingPackageSection_ReturnsError()
    {
        var toml = @"
[build]
target_cpu = ""68020""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required [package] section", error.Message);
    }

    [Fact]
    public void MissingRequiredPackageName_ReturnsError()
    {
        var toml = @"
[package]
version = ""1.0.0""
type = ""cli""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required 'name' key", error.Message);
    }

    [Fact]
    public void MissingRequiredPackageVersion_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
type = ""cli""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required 'version' key", error.Message);
    }

    [Fact]
    public void MissingRequiredPackageType_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required 'type' key", error.Message);
    }

    [Theory]
    [InlineData("cli")]
    [InlineData("workbench")]
    [InlineData("dual")]
    [InlineData("library")]
    [InlineData("device")]
    [InlineData("handler")]
    [InlineData("resource")]
    public void ValidPackageType_NoErrors(string packageType)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""{packageType}""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidPackageType_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""invalid-type""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Invalid package type 'invalid-type'", error.Message);
        Assert.Contains("cli", error.Message);
        Assert.Contains("library", error.Message);
    }

    [Theory]
    [InlineData("68000")]
    [InlineData("68020")]
    [InlineData("68030")]
    [InlineData("68040")]
    [InlineData("68060")]
    [InlineData("auto")]
    public void ValidTargetCpu_NoErrors(string targetCpu)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
target_cpu = ""{targetCpu}""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidTargetCpu_6502_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
target_cpu = ""6502""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Invalid target_cpu '6502'", error.Message);
        Assert.Contains("68000", error.Message);
        Assert.Contains("68020", error.Message);
    }

    [Fact]
    public void InvalidTargetCpu_Z80_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
target_cpu = ""z80""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Invalid target_cpu 'z80'", error.Message);
    }

    [Theory]
    [InlineData("soft")]
    [InlineData("none")]
    [InlineData("68881")]
    [InlineData("68882")]
    [InlineData("68040")]
    [InlineData("68060")]
    [InlineData("auto")]
    public void ValidFpu_NoErrors(string fpu)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
fpu = ""{fpu}""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidFpu_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
fpu = ""invalid-fpu""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Invalid fpu mode 'invalid-fpu'", error.Message);
    }

    [Theory]
    [InlineData("OCS")]
    [InlineData("ECS")]
    [InlineData("AGA")]
    [InlineData("auto")]
    public void ValidChipset_NoErrors(string chipset)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
chipset = ""{chipset}""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidChipset_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
chipset = ""VGA""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Invalid chipset 'VGA'", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ValidOptimizationLevel_NoErrors(int level)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
optimization_level = {level}
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void InvalidOptimizationLevel_OutOfRange_ReturnsError(int level)
    {
        var toml = $@"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
optimization_level = {level}
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("optimization_level must be between 0 and 3", error.Message);
        Assert.Contains(level.ToString(), error.Message);
    }

    [Fact]
    public void InvalidOptimizationLevel_StringInsteadOfInt_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
optimization_level = ""2""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("'optimization_level' must be an integer", error.Message);
    }

    [Fact]
    public void UnknownSectionInProjectToml_ReturnsWarning()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[unknown_section]
foo = ""bar""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var warning = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("Unknown section '[unknown_section]'", warning.Message);
        Assert.Contains("[package]", warning.Message);
        Assert.Contains("[build]", warning.Message);
    }

    [Fact]
    public void UnknownKeyInPackageSection_ReturnsWarning()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""
unknown_key = ""value""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var warning = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("Unknown key 'unknown_key'", warning.Message);
        Assert.Contains("[package] section", warning.Message);
    }

    [Fact]
    public void UnknownKeyInBuildSection_ReturnsWarning()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
unknown_setting = true
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var warning = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("Unknown key 'unknown_setting'", warning.Message);
        Assert.Contains("[build] section", warning.Message);
    }

    [Fact]
    public void DependencyWithBranchButNoGit_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[dependencies]
mylib = { version = ""1.0.0"", branch = ""main"" }
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("specifies branch/tag but is missing 'git' URL", error.Message);
    }

    [Fact]
    public void DependencyWithTagButNoGit_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[dependencies]
mylib = { version = ""1.0.0"", tag = ""v1.0.0"" }
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("specifies branch/tag but is missing 'git' URL", error.Message);
    }

    [Fact]
    public void ValidDependencyWithGitAndBranch_NoErrors()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[dependencies]
mylib = { git = ""https://github.com/user/repo.git"", branch = ""main"" }
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    #endregion

    #region Workspace.toml Tests

    [Fact]
    public void ValidWorkspaceToml_NoErrors()
    {
        var toml = @"
[workspace]
members = [""app"", ""lib""]
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MissingWorkspaceSection_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required [workspace] section", error.Message);
    }

    [Fact]
    public void MissingMembersInWorkspace_ReturnsError()
    {
        var toml = @"
[workspace]
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Missing required 'members' key", error.Message);
    }

    [Fact]
    public void MembersNotAnArray_ReturnsError()
    {
        var toml = @"
[workspace]
members = ""app""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("'members' must be an array", error.Message);
    }

    [Fact]
    public void UnknownSectionInWorkspaceToml_ReturnsWarning()
    {
        var toml = @"
[workspace]
members = [""app""]

[package]
name = ""test""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        var warning = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("Unknown section '[package]'", warning.Message);
        Assert.Contains("workspace.toml should only have [workspace] section", warning.Message);
    }

    [Fact]
    public void UnknownKeyInWorkspaceSection_ReturnsWarning()
    {
        var toml = @"
[workspace]
members = [""app""]
unknown_key = ""value""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateWorkspaceToml(table, toml);

        var warning = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("Unknown key 'unknown_key'", warning.Message);
    }

    #endregion

    #region Multiple Errors Tests

    [Fact]
    public void MultipleErrors_ReturnsAllDiagnostics()
    {
        var toml = @"
[package]
name = ""test""
type = ""invalid-type""

[build]
target_cpu = ""6502""
optimization_level = 5
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Equal(4, diagnostics.Count);
        Assert.Contains(diagnostics, d => d.Message.Contains("Missing required 'version'"));
        Assert.Contains(diagnostics, d => d.Message.Contains("Invalid package type 'invalid-type'"));
        Assert.Contains(diagnostics, d => d.Message.Contains("Invalid target_cpu '6502'"));
        Assert.Contains(diagnostics, d => d.Message.Contains("optimization_level must be between 0 and 3"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyPackageName_ReturnsError()
    {
        var toml = @"
[package]
name = """"
version = ""1.0.0""
type = ""cli""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("'name' must be a non-empty string", error.Message);
    }

    [Fact]
    public void EmitAsmBoolean_NoErrors()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
emit_asm = true
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void EmitAsmNotBoolean_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
emit_asm = ""yes""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("'emit_asm' must be a boolean", error.Message);
    }

    [Fact]
    public void AsmFilesArray_NoErrors()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
asm_files = [""startup.s"", ""custom.s""]
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AsmFilesNotArray_ReturnsError()
    {
        var toml = @"
[package]
name = ""test""
version = ""1.0.0""
type = ""cli""

[build]
asm_files = ""startup.s""
";
        var table = Toml.ToModel(toml);
        var diagnostics = TomlSchemaValidator.ValidateProjectToml(table, toml);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("'asm_files' must be an array", error.Message);
    }

    #endregion
}
