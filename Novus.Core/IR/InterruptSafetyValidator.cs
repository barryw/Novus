using System.Text;
using Novus.SemanticAnalysis;

namespace Novus.IR;

/// <summary>
/// Validates interrupt safety constraints at compile time.
///
/// On the Amiga (and similar systems), interrupt handlers run in a restricted context:
/// - No access to system calls that may block or allocate
/// - No dynamic memory allocation (AllocMem/FreeMem can't be called)
/// - No DOS functions (they use message passing which requires Wait())
/// - Must be reentrant (no global state mutation without protection)
///
/// This validator ensures functions marked @interrupt or @interrupt_safe follow these rules.
///
/// USAGE:
/// - @interrupt: Mark a function that will be called as an interrupt handler (VBL, CIAA, etc.)
/// - @interrupt_safe: Mark a function as safe to call from interrupt context
///
/// VALIDATION RULES:
/// 1. @interrupt functions can only call:
///    - Other @interrupt_safe functions
///    - Pure functions (no calls, only computation)
///    - Specific known-safe functions (inline assembly, etc.)
///
/// 2. @interrupt_safe functions follow the same rules
///
/// 3. Certain function names are known-unsafe:
///    - AllocMem, FreeMem (memory allocation)
///    - Wait (task synchronization)
///    - OpenLibrary, CloseLibrary (library management)
///    - Printf, Write, Read (DOS I/O)
/// </summary>
public class InterruptSafetyValidator
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    private readonly Dictionary<string, IrFunction> _functions = new();
    private readonly HashSet<string> _interruptHandlers = new();
    private readonly HashSet<string> _interruptSafeFunctions = new();
    private readonly HashSet<string> _analyzedFunctions = new();
    private IrFunction? _currentFunction;

    /// <summary>
    /// Known unsafe function names - calling these from interrupt context is always an error.
    /// These are AmigaOS exec.library, dos.library, and similar system calls.
    /// </summary>
    private static readonly HashSet<string> KnownUnsafeFunctions = new()
    {
        // exec.library - memory management
        "AllocMem", "FreeMem", "AllocVec", "FreeVec",
        "AllocAbs", "AllocPooled", "FreePooled", "CreatePool", "DeletePool",

        // exec.library - task/process management
        "Wait", "SetSignal", "AllocSignal", "FreeSignal",
        "CreateTask", "DeleteTask", "RemTask",
        "Forbid", "Permit", "SetIntVector", "AddIntServer", "RemIntServer",
        "CreateMsgPort", "DeleteMsgPort", "CreateIORequest", "DeleteIORequest",
        "WaitPort",

        // exec.library - library management
        "OpenLibrary", "CloseLibrary", "OpenDevice", "CloseDevice",
        "AddLibrary", "RemLibrary", "SumLibrary",

        // exec.library - resource management (some are safe, but avoid in interrupts)
        "OpenResource", "AddResource", "RemResource",

        // dos.library - all DOS functions use message passing
        "Open", "Close", "Read", "Write", "Seek",
        "Printf", "FPrintf", "VPrintf", "PutStr", "FPutC",
        "Lock", "UnLock", "CurrentDir", "CreateDir", "DeleteFile",
        "Examine", "ExNext", "ExAll", "Info",
        "Execute", "System", "RunCommand",
        "Input", "Output", "SelectInput", "SelectOutput",
        "Delay", "DateStamp",

        // intuition.library - most are unsafe
        "OpenWindow", "CloseWindow", "OpenScreen", "CloseScreen",
        "RefreshGadgets", "RefreshWindowFrame",
        "DisplayAlert", "AutoRequest", "EasyRequest",
        "ActivateWindow", "WindowToFront", "WindowToBack",

        // graphics.library - some are safe, but these use blitter which needs Forbid/Permit
        "LoadView", "WaitBlit", "WaitTOF",

        // misc functions that might block
        "DoIO", "SendIO", "CheckIO", "WaitIO", "AbortIO",
    };

    /// <summary>
    /// Known safe function names - these are always safe to call from interrupt context.
    /// </summary>
    private static readonly HashSet<string> KnownSafeFunctions = new()
    {
        // exec.library - interrupt-safe functions
        "Disable", "Enable",
        "Cause",              // Trigger software interrupt (safe to call)
        "Signal", "GetMsg", "PutMsg", "ReplyMsg", "FindPort", "FindTask",

        // graphics.library - direct hardware access (safe from VBL)
        "LoadRGB4", "LoadRGB32",
        "ScrollVPort",
        "MrgCop", "LoadView",  // Safe from VBL interrupt specifically

        // Custom hardware access helpers (our runtime)
        "custom_write", "custom_read",
        "copper_start", "copper_stop",

        // Low-level memory operations (no allocation)
        "CopyMem", "CopyMemQuick",

        // Novus runtime functions that are designed to be interrupt-safe
        "__novus_panic",  // Just halts - safe
    };

    /// <summary>
    /// Validate interrupt safety for an IR module.
    /// </summary>
    public InterruptSafetyResult Validate(IrModule module)
    {
        _errors.Clear();
        _warnings.Clear();
        _functions.Clear();
        _interruptHandlers.Clear();
        _interruptSafeFunctions.Clear();
        _analyzedFunctions.Clear();

        // First pass: collect all functions and identify interrupt handlers
        foreach (var function in module.Functions)
        {
            _functions[function.Name] = function;

            if (function.IsInterruptHandler)
            {
                _interruptHandlers.Add(function.Name);
            }

            if (function.IsInterruptSafe)
            {
                _interruptSafeFunctions.Add(function.Name);
            }
        }

        // Second pass: validate each interrupt handler and interrupt-safe function
        foreach (var handlerName in _interruptHandlers)
        {
            if (_functions.TryGetValue(handlerName, out var handler))
            {
                ValidateInterruptFunction(handler, isHandler: true);
            }
        }

        foreach (var safeFuncName in _interruptSafeFunctions)
        {
            if (!_interruptHandlers.Contains(safeFuncName) &&
                _functions.TryGetValue(safeFuncName, out var safeFunc))
            {
                ValidateInterruptFunction(safeFunc, isHandler: false);
            }
        }

        return new InterruptSafetyResult(_errors, _warnings);
    }

    /// <summary>
    /// Validate a function that must be interrupt-safe.
    /// </summary>
    private void ValidateInterruptFunction(IrFunction function, bool isHandler)
    {
        if (_analyzedFunctions.Contains(function.Name))
            return; // Already analyzed (prevents infinite recursion)

        _analyzedFunctions.Add(function.Name);
        _currentFunction = function;

        var context = isHandler ? "interrupt handler" : "interrupt-safe function";

        // Check all basic blocks for unsafe operations
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                ValidateInstruction(instruction, context);
            }
        }

        // Also check deferred blocks
        foreach (var block in function.DeferredBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                ValidateInstruction(instruction, context);
            }
        }
    }

    /// <summary>
    /// Validate a single instruction for interrupt safety.
    /// </summary>
    private void ValidateInstruction(IrInstruction instruction, string context)
    {
        switch (instruction)
        {
            case IrCall call:
                ValidateCall(call, context);
                break;

            case IrIndirectCall indirectCall:
                // Indirect calls through function pointers are risky
                AddWarning($"Indirect function call in {context} '{_currentFunction?.Name}' - " +
                           "cannot verify interrupt safety at compile time");
                break;

            // Other instructions are generally safe (pure computation, control flow, etc.)
        }
    }

    /// <summary>
    /// Validate a function call for interrupt safety.
    /// </summary>
    private void ValidateCall(IrCall call, string context)
    {
        var calledName = call.FunctionName;

        // Strip any module prefix (e.g., "exec::AllocMem" -> "AllocMem")
        var simpleName = calledName.Contains("::") ? calledName.Split("::").Last() : calledName;

        // Check against known unsafe functions
        if (KnownUnsafeFunctions.Contains(simpleName))
        {
            AddError($"Call to unsafe function '{simpleName}' from {context} '{_currentFunction?.Name}'. " +
                     $"This function cannot be safely called from interrupt context.");
            return;
        }

        // Known safe functions are always OK
        if (KnownSafeFunctions.Contains(simpleName))
        {
            return;
        }

        // Check if calling another function in this module
        if (_functions.TryGetValue(calledName, out var calledFunc))
        {
            // Recursive call to same function? Check if it's marked safe
            if (calledName == _currentFunction?.Name)
            {
                // Recursion in interrupt handler is dangerous
                AddWarning($"Recursive call in {context} '{_currentFunction.Name}' - " +
                           "ensure sufficient stack space and avoid unbounded recursion");
                return;
            }

            // Is the called function interrupt-safe?
            if (calledFunc.IsInterruptSafe || calledFunc.IsInterruptHandler)
            {
                // Safe - but we should validate it too
                ValidateInterruptFunction(calledFunc, isHandler: false);
                return;
            }

            // Analyze the called function to see if it's implicitly safe
            var isImplicitlySafe = IsImplicitlyInterruptSafe(calledFunc);
            if (!isImplicitlySafe)
            {
                AddError($"Call to non-interrupt-safe function '{calledName}' from {context} '{_currentFunction?.Name}'. " +
                         $"Add @interrupt_safe attribute to '{calledName}' if it is safe to call from interrupts.");
            }
            return;
        }

        // External function (not in this module)
        // If it starts with __ it's likely a runtime/compiler intrinsic
        if (calledName.StartsWith("__"))
        {
            // Most runtime intrinsics are safe (arithmetic, etc.)
            // But some aren't - add warnings for potentially unsafe ones
            if (calledName.Contains("alloc") || calledName.Contains("free"))
            {
                AddError($"Call to potentially unsafe runtime function '{calledName}' from {context} '{_currentFunction?.Name}'.");
            }
            return;
        }

        // Unknown external function - warn but don't error
        AddWarning($"Call to external function '{calledName}' from {context} '{_currentFunction?.Name}' - " +
                   "cannot verify interrupt safety. Ensure this function is safe to call from interrupts.");
    }

    /// <summary>
    /// Check if a function is implicitly interrupt-safe (pure computation, no calls to unsafe functions).
    /// </summary>
    private bool IsImplicitlyInterruptSafe(IrFunction function)
    {
        // Already checked and found unsafe
        if (_analyzedFunctions.Contains(function.Name) && !_interruptSafeFunctions.Contains(function.Name))
        {
            return false;
        }

        // Check all instructions
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCall call)
                {
                    var simpleName = call.FunctionName.Contains("::")
                        ? call.FunctionName.Split("::").Last()
                        : call.FunctionName;

                    if (KnownUnsafeFunctions.Contains(simpleName))
                    {
                        return false;
                    }

                    // Recursive check
                    if (_functions.TryGetValue(call.FunctionName, out var calledFunc))
                    {
                        if (calledFunc.IsInterruptSafe)
                            continue;

                        // Avoid infinite recursion
                        if (!_analyzedFunctions.Contains(calledFunc.Name))
                        {
                            _analyzedFunctions.Add(calledFunc.Name);
                            if (!IsImplicitlyInterruptSafe(calledFunc))
                                return false;
                        }
                    }
                }
                else if (instruction is IrIndirectCall)
                {
                    return false; // Can't verify indirect calls
                }
            }
        }

        return true;
    }

    private void AddError(string message)
    {
        _errors.Add(message);
    }

    private void AddWarning(string message)
    {
        _warnings.Add(message);
    }
}

/// <summary>
/// Result of interrupt safety validation.
/// </summary>
public class InterruptSafetyResult
{
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;
    public List<string> Errors { get; }
    public List<string> Warnings { get; }

    public InterruptSafetyResult(List<string> errors, List<string> warnings)
    {
        Errors = new List<string>(errors);
        Warnings = new List<string>(warnings);
    }

    public override string ToString()
    {
        if (IsValid && !HasWarnings)
            return "Interrupt safety validation passed";

        var sb = new StringBuilder();

        if (!IsValid)
        {
            sb.AppendLine($"Interrupt safety validation failed with {Errors.Count} error(s):");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  ERROR: {error}");
            }
        }

        if (HasWarnings)
        {
            sb.AppendLine($"Interrupt safety validation has {Warnings.Count} warning(s):");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  WARNING: {warning}");
            }
        }

        return sb.ToString();
    }
}
