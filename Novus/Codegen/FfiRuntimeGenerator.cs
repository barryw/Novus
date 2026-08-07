using System.Text;
using Novus.Compilation;

namespace Novus.Codegen;

/// <summary>Generates exact base storage and startup/cleanup for used NDK bindings.</summary>
public static class FfiRuntimeGenerator
{
    private static readonly HashSet<string> ExistingBases = new(StringComparer.Ordinal)
    {
        "_SysBase", "_DOSBase", "_IntuitionBase", "_GadToolsBase", "_GfxBase", "_DiskfontBase",
        "_WindowBase", "_LayoutBase", "_ButtonBase", "_CheckBoxBase", "_IntegerBase",
        "_RadioButtonBase", "_LabelBase", "_MUIMasterBase", "_SocketBase", "_AmiSSLBase"
    };

    public static string Generate(IEnumerable<FfiModuleMetadata> modules)
    {
        var bindings = modules
            .Where(m => m.ModuleName is not "exec" and not "dos")
            .DistinctBy(m => m.BaseSymbol)
            .OrderBy(m => m.ModuleName, StringComparer.Ordinal)
            .ToList();
        var sb = new StringBuilder();

        sb.AppendLine("; Auto-generated Novus FFI lifecycle");
        sb.AppendLine("\tsection\t__MERGED,bss");
        foreach (var binding in bindings.Where(b => !ExistingBases.Contains(b.BaseSymbol)))
        {
            sb.AppendLine($"\txdef\t{binding.BaseSymbol}");
            sb.AppendLine($"{binding.BaseSymbol}:\tds.l\t1");
        }
        foreach (var binding in bindings.Where(b => b.Kind == FfiModuleKind.Device))
            sb.AppendLine($"__novus_{binding.ModuleName}_ioreq:\tds.b\t32");

        sb.AppendLine("\tsection\t__MERGED,data");
        foreach (var binding in bindings)
        {
            sb.AppendLine($"__novus_{binding.ModuleName}_name:");
            sb.AppendLine($"\tdc.b\t'{binding.OpenName}',0");
            sb.AppendLine("\teven");
        }

        sb.AppendLine("\tsection\tCODE,code");
        sb.AppendLine("\txdef\t___novus_ffi_init");
        sb.AppendLine("\txdef\t___novus_ffi_cleanup");
        sb.AppendLine("___novus_ffi_init:");
        sb.AppendLine("\tmovem.l\td1/a0-a1/a6,-(sp)");
        sb.AppendLine("\tmove.l\t4.w,a6");
        foreach (var binding in bindings)
        {
            sb.AppendLine($"\ttst.l\t{binding.BaseSymbol}");
            sb.AppendLine($"\tbne.s\t.__novus_{binding.ModuleName}_ready");
            switch (binding.Kind)
            {
                case FfiModuleKind.Library:
                    sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_name,a1");
                    sb.AppendLine($"\tmove.l\t#{binding.MinimumVersion},d0");
                    sb.AppendLine("\tjsr\t-552(a6)\t; OpenLibrary");
                    sb.AppendLine($"\tmove.l\td0,{binding.BaseSymbol}");
                    break;
                case FfiModuleKind.Resource:
                    sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_name,a1");
                    sb.AppendLine("\tjsr\t-498(a6)\t; OpenResource");
                    sb.AppendLine($"\tmove.l\td0,{binding.BaseSymbol}");
                    break;
                case FfiModuleKind.Device:
                    sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_ioreq,a1");
                    sb.AppendLine("\tmove.w\t#32,18(a1)\t; mn_Length = sizeof(IORequest)");
                    sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_name,a0");
                    sb.AppendLine("\tmoveq\t#0,d0\t; unit 0");
                    sb.AppendLine("\tmoveq\t#0,d1\t; flags");
                    sb.AppendLine("\tjsr\t-444(a6)\t; OpenDevice");
                    sb.AppendLine("\ttst.l\td0");
                    sb.AppendLine("\tbne.s\t.__novus_ffi_failed");
                    sb.AppendLine($"\tmove.l\t__novus_{binding.ModuleName}_ioreq+20,{binding.BaseSymbol}");
                    break;
            }
            sb.AppendLine($"\ttst.l\t{binding.BaseSymbol}");
            sb.AppendLine("\tbeq\t.__novus_ffi_failed");
            sb.AppendLine($".__novus_{binding.ModuleName}_ready:");
        }
        sb.AppendLine("\tmoveq\t#1,d0");
        sb.AppendLine("\tbra.s\t.__novus_ffi_init_done");
        sb.AppendLine(".__novus_ffi_failed:");
        sb.AppendLine("\tbsr\t___novus_ffi_cleanup");
        sb.AppendLine("\tmoveq\t#0,d0");
        sb.AppendLine(".__novus_ffi_init_done:");
        sb.AppendLine("\tmovem.l\t(sp)+,d1/a0-a1/a6");
        sb.AppendLine("\trts");

        sb.AppendLine("___novus_ffi_cleanup:");
        sb.AppendLine("\tmovem.l\td0/a1/a6,-(sp)");
        sb.AppendLine("\tmove.l\t4.w,a6");
        foreach (var binding in bindings.AsEnumerable().Reverse())
        {
            if (binding.Kind == FfiModuleKind.Resource)
                continue;
            sb.AppendLine($"\ttst.l\t{binding.BaseSymbol}");
            sb.AppendLine($"\tbeq.s\t.__novus_{binding.ModuleName}_closed");
            if (binding.Kind == FfiModuleKind.Device)
            {
                sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_ioreq,a1");
                sb.AppendLine("\tjsr\t-450(a6)\t; CloseDevice");
            }
            else
            {
                sb.AppendLine($"\tmove.l\t{binding.BaseSymbol},a1");
                sb.AppendLine("\tjsr\t-414(a6)\t; CloseLibrary");
            }
            sb.AppendLine($"\tclr.l\t{binding.BaseSymbol}");
            sb.AppendLine($".__novus_{binding.ModuleName}_closed:");
        }
        sb.AppendLine("\tmovem.l\t(sp)+,d0/a1/a6");
        sb.AppendLine("\trts");
        sb.AppendLine("\tend");
        return sb.ToString();
    }
}
