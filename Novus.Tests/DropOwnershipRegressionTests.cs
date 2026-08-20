using Novus.Compilation;

namespace Novus.Tests;

/// <summary>
/// Regression coverage for three ownership defects found through the Amiga runtime suites.
/// Each one silently produced wrong cleanup rather than a compile error, so the assertions
/// look at the generated C: a missing drop call and a missing move are both invisible in the
/// IR-level success flag.
/// </summary>
public class DropOwnershipRegressionTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string StdLibPath = Path.Combine(ProjectRoot, "Novus", "std");

    private static async Task<string> CompileToCAsync(string source)
    {
        var sourcePath = Path.Combine(
            Path.GetTempPath(), $"novus-drop-regression-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(sourcePath, source);
        try
        {
            var result = await new InProcessCompiler(StdLibPath).CompileToCAsync(sourcePath);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.CCode);
            return result.CCode!;
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private const string OwnedTypePreamble = """
        from std::core import Result

        struct Owned { value: u32 }

        impl Drop for Owned {
            fn drop(&var self) {
                self.value = 0
            }
        }

        fn make() -> Result<Owned, u32> {
            return Result::Ok(Owned { value: 1 })
        }
        """;

    /// <summary>
    /// A wildcard binds no name, but the payload still moves out of the enum. `match` always
    /// dropped it; `let ... else` abandoned it, leaking every owned payload discarded this way.
    /// </summary>
    [Fact]
    public async Task LetElseWildcardPayload_IsDropped()
    {
        var c = await CompileToCAsync(OwnedTypePreamble + """

            fn discard() {
                let Result::Ok(_) = make() else {
                    return
                }
            }

            fn main() -> i32 {
                discard()
                return 0
            }
            """);

        var discard = ExtractFunction(c, "discard");
        Assert.Contains("Owned_Drop_drop", discard, StringComparison.Ordinal);
    }

    /// <summary>
    /// The named-binding form was always correct; it is the control that proves the assertion
    /// above is testing the wildcard specifically and not something incidental.
    /// </summary>
    [Fact]
    public async Task LetElseNamedPayload_IsDropped()
    {
        var c = await CompileToCAsync(OwnedTypePreamble + """

            fn keep() {
                let Result::Ok(owned) = make() else {
                    return
                }
            }

            fn main() -> i32 {
                keep()
                return 0
            }
            """);

        var keep = ExtractFunction(c, "keep");
        Assert.Contains("Owned_Drop_drop", keep, StringComparison.Ordinal);
    }

    /// <summary>
    /// Moving a consuming parameter into a struct field whose type is an enum used to emit one
    /// compound literal, so nothing recorded the move and the parameter was still dropped on the
    /// way out - while the returned value owned the very same resource. That double free showed
    /// up as AN_FreeTwice on the live machine.
    /// </summary>
    [Fact]
    public async Task ConsumingParameterMovedIntoEnumValuedField_EndsItsOwnership()
    {
        var c = await CompileToCAsync(OwnedTypePreamble + """

            enum Slot {
                Empty,
                Held(Owned),
            }

            struct Boxed { slot: Slot }

            fn store(consuming owned: Owned) -> Boxed {
                let boxed = Boxed { slot: Slot::Held(owned) }
                return boxed
            }

            fn main() -> i32 {
                let Result::Ok(owned) = make() else {
                    return 1
                }
                let boxed = store(owned)
                return 0
            }
            """);

        var store = ExtractFunction(c, "store");

        // The move must be recorded, either by clearing the parameter's cleanup flag or by
        // zeroing the source. Without one of them the parameter is dropped after the transfer.
        Assert.True(
            store.Contains("_active = false", StringComparison.Ordinal) ||
            store.Contains("Move semantics", StringComparison.Ordinal),
            $"store() never ends the consumed parameter's ownership:\n{store}");
    }

    /// <summary>
    /// `FileSystem` is public, owns its segment only through a private enum, and has no Drop impl
    /// of its own. The private enum used to arrive from the import with no variants, so structural
    /// drop analysis read it as "nothing to clean up" and every resolved handler leaked.
    /// </summary>
    [Fact]
    public async Task ImportedTypeDroppingThroughPrivateEnum_StillDrops()
    {
        var c = await CompileToCAsync("""
            from std::core import Result
            from amiga::dos import FileSystem

            fn resolve_and_drop() {
                match FileSystem::resolve($444F5303) {
                    Result::Ok(value) => {},
                    Result::Err(_) => {},
                }
            }

            fn main() -> i32 {
                resolve_and_drop()
                return 0
            }
            """);

        var resolver = ExtractFunction(c, "resolve_and_drop");
        Assert.Contains("LoadedSegment_Drop_drop", resolver, StringComparison.Ordinal);
    }

    /// <summary>
    /// Return the body of the generated C function for <paramref name="name"/>. Generated names
    /// carry both a module prefix and an overload suffix (`novus_mod_x_1234_store__Owned`), so
    /// match the identifier ahead of the parameter list on containment rather than equality.
    /// </summary>
    private static string ExtractFunction(string cCode, string name)
    {
        var lines = cCode.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var open = line.IndexOf('(');
            if (open <= 0 || !line.TrimEnd().EndsWith("{", StringComparison.Ordinal))
                continue;
            var signature = line[..open];
            if (!signature.Contains(name, StringComparison.Ordinal))
                continue;

            var body = new List<string>();
            for (var cursor = index; cursor < lines.Length; cursor++)
            {
                body.Add(lines[cursor]);
                if (lines[cursor].StartsWith("}", StringComparison.Ordinal))
                    break;
            }
            return string.Join('\n', body);
        }

        Assert.Fail($"generated C has no function named '{name}'");
        return string.Empty;
    }
}
