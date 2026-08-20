using System.Text;
using Novus.Compilation;

namespace Novus.Codegen;

/// <summary>Generates exact base storage and startup/cleanup for used NDK bindings.</summary>
public static class FfiRuntimeGenerator
{
    public static string Generate(IEnumerable<FfiModuleMetadata> modules, bool includeWorkbenchStartup = true)
    {
        var bindings = modules
            .Where(m => m.ModuleName is not "exec")
            .Where(m => m.Kind != FfiModuleKind.CallerSupplied)
            .DistinctBy(m => m.BaseSymbol)
            .OrderBy(m => m.ModuleName == "dos" ? 0 : m.ModuleName == "intuition" ? 1 : 2)
            .ThenBy(m => m.ModuleName, StringComparer.Ordinal)
            .ToList();
        var useCompactLibraryTable = bindings.Count >= 3 && bindings.All(binding => !binding.Optional) &&
                                     bindings.All(binding => binding.Kind == FfiModuleKind.Library);
        var sb = new StringBuilder();

        sb.AppendLine("; Auto-generated Novus FFI lifecycle");
        sb.AppendLine("\tsection\t__MERGED,bss");
        sb.AppendLine("\txdef\t_SysBase");
        sb.AppendLine("_SysBase:\tds.l\t1");
        if (includeWorkbenchStartup)
        {
            sb.AppendLine("\txdef\t_WBStartupMsg");
            sb.AppendLine("_WBStartupMsg:\tds.l\t1");
        }
        foreach (var binding in bindings.Where(binding => binding.Kind != FfiModuleKind.LazyLibrary))
        {
            sb.AppendLine($"\txdef\t{binding.BaseSymbol}");
            sb.AppendLine($"{binding.BaseSymbol}:\tds.l\t1");
        }
        foreach (var binding in bindings.Where(b => b.Kind == FfiModuleKind.Device))
        {
            sb.AppendLine($"__novus_{binding.ModuleName}_ioreq:\tds.l\t1");
            sb.AppendLine($"__novus_{binding.ModuleName}_port:\tds.l\t1");
        }

        sb.AppendLine("\tsection\t__MERGED,data");
        foreach (var binding in bindings.Where(binding => binding.Kind != FfiModuleKind.LazyLibrary))
        {
            sb.AppendLine($"__novus_{binding.ModuleName}_name:");
            sb.AppendLine($"\tdc.b\t'{binding.OpenName}',0");
            sb.AppendLine("\teven");
        }
        if (useCompactLibraryTable)
        {
            sb.AppendLine("__novus_ffi_table:");
            foreach (var binding in bindings)
            {
                sb.AppendLine($"\tdc.l\t__novus_{binding.ModuleName}_name");
                sb.AppendLine($"\tdc.l\t{binding.BaseSymbol}");
                sb.AppendLine($"\tdc.w\t{binding.MinimumVersion}");
                sb.AppendLine("\tdc.w\t0\t; keep every table entry long-aligned");
            }
            sb.AppendLine("__novus_ffi_table_end:");
            sb.AppendLine("\tdc.l\t0");
        }

        sb.AppendLine("\tsection\tCODE,code");
        sb.AppendLine("\txdef\t___novus_ffi_init");
        sb.AppendLine("\txdef\t___novus_ffi_cleanup");
        sb.AppendLine("\txdef\t___novus_ffi_cleanup_lazy");
        sb.AppendLine("\txref\t___novus_library_not_found");
        sb.AppendLine("___novus_ffi_init:");
        if (useCompactLibraryTable)
        {
            sb.AppendLine("\tmovem.l\td4/a2/a4/a6,-(sp)");
            sb.AppendLine("\tmove.l\t4.w,a6");
            sb.AppendLine("\tlea\t__novus_ffi_table,a4");
            sb.AppendLine(".__novus_ffi_open_next:");
            sb.AppendLine("\tmove.l\t(a4)+,d4");
            sb.AppendLine("\tbeq\t.__novus_ffi_opened");
            sb.AppendLine("\tmovea.l\t(a4)+,a2");
            sb.AppendLine("\tmoveq\t#0,d0");
            sb.AppendLine("\tmove.w\t(a4)+,d0");
            sb.AppendLine("\taddq.w\t#2,a4");
            sb.AppendLine("\ttst.l\t(a2)");
            sb.AppendLine("\tbne\t.__novus_ffi_open_next");
            sb.AppendLine("\tmovea.l\td4,a1");
            sb.AppendLine("\tjsr\t-552(a6)\t; OpenLibrary");
            sb.AppendLine("\tmove.l\td0,(a2)");
            sb.AppendLine("\tbne\t.__novus_ffi_open_next");
            sb.AppendLine("\tmoveq\t#0,d1");
            sb.AppendLine("\tmove.w\t-4(a4),d1");
            sb.AppendLine("\tmove.l\td1,-(sp)");
            sb.AppendLine("\tmove.l\t-12(a4),-(sp)");
            sb.AppendLine("\tjsr\t___novus_library_not_found");
            sb.AppendLine("\taddq.l\t#8,sp");
            sb.AppendLine("\tbsr\t___novus_ffi_cleanup");
            sb.AppendLine("\tmoveq\t#0,d0");
            sb.AppendLine("\tbra\t.__novus_ffi_init_done");
            sb.AppendLine(".__novus_ffi_opened:");
            sb.AppendLine("\tmoveq\t#1,d0");
            sb.AppendLine(".__novus_ffi_init_done:");
            sb.AppendLine("\tmovem.l\t(sp)+,d4/a2/a4/a6");
            sb.AppendLine("\trts");
        }
        else
        {
            sb.AppendLine("\tmovem.l\td1/d6/a0-a2/a6,-(sp)");
            sb.AppendLine("\tmove.l\t4.w,a6");
            foreach (var binding in bindings)
            {
                if (binding.Kind == FfiModuleKind.LazyLibrary)
                    continue;
                sb.AppendLine($"\ttst.l\t{binding.BaseSymbol}");
                sb.AppendLine($"\tbne\t.__novus_{binding.ModuleName}_ready");
                sb.AppendLine($"\tlea\t__novus_{binding.ModuleName}_name,a0");
                sb.AppendLine(binding.MinimumVersion <= 127
                    ? $"\tmoveq\t#{binding.MinimumVersion},d1"
                    : $"\tmove.l\t#{binding.MinimumVersion},d1");
                sb.AppendLine("\tmovea.l\ta0,a2");
                sb.AppendLine("\tmove.l\td1,d6");
                switch (binding.Kind)
                {
                    case FfiModuleKind.Library:
                        sb.AppendLine("\tmove.l\ta0,a1");
                        sb.AppendLine("\tmove.l\td1,d0");
                        sb.AppendLine("\tjsr\t-552(a6)\t; OpenLibrary");
                        sb.AppendLine($"\tmove.l\td0,{binding.BaseSymbol}");
                        sb.AppendLine(binding.Optional
                        ? $"\tbeq\t.__novus_{binding.ModuleName}_ready"
                            : "\tbeq\t.__novus_ffi_failed");
                        break;
                    case FfiModuleKind.Resource:
                        sb.AppendLine("\tmove.l\ta0,a1");
                        sb.AppendLine("\tjsr\t-498(a6)\t; OpenResource");
                        sb.AppendLine($"\tmove.l\td0,{binding.BaseSymbol}");
                        sb.AppendLine("\tbeq\t.__novus_ffi_failed");
                        break;
                    case FfiModuleKind.Device:
                        sb.AppendLine("\tjsr\t-666(a6)\t; CreateMsgPort");
                        sb.AppendLine($"\tmove.l\td0,__novus_{binding.ModuleName}_port");
                        sb.AppendLine("\tbeq\t.__novus_ffi_failed");
                        sb.AppendLine("\tmovea.l\td0,a0");
                        sb.AppendLine("\tmoveq\t#32,d0\t; sizeof(IORequest)");
                        sb.AppendLine("\tjsr\t-654(a6)\t; CreateIORequest");
                        sb.AppendLine($"\tmove.l\td0,__novus_{binding.ModuleName}_ioreq");
                        sb.AppendLine("\tbeq\t.__novus_ffi_failed");
                        sb.AppendLine("\tmovea.l\td0,a1");
                        sb.AppendLine("\tmovea.l\ta2,a0\t; device name");
                        sb.AppendLine(binding.DeviceUnit is >= -128 and <= 127
                            ? $"\tmoveq\t#{binding.DeviceUnit},d0\t; unit"
                            : $"\tmove.l\t#{binding.DeviceUnit},d0\t; unit");
                        sb.AppendLine("\tmoveq\t#0,d1\t; flags");
                        sb.AppendLine("\tjsr\t-444(a6)\t; OpenDevice");
                        sb.AppendLine("\ttst.l\td0");
                    sb.AppendLine("\tbne\t.__novus_ffi_failed");
                        sb.AppendLine($"\tmove.l\t20(a1),{binding.BaseSymbol}");
                        break;
                }
                if (binding.Kind == FfiModuleKind.Device)
                {
                    sb.AppendLine($"\ttst.l\t{binding.BaseSymbol}");
                    sb.AppendLine("\tbeq\t.__novus_ffi_failed");
                }
                sb.AppendLine($".__novus_{binding.ModuleName}_ready:");
            }
            sb.AppendLine("\tmoveq\t#1,d0");
            sb.AppendLine("\tbra\t.__novus_ffi_init_done");
            sb.AppendLine(".__novus_ffi_failed:");
            sb.AppendLine("\tmove.l\td6,-(sp)");
            sb.AppendLine("\tmove.l\ta2,-(sp)");
            sb.AppendLine("\tjsr\t___novus_library_not_found");
            sb.AppendLine("\taddq.l\t#8,sp");
            sb.AppendLine("\tbsr\t___novus_ffi_cleanup");
            sb.AppendLine("\tmoveq\t#0,d0");
            sb.AppendLine(".__novus_ffi_init_done:");
            sb.AppendLine("\tmovem.l\t(sp)+,d1/d6/a0-a2/a6");
            sb.AppendLine("\trts");
        }

        sb.AppendLine("___novus_ffi_cleanup:");
        sb.AppendLine("\tbsr\t___novus_ffi_cleanup_lazy");
        if (useCompactLibraryTable)
        {
            sb.AppendLine("\tmovem.l\td4/a2/a4/a6,-(sp)");
            sb.AppendLine("\tmove.l\t4.w,a6");
            sb.AppendLine($"\tmoveq\t#{bindings.Count - 1},d4");
            sb.AppendLine("\tlea\t__novus_ffi_table_end,a4");
            sb.AppendLine(".__novus_ffi_close_next:");
            sb.AppendLine("\tsuba.w\t#12,a4");
            sb.AppendLine("\tmovea.l\t4(a4),a2");
            sb.AppendLine("\tmove.l\t(a2),d0");
            sb.AppendLine("\tbeq\t.__novus_ffi_close_skip");
            sb.AppendLine("\tmovea.l\td0,a1");
            sb.AppendLine("\tjsr\t-414(a6)\t; CloseLibrary");
            sb.AppendLine("\tclr.l\t(a2)");
            sb.AppendLine(".__novus_ffi_close_skip:");
            sb.AppendLine("\tdbra\td4,.__novus_ffi_close_next");
            sb.AppendLine("\tmovem.l\t(sp)+,d4/a2/a4/a6");
            sb.AppendLine("\trts");
        }
        else
        {
            sb.AppendLine("\tmovem.l\td0/a1/a6,-(sp)");
            sb.AppendLine("\tmove.l\t4.w,a6");
            foreach (var binding in bindings.Where(binding => binding.Kind != FfiModuleKind.LazyLibrary).Reverse())
            {
                if (binding.Kind == FfiModuleKind.Resource)
                    continue;
                if (binding.Kind == FfiModuleKind.Device)
                {
                    sb.AppendLine($"\tmove.l\t{binding.BaseSymbol},d0");
                    sb.AppendLine($"\tbeq\t.__novus_{binding.ModuleName}_device_closed");
                    sb.AppendLine($"\tmovea.l\t__novus_{binding.ModuleName}_ioreq,a1");
                    sb.AppendLine("\tjsr\t-450(a6)\t; CloseDevice");
                    sb.AppendLine($"\tclr.l\t{binding.BaseSymbol}");
                    sb.AppendLine($".__novus_{binding.ModuleName}_device_closed:");
                    sb.AppendLine($"\tmove.l\t__novus_{binding.ModuleName}_ioreq,d0");
                    sb.AppendLine($"\tbeq\t.__novus_{binding.ModuleName}_request_deleted");
                    sb.AppendLine("\tmovea.l\td0,a0");
                    sb.AppendLine("\tjsr\t-660(a6)\t; DeleteIORequest");
                    sb.AppendLine($"\tclr.l\t__novus_{binding.ModuleName}_ioreq");
                    sb.AppendLine($".__novus_{binding.ModuleName}_request_deleted:");
                    sb.AppendLine($"\tmove.l\t__novus_{binding.ModuleName}_port,d0");
                    sb.AppendLine($"\tbeq\t.__novus_{binding.ModuleName}_closed");
                    sb.AppendLine("\tmovea.l\td0,a0");
                    sb.AppendLine("\tjsr\t-672(a6)\t; DeleteMsgPort");
                    sb.AppendLine($"\tclr.l\t__novus_{binding.ModuleName}_port");
                    sb.AppendLine($".__novus_{binding.ModuleName}_closed:");
                    continue;
                }
                sb.AppendLine($"\tmove.l\t{binding.BaseSymbol},d0");
                sb.AppendLine($"\tbeq\t.__novus_{binding.ModuleName}_closed");
                sb.AppendLine("\tmove.l\td0,a1");
                sb.AppendLine("\tjsr\t-414(a6)\t; CloseLibrary");
                sb.AppendLine($"\tclr.l\t{binding.BaseSymbol}");
                sb.AppendLine($".__novus_{binding.ModuleName}_closed:");
            }
            sb.AppendLine("\tmovem.l\t(sp)+,d0/a1/a6");
            sb.AppendLine("\trts");
        }
        sb.AppendLine("___novus_ffi_cleanup_lazy:");
        var lazyBindings = bindings.Where(binding => binding.Kind == FfiModuleKind.LazyLibrary).ToList();
        if (lazyBindings.Count > 0)
        {
            sb.AppendLine("\tmovem.l\td0/a1/a6,-(sp)");
            sb.AppendLine("\tmove.l\t4.w,a6");
            foreach (var binding in lazyBindings.AsEnumerable().Reverse())
            {
                sb.AppendLine($"\tmove.l\t{binding.BaseSymbol},d0");
                sb.AppendLine($"\tbeq\t.__novus_{binding.ModuleName}_lazy_closed");
                sb.AppendLine("\tmove.l\td0,a1");
                sb.AppendLine("\tjsr\t-414(a6)\t; CloseLibrary");
                sb.AppendLine($"\tclr.l\t{binding.BaseSymbol}");
                sb.AppendLine($".__novus_{binding.ModuleName}_lazy_closed:");
            }
            sb.AppendLine("\tmovem.l\t(sp)+,d0/a1/a6");
        }
        sb.AppendLine("\trts");
        if (includeWorkbenchStartup)
        {
            sb.AppendLine("\tsection\t___get_wb_startup_msg,code");
            sb.AppendLine("\txdef\t___get_wb_startup_msg");
            sb.AppendLine("___get_wb_startup_msg:");
            sb.AppendLine("\tmove.l\t_WBStartupMsg,d0");
            sb.AppendLine("\trts");
        }
        sb.AppendLine("\tend");
        return sb.ToString();
    }
}
