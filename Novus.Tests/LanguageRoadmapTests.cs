using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using System.Text.RegularExpressions;
using Xunit;

namespace Novus.Tests;

public class LanguageRoadmapTests
{
    private static readonly Regex NumericTypeSuffix = new(
        @"(?:(?:0[xX][0-9A-Fa-f_]+|\$[0-9A-Fa-f_]+|%[01_]+|\b[0-9][0-9_]*)(?:u|i)(?:8|16|32|64)|(?:\b[0-9][0-9_]*\.[0-9_]+|\B\.[0-9_]+)(?:f32|f64|fixed16|fixed32))\b",
        RegexOptions.Compiled);

    private static (IrModule Module, IrBuilder Builder) Build(string source)
    {
        var lexer = new NovusLexer(new AntlrInputStream(source));
        var parser = new NovusParser(new AngleBracketTokenStream(lexer));
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);
        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
        return (module, builder);
    }

    [Fact]
    public async Task RoadmapLibraryOperations_CompileThroughStdlib()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var sourcePath = Path.Combine(Path.GetTempPath(), $"novus-roadmap-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(sourcePath, """
            from std::core import Option, Result
            from std::memory import MemoryError
            from std::memory::slice import Slice, MutSlice

            enum ReadError { Failed }

            fn read(ok: bool) -> Result<u32, ReadError> {
                if ok { return Result::Ok(1) }
                return Result::Err(ReadError::Failed)
            }

            fn remap(ok: bool) -> Result<u32, ReadError> {
                return read(ok).or_error(ReadError::Failed)
            }

            fn copy(target: &var MutSlice<u8>, source: Slice<u8>) -> Result<(), MemoryError> {
                target.fill(0)
                return target.copy_from(source)
            }

            fn same<T>(left: T, right: T, ignored: u8) {}
            fn same<T>(left: T, right: T) { same(left, right, 0) }
            fn generic_overload() { same(1, 1) }
            """);
        try
        {
            var result = await new InProcessCompiler(stdlib).CompileToCAsync(sourcePath);
            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void TestModule_ProvidesConciseGenericAssertions()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var source = File.ReadAllText(Path.Combine(stdlib, "test/test.novus"));
        Assert.Contains("pub fn expect_ok<T, E>(result: Result<T, E>)", source);
        Assert.Contains("pub fn expect_err<T, E>(result: Result<T, E>)", source);
        Assert.Contains("pub fn expect_some<T>(value: Option<T>)", source);
        Assert.Contains("pub fn expect_none<T>(value: Option<T>)", source);
        Assert.Contains("pub fn expect_eq<T>(actual: T, expected: T)", source);
        Assert.Contains("pub fn expect_ne<T>(actual: T, expected: T)", source);
    }

    [Fact]
    public void NativeIndexAliases_ArePointerSizedOn68k()
    {
        var (module, _) = Build("fn size(index: usize, delta: isize) -> usize { return index }");
        var function = Assert.Single(module.Functions);
        Assert.Equal(IrIntType.U32, function.Parameters[0].Type);
        Assert.Equal(IrIntType.I32, function.Parameters[1].Type);
    }

    [Fact]
    public void Enumerate_LowersWithoutAnAdapterTypeOrCall()
    {
        var (module, _) = Build("""
            fn sum(values: [u16; 3]) -> u32 {
                var total: u32 = 0
                for (index, value) in values.enumerate() {
                    total += index + (value as u32)
                }
                return total
            }
            """);
        var function = Assert.Single(module.Functions);
        Assert.DoesNotContain(function.BasicBlocks.SelectMany(block => block.Instructions)
            .OfType<IrCall>(), call => call.FunctionName.Contains("enumerate"));
        Assert.Contains(function.LocalVariables, variable => variable.Name == "index");
        Assert.Contains(function.LocalVariables, variable => variable.Name == "value");
    }

    [Fact]
    public void ArrayFill_UsesOneProvenLoopBound()
    {
        var (module, _) = Build("""
            fn clear() -> u8 {
                var bytes: [u8] = [1, 2, 3]
                bytes.fill(0)
                return bytes[0]
            }
            """);
        var stores = module.GetFunction("clear")!.BasicBlocks.SelectMany(block => block.Instructions)
            .OfType<IrIndexStore>().ToList();
        Assert.Single(stores);
        Assert.Equal(IrBoundsCheckMode.Proven, stores[0].BoundsCheck);
    }

    [Fact]
    public void ByteAndFourCcLiterals_AreCompileTimeValues()
    {
        var (module, _) = Build("""
            const RDSK: u32 = fourcc"RDSK"
            fn byte() -> u8 { return b'P' }
            fn classify(bytes: [u8; 3]) -> u8 {
                return match bytes { b"PFS" => 1, _ => 0 }
            }
            """);
        Assert.Equal(unchecked((int)0x5244534B), module.Constants["RDSK"].Value);
        var byteReturn = module.GetFunction("byte")!.BasicBlocks
            .SelectMany(block => block.Instructions).OfType<IrReturn>().Single();
        Assert.Equal((long)'P', Assert.IsType<IrConstant>(byteReturn.Value).Value);
        Assert.Contains(module.GetFunction("classify")!.BasicBlocks.SelectMany(block => block.Instructions),
            instruction => instruction is IrIndexAccess { BoundsCheck: IrBoundsCheckMode.Proven });
    }

    [Fact]
    public void ReadOnlyGetter_CanBeUsedAsAProperty()
    {
        var (module, _) = Build("""
            struct Size { bytes: u32 }
            impl Size {
                fn block_bytes(&self) -> u32 { return self.bytes }
            }
            fn read(size: Size) -> u32 { return size.block_bytes }
            """);
        Assert.Contains(module.GetFunction("read")!.BasicBlocks.SelectMany(block => block.Instructions)
            .OfType<IrCall>(), call => call.FunctionName == "Size::block_bytes");
    }

    [Fact]
    public void PostfixPointerCondition_LowersToBoolBeforeOptimization()
    {
        var (module, _) = Build("""
            fn present(value: *u8) -> bool {
                return false unless value
                return true
            }
            """);
        var function = module.GetFunction("present")!;

        new ConstantPropagation(function, module).Propagate();

        var validation = new IrValidator().Validate(module);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.All(function.BasicBlocks.SelectMany(block => block.Instructions)
            .OfType<IrConditionalBranch>(), branch => Assert.IsType<IrBoolType>(branch.Condition.Type));
    }

    [Fact]
    public void RepresentedEnum_PreservesWidthAndDiscriminants()
    {
        var (module, builder) = Build("""
            enum GadgetId: u16 {
                Device = 1
                Partitions
                Save = 8
            }
            fn raw(id: GadgetId) -> u16 { return id as u16 }
            """);
        var type = Assert.Single(module.Enums);
        Assert.Equal(2, type.SizeInBytes);
        Assert.Equal([1L, 2L, 8L], type.Variants.Select(variant => variant.Tag));

        var registry = new TypeRegistry();
        registry.RegisterModule(module);
        var header = CCodeGenerator.GenerateSharedTypesHeader(registry, module.Functions);
        Assert.Contains("typedef uint16_t GadgetId;", header);
    }

    [Fact]
    public void MaintainedNovusSources_UseContextualNumericLiterals()
    {
        var root = PathUtility.FindProjectRoot()
            ?? throw new InvalidOperationException("Novus project root not found");
        var ignoredDirectories = new HashSet<string>(StringComparer.Ordinal)
            { ".git", ".novus-cache", "bin", "obj", "build" };

        var offenders = Directory.EnumerateFiles(root, "*.novus", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)
                .Any(ignoredDirectories.Contains))
            .Where(path => NumericTypeSuffix.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }
}
