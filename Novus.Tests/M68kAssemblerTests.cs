using Novus.Assembler;
using System.Security.Cryptography;

namespace Novus.Tests;

public sealed class M68kAssemblerTests
{
    [Fact]
    public void EmitsVasmCompatibleHunkForExportedFunction()
    {
        var source = """
            section "CODE",code
            xdef _answer
            _answer:
                moveq #42,d0
                rts
            """;

        var objectBytes = new M68kAssembler().Assemble(source, "novus-as-reference.s");

        Assert.Equal(
            "000003e7000000056e6f7675732d61732d7265666572656e63652e73000003e8" +
            "00000001434f4445000003e900000001702a4e75000003ef010000025f616e73" +
            "776572000000000000000000000003f0000000025f616e737765720000000000" +
            "00000000000003f2",
            Convert.ToHexString(objectBytes).ToLowerInvariant());
    }

    [Fact]
    public void ResolvesShortBranchesAndRejectsBadInputs()
    {
        var bytes = new M68kAssembler().Assemble("""
            section "CODE",code
            start:
                moveq #0,d0
                beq.s done
                nop
            done:
                rts
            """);

        Assert.Contains("700067024e714e75", Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.Throws<FormatException>(() => new M68kAssembler().Assemble("moveq #128,d0"));
        Assert.Throws<FormatException>(() => new M68kAssembler().Assemble("rts\nrts extra"));
    }

    [Fact]
    public void EmitsVasmCompatibleExternalReference()
    {
        var source = """
            section "CODE",code
            xref _external
            xdef _caller
            _caller:
                jsr _external
                rts
            """;

        var objectBytes = new M68kAssembler().Assemble(source, "novus-as-xref-reference.s");

        Assert.Equal(
            "000003e7000000076e6f7675732d61732d787265662d7265666572656e63" +
            "652e73000000000003e800000001434f4445000003e9000000024eb90000" +
            "00004e75000003ef810000035f65787465726e616c000000000000010000" +
            "0002010000025f63616c6c6572000000000000000000000003f000000002" +
            "5f63616c6c6572000000000000000000000003f2",
            Convert.ToHexString(objectBytes).ToLowerInvariant());
        Assert.Throws<FormatException>(() => new M68kAssembler().Assemble("jsr _missing"));
    }

    [Fact]
    public void EmitsVasmCompatibleProductionLibraryStub()
    {
        var source = """
            xref _ButtonBase
            section _BUTTON_GetClass_stub,code
            xdef _BUTTON_GetClass
            _BUTTON_GetClass:
                movem.l a6,-(sp)
                movea.l _ButtonBase,a6
                jsr -30(a6)
                movem.l (sp)+,a6
                rts
            """;

        var objectBytes = new M68kAssembler().Assemble(source, "button_stubs.s");

        Assert.Equal("94000f977f1189dca420b6d99bb9452e8d2e07d27bbe207762727cf95c82c084",
            Convert.ToHexString(SHA256.HashData(objectBytes)).ToLowerInvariant());
    }

    [Fact]
    public void AssemblesTightCBackendOutputByteForByteLikeVasm()
    {
        var source = """
            section code,code
            xdef add
            add:
                move.l 4(sp),d0
                move.l 8(sp),d1
                add.l d1,d0
            add_epilogue:
                rts
            """;

        var objectBytes = new M68kAssembler().Assemble(source, "novus-cc-reference.s");

        Assert.Equal("b15b58119303f4fe8e200eda573feaa346b0838ad4d16c22f2f8ec27210324da",
            Convert.ToHexString(SHA256.HashData(objectBytes)).ToLowerInvariant());
    }
}
