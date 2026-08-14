using System.Text.RegularExpressions;
using Xunit;

namespace Novus.Tests;

public class StdlibBorrowContractTests
{
    private static string Read(string relativePath)
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        return File.ReadAllText(Path.Combine(stdlib, relativePath));
    }

    [Fact]
    public void FoundationalCollectionsUseBorrowedAccessAndConsumingInsertion()
    {
        var vec = Read("collections/vec.novus");
        var deque = Read("collections/vecdeque.novus");
        var ring = Read("collections/ringbuffer.novus");
        var small = Read("collections/smallvec.novus");

        Assert.Contains("pub fn get(&self, index: usize) -> Option<&T>", vec);
        Assert.Contains("pub fn get_mut(&var self, index: usize) -> Option<&var T>", vec);
        Assert.Contains("pub fn push(&var self, consuming value: T)", vec);
        Assert.Contains("pub fn get(&self, index: usize) -> Option<&T>", deque);
        Assert.Contains("pub fn get_mut(&var self, index: usize) -> Option<&var T>", deque);
        Assert.Contains("pub fn push_back(&var self, consuming value: T)", ring);
        Assert.DoesNotContain("-> Option<*T>", ring);
        Assert.Contains("pub fn get_mut(&var self, index: usize) -> Option<&var T>", ring);
        Assert.Contains("pub fn push(&var self, consuming value: T)", small);
        Assert.Contains("pub fn set(&var self, index: usize, consuming value: T)", small);
    }

    [Fact]
    public void CollectionViewsAndIteratorsAreOwnerTied()
    {
        var slice = Read("memory/slice.novus");
        var map = Read("collections/hashmap.novus");
        var slotMap = Read("collections/slotmap.novus");

        Assert.Contains("ptr: &T", slice);
        Assert.Contains("ptr: &var T", slice);
        Assert.Contains("map: &HashMap<K, V>", map);
        Assert.Contains("map: &var HashMap<K, V>", map);
        Assert.Contains("key: &K", map);
        Assert.Contains("value: &var V", map);
        Assert.DoesNotContain("entry_addr", map);
        Assert.Contains("map: &SlotMap<T>", slotMap);
        Assert.Contains("value: &T", slotMap);
        Assert.Contains("pub fn get(&self, key: SlotKey) -> Option<&T>", slotMap);
        Assert.Contains("pub fn get_mut(&var self, key: SlotKey) -> Option<&var T>", slotMap);

        var freeList = Read("collections/freelist.novus");
        Assert.Contains("pub fn get(&self, idx: usize) -> Option<&T>", freeList);
        Assert.Contains("pub fn get_mut(&var self, idx: usize) -> Option<&var T>", freeList);
    }

    [Fact]
    public void StringAndAmigaViewsDoNotEraseOwnersIntoRawFields()
    {
        var strings = Read("string/core.novus");
        var draw = Read("amiga/sys/graphics/draw.novus");
        var area = Read("amiga/sys/graphics/area.novus");
        var args = Read("amiga/sys/workbench/args.novus");

        Assert.Matches(@"pub struct Str\s*\{(?:\s*///[^\r\n]*)*\s*ptr: &u8,", strings);
        Assert.Contains("@unsafe\n    pub fn new(ptr: &u8, len: usize)", strings);
        Assert.Contains("pub fn borrow_raw(ptr: *u8, len: usize)", strings);
        Assert.Contains("pub fn borrow_raw(rp: *RastPort)", draw);
        Assert.Contains("rp: &RastPort", draw);
        Assert.Contains("@borrows(rp)", draw);
        Assert.Contains("rp: &RastPort", area);
        Assert.Contains("name: Option<Str>", args);
        Assert.DoesNotContain("name_ptr: *u8", args);
    }

    [Fact]
    public void EveryIntoRawConsumesAndDisarmsItsOwner()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var failures = new List<string>();

        foreach (var file in Directory.EnumerateFiles(stdlib, "*.novus", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (Match method in Regex.Matches(source,
                         @"(?:pub\s+)?fn\s+into_raw\((?<parameters>[^)]*)\)[^{]*\{(?<body>[^}]*)\}"))
            {
                var parameters = method.Groups["parameters"].Value;
                var body = method.Groups["body"].Value;
                if (!parameters.Contains("consuming self", StringComparison.Ordinal) ||
                    !Regex.IsMatch(body, @"self\.\w+\s*="))
                {
                    failures.Add(Path.GetRelativePath(stdlib, file));
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void GenericOwnershipTransfersAreExplicitlyConsuming()
    {
        Assert.Contains("insert(&var self, consuming key: K, consuming value: V)",
            Read("collections/hashmap.novus"));
        Assert.Contains("insert(&var self, consuming value: T)",
            Read("collections/hashset.novus"));
        Assert.Contains("insert(&var self, consuming value: T)",
            Read("collections/slotmap.novus"));
        Assert.Contains("send(&self, consuming value: T)",
            Read("amiga/sys/exec/channel.novus"));
        Assert.Contains("new(consuming value: T)",
            Read("memory/block.novus"));
        Assert.Contains("new(consuming value: T)",
            Read("async/future.novus"));
        Assert.Contains("new(consuming memory: MemoryBlock", Read("amiga/sys/graphics/bitmap.novus"));
        Assert.Contains("self.active = false", Read("amiga/sys/graphics/bitmap.novus"));
        Assert.Contains("from_sprites(consuming plane01: SpriteData, consuming plane23: SpriteData)",
            Read("amiga/sys/graphics/sprite.novus"));
        Assert.Contains("connect(&self, consuming tcp: TcpStream", Read("net/tls.novus"));
        Assert.Contains("accept(&self, consuming tcp: TcpStream", Read("net/tls.novus"));
        Assert.Contains("block_on_sleep(consuming sleep_future: Sleep)", Read("async/executor.novus"));
        Assert.Contains("fn index(&self, consuming idx: I)", Read("core.novus"));
        Assert.Contains("fn index_set(&var self, consuming idx: I, consuming value: T)", Read("core.novus"));
        Assert.Contains("fn call_once(consuming self, consuming args: Args)", Read("core.novus"));
    }

    [Fact]
    public void StatefulDropsAndGuardsUseMutableOwnerTiedBorrows()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var immutableDrops = Directory.EnumerateFiles(stdlib, "*.novus", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("fn drop(&self)", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(stdlib, file));
        var exec = Read("amiga/sys/exec/exec.novus");

        Assert.Empty(immutableDrops);
        Assert.Contains("owner: &SemaphoreHandle", exec);
        Assert.Contains("data: &var T", exec);
        Assert.Contains("data: &T", exec);
        Assert.Contains("pub fn get_time(&var self)", Read("amiga/sys/timer/device.novus"));
        Assert.Contains("pub fn delay(&var self", Read("amiga/sys/timer/device.novus"));
        Assert.Contains("pub fn play(&var self", Read("amiga/sys/device/audio.novus"));
        Assert.Contains("pub fn stop(&var self)", Read("amiga/sys/device/audio.novus"));
    }

    [Fact]
    public async Task NativeIndexTypesCompileAcrossCanonicalPortableSurfaces()
    {
        var stdlib = PathUtility.FindStdLibPath()
            ?? throw new InvalidOperationException("Novus standard library not found");
        var path = Path.Combine(Path.GetTempPath(), $"novus-native-index-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(path, """
            from std::collections import ArrayVec, BitSet, FreeList, HashMap, SmallVec, SlotMap, Vec, VecDeque
            from std::core import Result
            from std::memory import Buffer, MemoryError
            from std::string import FixedString

            fn total(index: usize) -> Result<usize, MemoryError> {
                let buffer = Buffer::new(8)?
                let values = ArrayVec::<u8, 4>::new()
                let text = FixedString::<16>::new()
                return Result::Ok(buffer.len() + values.capacity() + text.len() + index)
            }
            """);
        try
        {
            var result = await new InProcessCompiler(stdlib).CompileToCAsync(path);
            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
