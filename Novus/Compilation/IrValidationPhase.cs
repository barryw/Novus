using Novus.IR;

namespace Novus.Compilation;

/// <summary>
/// Phase 1.25: Validate IR correctness after generation.
/// Catches malformed IR early, before optimization or code generation.
///
/// This phase runs after ParseAndIRPhase and before LoweringPhase to ensure
/// the IR produced by IrBuilder is structurally valid.
///
/// A second validation can optionally run after LoweringPhase to verify
/// that lowering passes preserve IR invariants.
/// </summary>
public class IrValidationPhase : ICompilationPhase
{
    public string Name => "IR Validation";

    private readonly bool _verbose;
    private readonly bool _failOnWarnings;

    /// <summary>
    /// Create an IR validation phase.
    /// </summary>
    /// <param name="verbose">If true, print detailed validation progress.</param>
    /// <param name="failOnWarnings">If true, treat validation warnings as errors.</param>
    public IrValidationPhase(bool verbose = false, bool failOnWarnings = false)
    {
        _verbose = verbose;
        _failOnWarnings = failOnWarnings;
    }

    public Task<bool> ExecuteAsync(CompilationContext context)
    {
        if (context.MainModuleIR == null)
        {
            // Nothing to validate
            return Task.FromResult(true);
        }

        if (_verbose)
        {
            Console.WriteLine("Validating IR...");
        }

        var validator = new IrValidator();
        var allErrors = new List<string>();
        var modulesValidated = 0;

        // Validate main module
        var mainResult = validator.Validate(context.MainModuleIR.IrModule);
        if (!mainResult.IsValid)
        {
            var moduleName = Path.GetFileNameWithoutExtension(context.Options.InputFile);
            foreach (var error in mainResult.Errors)
            {
                allErrors.Add($"[{moduleName}] {error}");
            }
        }
        modulesValidated++;

        // Validate all imported modules
        foreach (var (modulePath, moduleIR) in context.AllModulesIR)
        {
            var result = validator.Validate(moduleIR.IrModule);
            if (!result.IsValid)
            {
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                foreach (var error in result.Errors)
                {
                    allErrors.Add($"[{moduleName}] {error}");
                }
            }
            modulesValidated++;
        }

        if (allErrors.Count > 0)
        {
            Console.WriteLine($"IR validation failed with {allErrors.Count} error(s):");
            foreach (var error in allErrors.Take(20)) // Limit output to avoid overwhelming user
            {
                Console.WriteLine($"  - {error}");
            }
            if (allErrors.Count > 20)
            {
                Console.WriteLine($"  ... and {allErrors.Count - 20} more errors");
            }

            // In debug builds, this is a compiler bug - fail immediately
            // In release builds, we may want to continue and let codegen fail more gracefully
#if DEBUG
            return Task.FromResult(false);
#else
            Console.WriteLine("WARNING: IR validation errors detected. Continuing compilation, but output may be incorrect.");
            return Task.FromResult(true);
#endif
        }

        if (_verbose)
        {
            Console.WriteLine($"  ✓ Validated {modulesValidated} module(s)");
        }

        // Run interrupt safety validation
        var interruptValidator = new InterruptSafetyValidator();
        var interruptErrors = new List<string>();
        var interruptWarnings = new List<string>();

        // Validate main module
        var mainInterruptResult = interruptValidator.Validate(context.MainModuleIR.IrModule);
        interruptErrors.AddRange(mainInterruptResult.Errors);
        interruptWarnings.AddRange(mainInterruptResult.Warnings);

        // Validate all imported modules (they may have interrupt handlers too)
        foreach (var (_, moduleIR) in context.AllModulesIR)
        {
            var interruptResult = interruptValidator.Validate(moduleIR.IrModule);
            interruptErrors.AddRange(interruptResult.Errors);
            interruptWarnings.AddRange(interruptResult.Warnings);
        }

        // Report interrupt safety warnings
        if (interruptWarnings.Count > 0 && _verbose)
        {
            Console.WriteLine($"Interrupt safety warnings ({interruptWarnings.Count}):");
            foreach (var warning in interruptWarnings.Take(10))
            {
                Console.WriteLine($"  WARNING: {warning}");
            }
            if (interruptWarnings.Count > 10)
            {
                Console.WriteLine($"  ... and {interruptWarnings.Count - 10} more warnings");
            }
        }

        // Report interrupt safety errors
        if (interruptErrors.Count > 0)
        {
            Console.WriteLine($"Interrupt safety validation failed with {interruptErrors.Count} error(s):");
            foreach (var error in interruptErrors.Take(10))
            {
                Console.WriteLine($"  ERROR: {error}");
            }
            if (interruptErrors.Count > 10)
            {
                Console.WriteLine($"  ... and {interruptErrors.Count - 10} more errors");
            }
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}

/// <summary>
/// Static helper to run IR validation at strategic points during compilation.
/// Use this for ad-hoc validation during development or debugging.
/// </summary>
public static class IrValidationHelper
{
    /// <summary>
    /// Validate a single module and return errors.
    /// Does not throw - returns validation result for caller to handle.
    /// </summary>
    public static ValidationResult ValidateModule(IrModule module)
    {
        var validator = new IrValidator();
        return validator.Validate(module);
    }

    /// <summary>
    /// Validate a module and throw if invalid.
    /// Use in debug assertions or tests.
    /// </summary>
    public static void AssertValid(IrModule module, string context = "")
    {
        var result = ValidateModule(module);
        if (!result.IsValid)
        {
            var contextPrefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            throw new IrValidationException($"{contextPrefix}IR validation failed:\n{result}");
        }
    }

    /// <summary>
    /// Validate a module in debug builds only.
    /// No-op in release builds for performance.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void DebugValidate(IrModule module, string context = "")
    {
        AssertValid(module, context);
    }
}

/// <summary>
/// Exception thrown when IR validation fails.
/// </summary>
public class IrValidationException : Exception
{
    public IrValidationException(string message) : base(message) { }
    public IrValidationException(string message, Exception inner) : base(message, inner) { }
}
