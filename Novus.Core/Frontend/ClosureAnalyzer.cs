using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// Analyzes closure expressions to determine captured variables and their capture modes.
///
/// The analyzer walks the closure body AST to find:
/// 1. Variables referenced that are defined in an outer scope (free variables)
/// 2. The capture mode for each variable (by-value, by-reference, or mutable)
/// 3. Whether the closure escapes its scope (affects allocation strategy)
///
/// Usage:
/// <code>
/// var analyzer = new ClosureAnalyzer(localVariables, currentFunctionParams);
/// var captures = analyzer.Analyze(closureBodyContext, explicitMutCaptures, explicitRefCaptures);
/// </code>
/// </summary>
public class ClosureAnalyzer
{
    private readonly IReadOnlyDictionary<string, IrLocalVariable> _localVariables;
    private readonly HashSet<string> _currentFunctionParams;
    private readonly HashSet<string> _closureParams;
    private readonly Dictionary<string, CapturedVariable> _captures;

    public ClosureAnalyzer(IReadOnlyDictionary<string, IrLocalVariable> localVariables, IEnumerable<string> currentFunctionParams)
    {
        _localVariables = localVariables;
        _currentFunctionParams = new HashSet<string>(currentFunctionParams);
        _closureParams = new HashSet<string>();
        _captures = new Dictionary<string, CapturedVariable>();
    }

    /// <summary>
    /// Analyzes a closure body to determine captured variables.
    /// </summary>
    /// <param name="body">The closure body (block context)</param>
    /// <param name="closureParams">Parameter names defined by the closure (not captures)</param>
    /// <param name="explicitMutCaptures">Variables explicitly marked as mut captures</param>
    /// <param name="explicitRefCaptures">Variables explicitly marked as &amp; captures</param>
    /// <returns>Information about captured variables</returns>
    public CaptureInfo Analyze(
        IParseTree body,
        IEnumerable<string> closureParams,
        IEnumerable<string> explicitMutCaptures,
        IEnumerable<string> explicitRefCaptures)
    {
        _closureParams.Clear();
        foreach (var param in closureParams)
            _closureParams.Add(param);

        _captures.Clear();

        var mutCaptures = new HashSet<string>(explicitMutCaptures);
        var refCaptures = new HashSet<string>(explicitRefCaptures);

        // Walk the AST to find all identifier references
        WalkForIdentifiers(body, mutCaptures, refCaptures);

        return new CaptureInfo(_captures.Values.ToList());
    }

    private void WalkForIdentifiers(IParseTree node, HashSet<string> mutCaptures, HashSet<string> refCaptures)
    {
        if (node is ITerminalNode terminal)
        {
            // Check if this is an identifier token
            if (terminal.Symbol.Type == NovusLexer.IDENTIFIER)
            {
                var name = terminal.GetText();
                TryAddCapture(name, mutCaptures, refCaptures);
            }
        }
        else
        {
            // Recurse into children
            for (int i = 0; i < node.ChildCount; i++)
            {
                WalkForIdentifiers(node.GetChild(i), mutCaptures, refCaptures);
            }
        }
    }

    private void TryAddCapture(string name, HashSet<string> mutCaptures, HashSet<string> refCaptures)
    {
        // Skip if already captured
        if (_captures.ContainsKey(name))
            return;

        // Skip if it's a closure parameter (not a capture)
        if (_closureParams.Contains(name))
            return;

        // Skip keywords that look like identifiers
        if (name == "self" || name == "Self" || name == "true" || name == "false" || name == "null")
            return;

        // Check if it's a local variable from an outer scope
        if (_localVariables.TryGetValue(name, out var localVar))
        {
            // This is a free variable - needs to be captured
            var mode = DetermineCaptureMode(name, localVar.Type, mutCaptures, refCaptures);
            _captures[name] = new CapturedVariable
            {
                Name = name,
                Type = localVar.Type,
                Mode = mode
            };
            return;
        }

        // Check if it's a function parameter from the outer scope
        // (function parameters are also in _localVariables, so this is mainly for documentation)
        if (_currentFunctionParams.Contains(name) && _localVariables.TryGetValue(name, out var paramVar))
        {
            var mode = DetermineCaptureMode(name, paramVar.Type, mutCaptures, refCaptures);
            _captures[name] = new CapturedVariable
            {
                Name = name,
                Type = paramVar.Type,
                Mode = mode
            };
        }

        // If not found as local/param, it might be:
        // - A function name (don't capture)
        // - A type name (don't capture)
        // - A constant (don't capture - inline the value)
        // - An enum variant (don't capture)
        // We only capture local variables and parameters
    }

    private CaptureMode DetermineCaptureMode(string name, IrType type, HashSet<string> mutCaptures, HashSet<string> refCaptures)
    {
        // Explicit mut capture -> Mutable (captures by pointer, allows mutation)
        if (mutCaptures.Contains(name))
            return CaptureMode.Mutable;

        // Explicit & capture -> ByReference (captures by pointer, read-only)
        if (refCaptures.Contains(name))
            return CaptureMode.ByReference;

        // Default: by-value for small types, by-reference for large types
        // Small = 8 bytes or less (fits in two registers on 68k)
        if (type.SizeInBytes <= 8)
            return CaptureMode.ByValue;

        return CaptureMode.ByReference;
    }
}

/// <summary>
/// Information about variables captured by a closure
/// </summary>
public class CaptureInfo
{
    public List<CapturedVariable> Captures { get; }

    public CaptureInfo(List<CapturedVariable> captures)
    {
        Captures = captures;
    }

    /// <summary>
    /// True if the closure captures no variables (can be optimized to function pointer)
    /// </summary>
    public bool IsStateless => Captures.Count == 0;

    /// <summary>
    /// True if any captures are mutable (closure cannot escape scope)
    /// </summary>
    public bool HasMutableCaptures => Captures.Any(c => c.Mode == CaptureMode.Mutable);

    /// <summary>
    /// Calculate total size of environment struct
    /// </summary>
    public int CalculateEnvironmentSize()
    {
        if (IsStateless)
            return 0;

        int size = 4; // refcount field (i32)
        foreach (var capture in Captures)
        {
            // Align to word boundary (68k prefers word-aligned)
            if (size % 2 != 0)
                size++;

            // Mutable and by-reference captures store pointers
            if (capture.Mode == CaptureMode.ByValue)
                size += capture.Type.SizeInBytes;
            else
                size += 4; // pointer size
        }

        // Align final size to word boundary
        if (size % 2 != 0)
            size++;

        return size;
    }

    /// <summary>
    /// Generate environment struct type for this closure
    /// </summary>
    public IrStructType GenerateEnvironmentType(string closureName)
    {
        var fields = new List<IrStructField>
        {
            new IrStructField("__refcount", IrIntType.I32)
        };

        foreach (var capture in Captures)
        {
            var fieldType = capture.Mode == CaptureMode.ByValue
                ? capture.Type
                : new IrPointerType(capture.Type);

            fields.Add(new IrStructField(capture.Name, fieldType));
        }

        return new IrStructType($"__closure_{closureName}_env", fields, new List<string>());
    }
}
