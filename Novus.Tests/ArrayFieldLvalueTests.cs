using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// An array field reached through a reference must be addressed in place.
///
/// Materializing it as a value first (memcpy the whole array into a local, then index
/// that local) makes every write land in a temporary that is discarded on return: the
/// struct never changes, nothing fails, and the program silently produces wrong output.
/// This broke every f-string in the language, because StackFormatter accumulates into
/// `self.buf` - programs compiled, ran, returned 0, and printed nothing.
/// </summary>
public class ArrayFieldLvalueTests
{
    private static string GenerateC(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", BuildMode.Debug);
        return codegen.Generate();
    }

    private const string BufferSource = @"
pub struct Buffer {
    buf: [u8; 16],
    len: u32,
}

impl Buffer {
    pub fn put(&var self, c: u8) -> bool {
        self.buf[self.len] = c
        self.len = self.len + 1u32
        return true
    }

    pub fn first(&self) -> u8 {
        return self.buf[0u32]
    }
}";

    [Fact]
    public void StoreIntoArrayField_WritesThroughSelf_NotACopy()
    {
        var code = GenerateC(BufferSource);

        Assert.Contains("self->buf[", code);
        Assert.DoesNotContain("__novus_memcpy((uint8_t*)&_field_buf", code);
    }

    [Fact]
    public void ReadFromArrayField_IndexesSelf_WithoutCopyingTheArray()
    {
        var code = GenerateC(BufferSource);

        // Reading one element never justifies copying the whole array out of the struct.
        // (An unused slot declaration may still be emitted; only the copy matters here.)
        Assert.Contains("self->buf[0]", code);
        Assert.DoesNotContain("(uint8_t*)&(self->buf)", code);
    }

    [Fact]
    public void AddressOfArrayFieldElement_PointsIntoSelf()
    {
        var code = GenerateC(@"
pub struct Buffer {
    buf: [u8; 16],
    len: u32,
}

impl Buffer {
    pub fn tail(&var self) -> *u8 {
        unsafe {
            let dest: *u8 = (*u8)&self.buf[self.len]
            return dest
        }
    }
}");

        Assert.Contains("&self->buf[", code);
    }

    [Fact]
    public void BindingAnArrayField_StillCopies()
    {
        // The elision only applies to place uses. Binding the field to a new local is a
        // value use and must keep copy semantics, or mutating the local would write back
        // into the struct.
        var code = GenerateC(@"
pub struct Buffer {
    buf: [u8; 16],
    len: u32,
}

impl Buffer {
    pub fn snapshot(&self) -> [u8; 16] {
        let copy: [u8; 16] = self.buf
        return copy
    }
}");

        Assert.Contains("__novus_memcpy", code);
    }
}
