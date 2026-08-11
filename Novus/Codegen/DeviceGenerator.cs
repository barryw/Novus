using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Codegen;

/// <summary>
/// Generates AmigaOS device driver boilerplate from @device attributes.
///
/// When a struct has @device attribute, this generates:
/// - ROMTag structure (for exec.library scanning)
/// - Device node structure
/// - BeginIO/AbortIO command dispatcher
/// - Unit management code
/// - A6 calling convention wrappers
/// - Default lifecycle functions (Open/Close/Expunge)
///
/// Device drivers differ from libraries in key ways:
/// - They use IORequest-based command dispatch instead of vector tables
/// - They handle units (multiple instances of the same device)
/// - They have BeginIO/AbortIO as the main entry points
/// - Commands are dispatched via io_Command field
/// </summary>
public class DeviceGenerator
{
    private readonly IrModule _module;
    private readonly IrStructType? _deviceStruct;
    private readonly AttributeInfo? _deviceAttribute;
    private readonly List<DeviceCommand> _deviceCommands;
    private readonly string? _projectVersion;
    private readonly int _versionMajor;
    private readonly int _versionMinor;
    private readonly int _versionPatch;
    private IrFunction? _initHook;
    private IrFunction? _openHook;
    private IrFunction? _closeHook;
    private IrFunction? _expungeHook;
    private IrFunction? _abortHook;

    /// <summary>
    /// Represents a device command handler.
    /// </summary>
    private class DeviceCommand
    {
        public string Name { get; set; } = "";
        public string CName { get; set; } = "";
        public IrFunction Function { get; set; } = null!;
        public int CommandNumber { get; set; }
        public bool IsQuickIO { get; set; }  // Can complete immediately without waiting
        public bool IsDeferred { get; set; } // Handler owns the request and replies later
    }

    // Standard Exec device commands (CMD_*)
    private static readonly Dictionary<string, int> StandardCommands = new()
    {
        { "CMD_INVALID", 0 },
        { "CMD_RESET", 1 },
        { "CMD_READ", 2 },
        { "CMD_WRITE", 3 },
        { "CMD_UPDATE", 4 },
        { "CMD_CLEAR", 5 },
        { "CMD_STOP", 6 },
        { "CMD_START", 7 },
        { "CMD_FLUSH", 8 },
        { "CMD_NONSTD", 9 },  // First non-standard command
    };

    public DeviceGenerator(IrModule module, string? projectVersion = null)
    {
        _module = module;
        _deviceCommands = new List<DeviceCommand>();
        _projectVersion = projectVersion;

        // Parse semver version
        if (!string.IsNullOrEmpty(projectVersion))
        {
            var parts = projectVersion.Split('.');
            _versionMajor = parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 1;
            _versionMinor = parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0;
            _versionPatch = parts.Length > 2 && int.TryParse(parts[2], out var patch) ? patch : 0;
        }
        else
        {
            _versionMajor = 1;
            _versionMinor = 0;
            _versionPatch = 0;
        }

        // Find the struct with @device attribute
        foreach (var structType in module.Structs)
        {
            if (structType.Attributes?.Has(KnownAttributes.Device) == true)
            {
                _deviceStruct = structType;
                _deviceAttribute = structType.Attributes.Get(KnownAttributes.Device);
                break;
            }
        }

        if (_deviceStruct != null)
        {
            AnalyzeDeviceCommands();
        }
    }

    /// <summary>
    /// Returns true if this module is a device (has @device attribute).
    /// </summary>
    public bool IsDevice => _deviceStruct != null;

    /// <summary>
    /// Get the device name from the @device attribute.
    /// </summary>
    public string GetDeviceName()
    {
        if (_deviceAttribute == null)
            return "";

        var name = _deviceAttribute.GetString("name");
        if (name == null)
            throw new InvalidOperationException("@device attribute requires 'name' parameter");

        if (!name.EndsWith(".device"))
            throw new InvalidOperationException("Device name must end with '.device'");

        return name;
    }

    /// <summary>
    /// Get the device version.
    /// </summary>
    public int GetDeviceVersion() => _versionMajor;

    /// <summary>
    /// Get the device revision.
    /// </summary>
    public int GetDeviceRevision() => _versionMinor;

    /// <summary>
    /// Get the device patch version.
    /// </summary>
    public int GetDevicePatch() => _versionPatch;

    /// <summary>
    /// Get the number of units this device supports.
    /// </summary>
    public int GetMaxUnits()
    {
        return _deviceAttribute?.GetInt("units") ?? 1;
    }

    /// <summary>
    /// Mangle a Novus name to a C-compatible name.
    /// </summary>
    private string MangleName(string name)
    {
        return name.Replace("::", "_");
    }

    /// <summary>
    /// Analyze which functions are device command handlers.
    /// </summary>
    private void AnalyzeDeviceCommands()
    {
        if (_deviceStruct == null)
            return;

        int nextCustomCommand = 9;  // CMD_NONSTD starts at 9

        foreach (var function in _module.Functions)
        {
            if (function.IsExtern || !function.IsPublic)
                continue;

            if (function.Attributes?.Has(KnownAttributes.DeviceInit) == true) { _initHook = function; continue; }
            if (function.Attributes?.Has(KnownAttributes.DeviceOpen) == true) { _openHook = function; continue; }
            if (function.Attributes?.Has(KnownAttributes.DeviceClose) == true) { _closeHook = function; continue; }
            if (function.Attributes?.Has(KnownAttributes.DeviceExpunge) == true) { _expungeHook = function; continue; }
            if (function.Attributes?.Has(KnownAttributes.AbortIO) == true) { _abortHook = function; continue; }

            // Check for @devicecmd attribute
            var deviceCmdAttr = function.Attributes?.Get(KnownAttributes.DeviceCmd);
            if (deviceCmdAttr != null)
            {
                var cmd = new DeviceCommand
                {
                    Name = function.Name,
                    CName = MangleName(function.Name),
                    Function = function,
                    IsQuickIO = deviceCmdAttr.HasFlag("quick") || deviceCmdAttr.GetBool("quick") == true,
                    IsDeferred = deviceCmdAttr.GetBool("deferred") == true
                };

                if (cmd.IsQuickIO && cmd.IsDeferred)
                    throw new InvalidOperationException($"Device command '{function.Name}' cannot be both quick and deferred");

                // Check if it's a standard command
                var cmdName = deviceCmdAttr.GetString("cmd");
                if (cmdName != null && StandardCommands.TryGetValue(cmdName, out var stdCmdNum))
                {
                    cmd.CommandNumber = stdCmdNum;
                }
                else
                {
                    // Custom command
                    var cmdNum = deviceCmdAttr.GetInt("cmd");
                    cmd.CommandNumber = cmdNum ?? nextCustomCommand++;
                }

                _deviceCommands.Add(cmd);
            }
        }

        // Sort by command number
        _deviceCommands.Sort((a, b) => a.CommandNumber.CompareTo(b.CommandNumber));
    }

    /// <summary>
    /// Generate the device base structure definition.
    /// </summary>
    public string GenerateDeviceBaseStruct()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var deviceName = GetDeviceName();
        var structName = $"{_deviceStruct.StructName}Base";
        var maxUnits = GetMaxUnits();

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Device Base Structure");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Include headers
        sb.AppendLine("#include \"novus_types.h\"");
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine("#include <exec/nodes.h>");
        sb.AppendLine("#include <exec/devices.h>");
        sb.AppendLine("#include <exec/io.h>");
        sb.AppendLine("#include <exec/errors.h>");
        sb.AppendLine("#include <dos/dos.h>");
        sb.AppendLine();

        // Include proto files for Exec functions
        sb.AppendLine("#include <proto/exec.h>");
        sb.AppendLine();

        // Unit structure for per-unit state
        sb.AppendLine($"// Per-unit state structure");
        sb.AppendLine($"struct {_deviceStruct.StructName}Unit {{");
        sb.AppendLine("    struct Unit unit;           // Standard Exec unit header");
        sb.AppendLine("    ULONG unit_OpenCnt;         // Open count for this unit");
        sb.AppendLine("    ULONG unit_Flags;           // Unit-specific flags");
        sb.AppendLine("    // Add custom per-unit fields here");
        sb.AppendLine("};");
        sb.AppendLine();

        // Device base structure
        sb.AppendLine($"// The device base includes the standard Device header plus custom fields.");
        sb.AppendLine($"struct {structName} {{");
        sb.AppendLine("    struct Device dev;              // Standard Exec device header");
        sb.AppendLine("    BPTR dev_SegList;               // Segment list for device unloading");
        sb.AppendLine("    UWORD dev_Patch;                // Patch version for semver");
        sb.AppendLine($"    struct {_deviceStruct.StructName}Unit* dev_Units[{maxUnits}];  // Unit pointers");
        sb.AppendLine("    ULONG dev_TotalCommands;        // Statistics: total commands processed");
        sb.AppendLine("    ULONG dev_TotalOpens;           // Statistics: lifetime Opens");
        sb.AppendLine("    ULONG dev_TotalCloses;          // Statistics: lifetime Closes");

        sb.AppendLine($"    {_deviceStruct.StructName} state;          // Novus-defined device state");

        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generate the ROMTag structure for the device.
    /// </summary>
    public string GenerateROMTag()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var deviceName = GetDeviceName();
        var version = GetDeviceVersion();
        var revision = GetDeviceRevision();
        var structName = $"{_deviceStruct.StructName}Base";

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Device Entry Point");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();
        sb.AppendLine("LONG _start(void) {");
        sb.AppendLine("    return -1;  // Cannot run from CLI");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// ROMTag (Resident Structure)");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();
        sb.AppendLine("#include <exec/resident.h>");
        sb.AppendLine();

        // Device name and ID string
        sb.AppendLine($"static const char DevName[] = \"{deviceName}\";");
        sb.AppendLine($"static const char DevIdString[] = \"{deviceName} {version}.{revision}\";");
        sb.AppendLine();

        // Forward declarations
        sb.AppendLine("// Forward declarations");
        sb.AppendLine("struct InitData {");
        sb.AppendLine("    ULONG DevSize;");
        sb.AppendLine("    APTR  FuncTable;");
        sb.AppendLine("    APTR  DataTable;");
        sb.AppendLine("    APTR  InitFunc;");
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine($"struct Device* DevInit(__reg(\"d0\") struct {structName}* base, __reg(\"a0\") BPTR segList, __reg(\"a6\") struct ExecBase* sysBase);");
        sb.AppendLine($"LONG DevOpen(__reg(\"a1\") struct IORequest* ioReq, __reg(\"d0\") ULONG unitNum, __reg(\"d1\") ULONG flags, __reg(\"a6\") struct {structName}* base);");
        sb.AppendLine($"BPTR DevClose(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base);");
        sb.AppendLine($"BPTR DevExpunge(__reg(\"a6\") struct {structName}* base);");
        sb.AppendLine("LONG DevReserved(void);");
        sb.AppendLine($"void DevBeginIO(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base);");
        sb.AppendLine($"LONG DevAbortIO(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base);");
        sb.AppendLine();

        // Forward declarations for A6 wrappers
        sb.AppendLine("// A6 wrapper function declarations");
        sb.AppendLine("extern void DevOpen_Wrapper(void);");
        sb.AppendLine("extern void DevClose_Wrapper(void);");
        sb.AppendLine("extern void DevExpunge_Wrapper(void);");
        sb.AppendLine("extern void DevReserved_Wrapper(void);");
        sb.AppendLine("extern void DevBeginIO_Wrapper(void);");
        sb.AppendLine("extern void DevAbortIO_Wrapper(void);");
        sb.AppendLine();

        // Function table - devices have fixed vector layout
        sb.AppendLine("// Device function vector table");
        sb.AppendLine("// Note: Devices have fixed layout: Open, Close, Expunge, Reserved, BeginIO, AbortIO");
        sb.AppendLine("static APTR FuncTable[] = {");
        sb.AppendLine("    (APTR)DevOpen_Wrapper,      // -6: Open");
        sb.AppendLine("    (APTR)DevClose_Wrapper,     // -12: Close");
        sb.AppendLine("    (APTR)DevExpunge_Wrapper,   // -18: Expunge");
        sb.AppendLine("    (APTR)DevReserved_Wrapper,  // -24: Reserved");
        sb.AppendLine("    (APTR)DevBeginIO_Wrapper,   // -30: BeginIO");
        sb.AppendLine("    (APTR)DevAbortIO_Wrapper,   // -36: AbortIO");
        sb.AppendLine("    (APTR)0xFFFFFFFFUL");
        sb.AppendLine("};");
        sb.AppendLine();

        // AutoInit structure
        sb.AppendLine("static struct InitData InitTable = {");
        sb.AppendLine($"    sizeof(struct {structName}),");
        sb.AppendLine("    (APTR)FuncTable,");
        sb.AppendLine("    NULL,");
        sb.AppendLine("    (APTR)DevInit");
        sb.AppendLine("};");
        sb.AppendLine();

        // ROMTag structure
        sb.AppendLine("__entry struct Resident RomTag = {");
        sb.AppendLine("    RTC_MATCHWORD,");
        sb.AppendLine("    &RomTag,");
        sb.AppendLine("    (APTR)((UBYTE*)&RomTag + sizeof(struct Resident)),");
        sb.AppendLine("    RTF_AUTOINIT,");
        sb.AppendLine($"    {version},");
        sb.AppendLine("    NT_DEVICE,          // Type = Device");
        sb.AppendLine("    0,");
        sb.AppendLine("    (char*)DevName,");
        sb.AppendLine("    (char*)DevIdString,");
        sb.AppendLine("    (APTR)&InitTable");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generate the default lifecycle and command dispatch functions.
    /// </summary>
    public string GenerateLifecycleFunctions()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var structName = $"{_deviceStruct.StructName}Base";
        var unitStructName = $"{_deviceStruct.StructName}Unit";
        var maxUnits = GetMaxUnits();

        // First include the device base struct
        sb.AppendLine(GenerateDeviceBaseStruct());

        // Device name/ID (needed by lifecycle)
        var deviceName = GetDeviceName();
        var version = GetDeviceVersion();
        var revision = GetDeviceRevision();
        sb.AppendLine($"static const char DevName[] = \"{deviceName}\";");
        sb.AppendLine($"static const char DevIdString[] = \"{deviceName} {version}.{revision}\";");
        sb.AppendLine();

        sb.AppendLine("extern struct ExecBase* SysBase;");
        sb.AppendLine("extern int __novus_ffi_init(void);");
        sb.AppendLine("extern void __novus_ffi_cleanup(void);");
        sb.AppendLine($"__entry BPTR DevExpunge(__reg(\"a6\") struct {structName}* base);");
        sb.AppendLine();

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Device Lifecycle Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // DevInit
        sb.AppendLine($"__entry struct Device* DevInit(__reg(\"d0\") struct {structName}* base, __reg(\"a0\") BPTR segList, __reg(\"a6\") struct ExecBase* sysBase) {{");
        sb.AppendLine("    SysBase = sysBase;");
        sb.AppendLine("    if (!__novus_ffi_init()) return NULL;");
        sb.AppendLine("    // Initialize device node fields");
        sb.AppendLine("    base->dev.dd_Library.lib_Node.ln_Type = NT_DEVICE;");
        sb.AppendLine("    base->dev.dd_Library.lib_Node.ln_Pri = 0;");
        sb.AppendLine("    base->dev.dd_Library.lib_Node.ln_Name = (char*)DevName;");
        sb.AppendLine("    base->dev.dd_Library.lib_Flags = LIBF_CHANGED;");
        sb.AppendLine("    base->dev.dd_Library.lib_OpenCnt = 0;");
        sb.AppendLine($"    base->dev.dd_Library.lib_Version = {GetDeviceVersion()};");
        sb.AppendLine($"    base->dev.dd_Library.lib_Revision = {GetDeviceRevision()};");
        sb.AppendLine("    base->dev.dd_Library.lib_IdString = (char*)DevIdString;");
        sb.AppendLine();
        sb.AppendLine("    // Store segment list and version");
        sb.AppendLine("    base->dev_SegList = segList;");
        sb.AppendLine($"    base->dev_Patch = {GetDevicePatch()};");
        sb.AppendLine();
        sb.AppendLine("    // Initialize statistics");
        sb.AppendLine("    base->dev_TotalCommands = 0;");
        sb.AppendLine("    base->dev_TotalOpens = 0;");
        sb.AppendLine("    base->dev_TotalCloses = 0;");
        sb.AppendLine();
        sb.AppendLine("    // Initialize unit pointers to NULL");
        sb.AppendLine($"    for (int i = 0; i < {maxUnits}; i++) {{");
        sb.AppendLine("        base->dev_Units[i] = NULL;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Initialize custom fields
        if (_deviceStruct.Fields.Count > 0)
        {
            sb.AppendLine("    // Initialize custom fields");
            foreach (var field in _deviceStruct.Fields)
            {
                sb.AppendLine($"    base->state.{field.Name} = 0;");
            }
            sb.AppendLine();
        }

        if (_initHook != null)
            sb.AppendLine($"    if (!{MangleName(_initHook.Name)}(&base->state)) {{ __novus_ffi_cleanup(); return NULL; }}");
        sb.AppendLine("    return &base->dev;");
        sb.AppendLine("}");
        sb.AppendLine();

        // DevOpen - handles per-unit opening
        sb.AppendLine($"__entry LONG DevOpen(__reg(\"a1\") struct IORequest* ioReq, __reg(\"d0\") ULONG unitNum, __reg(\"d1\") ULONG flags, __reg(\"a6\") struct {structName}* base) {{");
        sb.AppendLine($"    if (unitNum >= {maxUnits}) {{");
        sb.AppendLine("        ioReq->io_Error = IOERR_OPENFAIL;");
        sb.AppendLine("        ioReq->io_Device = NULL;");
        sb.AppendLine("        ioReq->io_Unit = NULL;");
        sb.AppendLine("        return IOERR_OPENFAIL;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Get or create unit structure");
        sb.AppendLine($"    struct {unitStructName}* unit = base->dev_Units[unitNum];");
        sb.AppendLine("    if (unit == NULL) {");
        sb.AppendLine("        // Allocate new unit");
        sb.AppendLine($"        unit = (struct {unitStructName}*)AllocMem(sizeof(struct {unitStructName}), MEMF_PUBLIC | MEMF_CLEAR);");
        sb.AppendLine("        if (unit == NULL) {");
        sb.AppendLine("            ioReq->io_Error = IOERR_OPENFAIL;");
        sb.AppendLine("            ioReq->io_Device = NULL;");
        sb.AppendLine("            ioReq->io_Unit = NULL;");
        sb.AppendLine("            return IOERR_OPENFAIL;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Initialize unit");
        sb.AppendLine("        unit->unit.unit_MsgPort.mp_Node.ln_Type = NT_MSGPORT;");
        sb.AppendLine("        unit->unit.unit_MsgPort.mp_Flags = PA_IGNORE;");
        sb.AppendLine("        NewList(&unit->unit.unit_MsgPort.mp_MsgList);");
        sb.AppendLine("        unit->unit_OpenCnt = 0;");
        sb.AppendLine("        unit->unit_Flags = 0;");
        sb.AppendLine();
        sb.AppendLine("        base->dev_Units[unitNum] = unit;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Increment open counts");
        sb.AppendLine("    unit->unit_OpenCnt++;");
        sb.AppendLine("    base->dev.dd_Library.lib_OpenCnt++;");
        sb.AppendLine("    base->dev.dd_Library.lib_Flags &= ~LIBF_DELEXP;");
        sb.AppendLine("    base->dev_TotalOpens++;");
        sb.AppendLine();
        sb.AppendLine("    // Fill in IORequest");
        sb.AppendLine("    ioReq->io_Error = 0;");
        sb.AppendLine("    ioReq->io_Device = &base->dev;");
        sb.AppendLine("    ioReq->io_Unit = (struct Unit*)unit;");
        sb.AppendLine();
        if (_openHook != null)
        {
            sb.AppendLine($"    if (!{MangleName(_openHook.Name)}(ioReq, &base->state, unitNum, flags)) {{");
            sb.AppendLine("        unit->unit_OpenCnt--;");
            sb.AppendLine("        base->dev.dd_Library.lib_OpenCnt--;");
            sb.AppendLine("        ioReq->io_Error = IOERR_OPENFAIL;");
            sb.AppendLine("        ioReq->io_Device = NULL;");
            sb.AppendLine("        ioReq->io_Unit = NULL;");
            sb.AppendLine("        return IOERR_OPENFAIL;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();

        // DevClose
        sb.AppendLine($"__entry BPTR DevClose(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base) {{");
        sb.AppendLine($"    struct {unitStructName}* unit = (struct {unitStructName}*)ioReq->io_Unit;");
        sb.AppendLine();
        sb.AppendLine("    // Decrement unit open count");
        sb.AppendLine("    if (unit && unit->unit_OpenCnt > 0) {");
        sb.AppendLine("        unit->unit_OpenCnt--;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Update statistics");
        sb.AppendLine("    base->dev_TotalCloses++;");
        sb.AppendLine();
        if (_closeHook != null)
        {
            sb.AppendLine($"    {MangleName(_closeHook.Name)}(ioReq, &base->state);");
            sb.AppendLine();
        }
        sb.AppendLine("    // Clear IORequest device/unit pointers");
        sb.AppendLine("    ioReq->io_Device = NULL;");
        sb.AppendLine("    ioReq->io_Unit = NULL;");
        sb.AppendLine();
        sb.AppendLine("    // Decrement device open count");
        sb.AppendLine("    base->dev.dd_Library.lib_OpenCnt--;");
        sb.AppendLine();
        sb.AppendLine("    // Check for delayed expunge");
        sb.AppendLine("    if (base->dev.dd_Library.lib_OpenCnt == 0 && (base->dev.dd_Library.lib_Flags & LIBF_DELEXP)) {");
        sb.AppendLine("        return DevExpunge(base);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();

        // DevExpunge
        sb.AppendLine($"__entry BPTR DevExpunge(__reg(\"a6\") struct {structName}* base) {{");
        sb.AppendLine("    if (base->dev.dd_Library.lib_OpenCnt > 0) {");
        sb.AppendLine("        base->dev.dd_Library.lib_Flags |= LIBF_DELEXP;");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    BPTR segList = base->dev_SegList;");
        if (_expungeHook != null)
            sb.AppendLine($"    {MangleName(_expungeHook.Name)}(&base->state);");
        sb.AppendLine("    __novus_ffi_cleanup();");
        sb.AppendLine();
        sb.AppendLine("    // Free all unit structures");
        sb.AppendLine($"    for (int i = 0; i < {maxUnits}; i++) {{");
        sb.AppendLine("        if (base->dev_Units[i] != NULL) {");
        sb.AppendLine($"            FreeMem(base->dev_Units[i], sizeof(struct {unitStructName}));");
        sb.AppendLine("            base->dev_Units[i] = NULL;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Remove device from system list");
        sb.AppendLine("    Remove((struct Node*)&base->dev);");
        sb.AppendLine();
        sb.AppendLine("    // Free device base memory");
        sb.AppendLine("    ULONG memSize = base->dev.dd_Library.lib_NegSize + base->dev.dd_Library.lib_PosSize;");
        sb.AppendLine("    void* memStart = (void*)((UBYTE*)base - base->dev.dd_Library.lib_NegSize);");
        sb.AppendLine("    FreeMem(memStart, memSize);");
        sb.AppendLine();
        sb.AppendLine("    return segList;");
        sb.AppendLine("}");
        sb.AppendLine();

        // DevReserved
        sb.AppendLine("__entry LONG DevReserved(void) {");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate BeginIO with command dispatch
        GenerateBeginIO(sb, structName);

        // Generate AbortIO
        GenerateAbortIO(sb, structName);

        // Append ROMTag
        sb.AppendLine(GenerateROMTagWithoutNames());

        return sb.ToString();
    }

    /// <summary>
    /// Generate BeginIO command dispatcher.
    /// </summary>
    private void GenerateBeginIO(StringBuilder sb, string structName)
    {
        var stateName = _deviceStruct!.StructName;
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// BeginIO - Command Dispatcher");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Forward declarations for command handlers
        foreach (var cmd in _deviceCommands)
        {
            sb.AppendLine($"BYTE {cmd.CName}(struct IORequest* ioReq, {stateName}* state);");
        }
        sb.AppendLine();

        sb.AppendLine($"__entry void DevBeginIO(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base) {{");
        sb.AppendLine("    // Clear error and mark as not quick (unless handler says otherwise)");
        sb.AppendLine("    ioReq->io_Error = 0;");
        sb.AppendLine("    ioReq->io_Flags &= ~IOF_QUICK;");
        sb.AppendLine("    BOOL completed = TRUE;");
        sb.AppendLine();
        sb.AppendLine("    // Update statistics");
        sb.AppendLine("    base->dev_TotalCommands++;");
        sb.AppendLine();
        sb.AppendLine("    // Dispatch based on io_Command");
        sb.AppendLine("    switch (ioReq->io_Command) {");

        // Generate case for each command handler
        foreach (var cmd in _deviceCommands)
        {
            sb.AppendLine($"        case {cmd.CommandNumber}: // {cmd.Name}");
            if (cmd.IsQuickIO)
            {
                sb.AppendLine("            ioReq->io_Flags |= IOF_QUICK;  // Can complete immediately");
            }
            if (cmd.IsDeferred)
            {
                sb.AppendLine("            completed = FALSE;  // Handler owns request and must ReplyMsg later");
            }
            sb.AppendLine($"            ioReq->io_Error = {cmd.CName}(ioReq, &base->state);");
            sb.AppendLine("            break;");
        }

        // Default case for unknown commands
        sb.AppendLine("        default:");
        sb.AppendLine("            ioReq->io_Error = IOERR_NOCMD;");
        sb.AppendLine("            break;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // If not quick I/O, reply to the message");
        sb.AppendLine("    if (completed && !(ioReq->io_Flags & IOF_QUICK)) {");
        sb.AppendLine("        ReplyMsg(&ioReq->io_Message);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generate AbortIO handler.
    /// </summary>
    private void GenerateAbortIO(StringBuilder sb, string structName)
    {
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// AbortIO - Abort Pending I/O");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"__entry LONG DevAbortIO(__reg(\"a1\") struct IORequest* ioReq, __reg(\"a6\") struct {structName}* base) {{");
        if (_abortHook != null)
        {
            sb.AppendLine($"    if ({MangleName(_abortHook.Name)}(ioReq, &base->state)) {{");
            sb.AppendLine("        ioReq->io_Error = IOERR_ABORTED;");
            sb.AppendLine("        ReplyMsg(&ioReq->io_Message);");
            sb.AppendLine("        return 0;");
            sb.AppendLine("    }");
        }
        sb.AppendLine("    return IOERR_NOCMD;  // No pending request was claimed");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generate ROMTag without redefining DevName/DevIdString.
    /// </summary>
    private string GenerateROMTagWithoutNames()
    {
        if (!IsDevice)
            return "";

        var romTag = GenerateROMTag();
        var lines = romTag.Split('\n');
        var filtered = lines.Where(line =>
            !line.Contains("static const char DevName[]") &&
            !line.Contains("static const char DevIdString[]")
        );

        return string.Join("\n", filtered);
    }

    /// <summary>
    /// Generate A6 wrapper assembly for AmigaOS calling convention.
    /// </summary>
    public string GenerateA6Wrappers()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var structName = $"{_deviceStruct.StructName}Base";

        sb.AppendLine("; A6 Wrapper Functions for AmigaOS Device Calling Convention");
        sb.AppendLine("; Generated by Novus compiler");
        sb.AppendLine(";");
        sb.AppendLine("; Device functions receive:");
        sb.AppendLine(";   A6 = Device base pointer");
        sb.AppendLine(";   A1 = IORequest pointer");
        sb.AppendLine(";   D0 = Unit number (for Open)");
        sb.AppendLine(";   D1 = Flags (for Open)");
        sb.AppendLine();

        sb.AppendLine("        SECTION CODE");
        sb.AppendLine();

        // DevOpen wrapper
        sb.AppendLine("; DevOpen(ioReq A1, unitNum D0, flags D1, base A6)");
        sb.AppendLine("        XDEF    _DevOpen_Wrapper");
        sb.AppendLine("        XREF    _DevOpen");
        sb.AppendLine("_DevOpen_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push base");
        sb.AppendLine("        move.l  d1,-(sp)        ; Push flags");
        sb.AppendLine("        move.l  d0,-(sp)        ; Push unitNum");
        sb.AppendLine("        move.l  a1,-(sp)        ; Push ioReq");
        sb.AppendLine("        jsr     _DevOpen");
        sb.AppendLine("        add.l   #16,sp          ; Clean up 4 params");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // DevClose wrapper
        sb.AppendLine("; DevClose(ioReq A1, base A6)");
        sb.AppendLine("        XDEF    _DevClose_Wrapper");
        sb.AppendLine("        XREF    _DevClose");
        sb.AppendLine("_DevClose_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push base");
        sb.AppendLine("        move.l  a1,-(sp)        ; Push ioReq");
        sb.AppendLine("        jsr     _DevClose");
        sb.AppendLine("        addq.l  #8,sp           ; Clean up 2 params");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // DevExpunge wrapper
        sb.AppendLine("; DevExpunge(base A6)");
        sb.AppendLine("        XDEF    _DevExpunge_Wrapper");
        sb.AppendLine("        XREF    _DevExpunge");
        sb.AppendLine("_DevExpunge_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push base");
        sb.AppendLine("        jsr     _DevExpunge");
        sb.AppendLine("        addq.l  #4,sp           ; Clean up param");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // DevReserved wrapper
        sb.AppendLine("; DevReserved()");
        sb.AppendLine("        XDEF    _DevReserved_Wrapper");
        sb.AppendLine("        XREF    _DevReserved");
        sb.AppendLine("_DevReserved_Wrapper:");
        sb.AppendLine("        jsr     _DevReserved");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // DevBeginIO wrapper
        sb.AppendLine("; DevBeginIO(ioReq A1, base A6)");
        sb.AppendLine("        XDEF    _DevBeginIO_Wrapper");
        sb.AppendLine("        XREF    _DevBeginIO");
        sb.AppendLine("_DevBeginIO_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push base");
        sb.AppendLine("        move.l  a1,-(sp)        ; Push ioReq");
        sb.AppendLine("        jsr     _DevBeginIO");
        sb.AppendLine("        addq.l  #8,sp           ; Clean up 2 params");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // DevAbortIO wrapper
        sb.AppendLine("; DevAbortIO(ioReq A1, base A6)");
        sb.AppendLine("        XDEF    _DevAbortIO_Wrapper");
        sb.AppendLine("        XREF    _DevAbortIO");
        sb.AppendLine("_DevAbortIO_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push base");
        sb.AppendLine("        move.l  a1,-(sp)        ; Push ioReq");
        sb.AppendLine("        jsr     _DevAbortIO");
        sb.AppendLine("        addq.l  #8,sp           ; Clean up 2 params");
        sb.AppendLine("        rts");
        sb.AppendLine();

        sb.AppendLine("        END");

        return sb.ToString();
    }

    /// <summary>
    /// Generate C header file for the device.
    /// </summary>
    public string GenerateCHeader()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var deviceName = GetDeviceName();
        var structName = $"{_deviceStruct.StructName}Base";
        var unitStructName = $"{_deviceStruct.StructName}Unit";
        var headerGuard = $"_{deviceName.Replace(".", "_").ToUpper()}_H";
        var maxUnits = GetMaxUnits();

        sb.AppendLine($"#ifndef {headerGuard}");
        sb.AppendLine($"#define {headerGuard}");
        sb.AppendLine();
        sb.AppendLine("/*");
        sb.AppendLine($" * {deviceName} - C Interface");
        sb.AppendLine($" * Generated by Novus compiler");
        sb.AppendLine($" * Version: {GetDeviceVersion()}.{GetDeviceRevision()}");
        sb.AppendLine(" */");
        sb.AppendLine();
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine("#include <exec/devices.h>");
        sb.AppendLine("#include <exec/io.h>");
        sb.AppendLine();

        // Unit structure
        sb.AppendLine("/* Per-unit state structure */");
        sb.AppendLine($"struct {unitStructName} {{");
        sb.AppendLine("    struct Unit unit;");
        sb.AppendLine("    ULONG unit_OpenCnt;");
        sb.AppendLine("    ULONG unit_Flags;");
        sb.AppendLine("};");
        sb.AppendLine();

        // Device base structure
        sb.AppendLine("/* Device base structure */");
        sb.AppendLine($"struct {structName} {{");
        sb.AppendLine("    struct Device dev;");
        sb.AppendLine("    BPTR dev_SegList;");
        sb.AppendLine("    UWORD dev_Patch;");
        sb.AppendLine($"    struct {unitStructName}* dev_Units[{maxUnits}];");
        sb.AppendLine("    ULONG dev_TotalCommands;");
        sb.AppendLine("    ULONG dev_TotalOpens;");
        sb.AppendLine("    ULONG dev_TotalCloses;");

        foreach (var field in _deviceStruct.Fields)
        {
            var cType = GetCType(field.Type);
            sb.AppendLine($"    {cType} {field.Name};");
        }

        sb.AppendLine("};");
        sb.AppendLine();

        // Command constants
        if (_deviceCommands.Count > 0)
        {
            sb.AppendLine("/* Device-specific commands */");
            foreach (var cmd in _deviceCommands)
            {
                var constName = $"CMD_{_deviceStruct.StructName.ToUpper()}_{cmd.Name.ToUpper()}";
                sb.AppendLine($"#define {constName} {cmd.CommandNumber}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"#endif /* {headerGuard} */");

        return sb.ToString();
    }

    /// <summary>
    /// Generate Novus FFI binding for the device.
    /// </summary>
    public string GenerateNovusFFI()
    {
        if (!IsDevice || _deviceStruct == null)
            return "";

        var sb = new StringBuilder();
        var deviceName = GetDeviceName();
        var moduleName = _deviceStruct.StructName.ToLower();
        var maxUnits = GetMaxUnits();

        sb.AppendLine("//");
        sb.AppendLine($"// {deviceName} - Novus FFI Binding");
        sb.AppendLine("// Generated by Novus compiler");
        sb.AppendLine($"// Version: {GetDeviceVersion()}.{GetDeviceRevision()}");
        sb.AppendLine("//");
        sb.AppendLine();
        sb.AppendLine("from std::ffi::exec import OpenDevice, CloseDevice, DoIO, SendIO, AbortIO");
        sb.AppendLine("from std::ffi::exec import CreateMsgPort, DeleteMsgPort, CreateIORequest, DeleteIORequest");
        sb.AppendLine("from std::ffi::amiga_structs import IORequest, MsgPort, Unit, Device");
        sb.AppendLine();

        // Error type
        sb.AppendLine($"pub enum {_deviceStruct.StructName}Error {{");
        sb.AppendLine("    OpenFailed,");
        sb.AppendLine("    NoMemory,");
        sb.AppendLine("    InvalidUnit,");
        sb.AppendLine("    CommandFailed(i8),");
        sb.AppendLine("}");
        sb.AppendLine();

        // Command constants
        if (_deviceCommands.Count > 0)
        {
            sb.AppendLine("// Device-specific commands");
            foreach (var cmd in _deviceCommands)
            {
                var constName = $"CMD_{cmd.Name.ToUpper()}";
                sb.AppendLine($"pub const {constName}: u16 = {cmd.CommandNumber}");
            }
            sb.AppendLine();
        }

        // Device handle
        sb.AppendLine($"// RAII handle for {deviceName}");
        sb.AppendLine($"pub struct {_deviceStruct.StructName} {{");
        sb.AppendLine("    port: *MsgPort,");
        sb.AppendLine("    request: *IORequest,");
        sb.AppendLine("    unit: u32,");
        sb.AppendLine("}");
        sb.AppendLine();

        // Implementation
        sb.AppendLine($"impl {_deviceStruct.StructName} {{");
        sb.AppendLine($"    pub fn open(unit: u32) -> Result<{_deviceStruct.StructName}, {_deviceStruct.StructName}Error> {{");
        sb.AppendLine($"        if unit >= {maxUnits} {{");
        sb.AppendLine($"            return Result::Err({_deviceStruct.StructName}Error::InvalidUnit)");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        let port = CreateMsgPort()");
        sb.AppendLine("        if (u32)port == 0 {");
        sb.AppendLine($"            return Result::Err({_deviceStruct.StructName}Error::NoMemory)");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        let request = CreateIORequest(port, @sizeof(IORequest))");
        sb.AppendLine("        if (u32)request == 0 {");
        sb.AppendLine("            DeleteMsgPort(port)");
        sb.AppendLine($"            return Result::Err({_deviceStruct.StructName}Error::NoMemory)");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        let error = OpenDevice(\"{deviceName}\".as_cstr(), unit, request, 0)");
        sb.AppendLine("        if error != 0 {");
        sb.AppendLine("            DeleteIORequest(request)");
        sb.AppendLine("            DeleteMsgPort(port)");
        sb.AppendLine($"            return Result::Err({_deviceStruct.StructName}Error::OpenFailed)");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        return Result::Ok({_deviceStruct.StructName} {{ port: port, request: request, unit: unit }})");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Command helper
        sb.AppendLine($"    pub fn command(&self, cmd: u16) -> Result<(), {_deviceStruct.StructName}Error> {{");
        sb.AppendLine("        self.request.io_Command = cmd");
        sb.AppendLine("        let error = DoIO(self.request)");
        sb.AppendLine("        if error != 0 {");
        sb.AppendLine($"            return Result::Err({_deviceStruct.StructName}Error::CommandFailed(error))");
        sb.AppendLine("        }");
        sb.AppendLine("        return Result::Ok(())");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Drop implementation
        sb.AppendLine($"impl Drop for {_deviceStruct.StructName} {{");
        sb.AppendLine("    fn drop(&var self) {");
        sb.AppendLine("        if self.request != null {");
        sb.AppendLine("            CloseDevice(self.request)");
        sb.AppendLine("            DeleteIORequest(self.request)");
        sb.AppendLine("            self.request = null");
        sb.AppendLine("        }");
        sb.AppendLine("        if self.port != null {");
        sb.AppendLine("            DeleteMsgPort(self.port)");
        sb.AppendLine("            self.port = null");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Get the C type name for an IR type.
    /// </summary>
    private string GetCType(IrType type)
    {
        return type switch
        {
            IrIntType intType => intType.IsSigned
                ? $"int{intType.SizeInBytes * 8}_t"
                : $"uint{intType.SizeInBytes * 8}_t",
            IrBoolType => "BOOL",
            IrPointerType ptrType => $"{GetCType(ptrType.PointeeType)}*",
            IrVoidType => "void",
            _ => "LONG"
        };
    }
}
