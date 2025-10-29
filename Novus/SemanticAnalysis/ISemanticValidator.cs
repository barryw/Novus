using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Interface for semantic validation plugins
/// Validators can be added to the SemanticAnalyzer to extend checking
/// </summary>
public interface ISemanticValidator
{
    /// <summary>
    /// Name of the validator for logging/debugging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Run validation on the AST
    /// Returns true if validation passed, false if errors were found
    /// </summary>
    /// <param name="context">The ANTLR parser context to validate</param>
    /// <param name="diagnostics">Diagnostics bag for reporting errors</param>
    /// <returns>True if validation passed</returns>
    bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics);
}

/// <summary>
/// Base class for validators providing common utilities
/// </summary>
public abstract class ValidatorBase : ISemanticValidator
{
    public abstract string Name { get; }

    public abstract bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics);

    /// <summary>
    /// Helper to report an error
    /// </summary>
    protected void ReportError(DiagnosticBag diagnostics, string code, string message, SourceLocation location)
    {
        diagnostics.ReportError(code, message, location);
    }

    /// <summary>
    /// Helper to report a warning
    /// </summary>
    protected void ReportWarning(DiagnosticBag diagnostics, string code, string message, SourceLocation location)
    {
        diagnostics.ReportWarning(code, message, location);
    }
}

/// <summary>
/// Manager for semantic validators
/// Runs validators in sequence and collects errors
/// </summary>
public class ValidatorManager
{
    private readonly List<ISemanticValidator> _validators = new();
    private readonly bool _verbose;

    public ValidatorManager(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Add a validator to the manager
    /// </summary>
    public void AddValidator(ISemanticValidator validator)
    {
        _validators.Add(validator);
    }

    /// <summary>
    /// Run all validators on the AST
    /// Returns true if all validators passed
    /// </summary>
    public bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics)
    {
        if (_verbose)
        {
            Console.WriteLine($"Running {_validators.Count} semantic validators...");
        }

        bool allPassed = true;

        foreach (var validator in _validators)
        {
            if (_verbose)
            {
                Console.WriteLine($"  Running {validator.Name}...");
            }

            bool passed = validator.Validate(context, diagnostics);
            allPassed &= passed;

            if (_verbose)
            {
                Console.WriteLine($"    -> {(passed ? "Passed" : "Failed")}");
            }

            // Continue running all validators even if one fails
            // This allows collecting all errors, not just the first one
        }

        if (_verbose)
        {
            Console.WriteLine($"Validation complete: {(allPassed ? "Passed" : "Failed")}");
        }

        return allPassed;
    }

    /// <summary>
    /// Create a standard validator manager with default validators
    /// </summary>
    public static ValidatorManager CreateDefaultManager(bool verbose = false)
    {
        var manager = new ValidatorManager(verbose);

        // Add standard validators
        // TODO: Extract these from SemanticAnalyzer
        // manager.AddValidator(new TypeCheckValidator());
        // manager.AddValidator(new ControlFlowValidator());

        return manager;
    }

    /// <summary>
    /// Create a validator manager with DSL validators
    /// </summary>
    public static ValidatorManager CreateDslManager(bool verbose = false)
    {
        var manager = CreateDefaultManager(verbose);

        // Add DSL-specific validators
        // manager.AddValidator(new CopperDslValidator());
        // manager.AddValidator(new BlitterDslValidator());

        return manager;
    }
}
