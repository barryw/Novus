using Novus.IR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Generic monomorphization transformation pass.
///
/// NOTE: Generic monomorphization is primarily handled during semantic analysis
/// in SemanticAnalyzer.cs via the MonomorphizeFunction method and the various
/// monomorphized struct/enum caches. This pass is retained for post-IR validation
/// and potential future optimizations on monomorphized code.
///
/// The monomorphization system works as follows:
/// 1. During semantic analysis, when a generic function is called with concrete types,
///    MonomorphizeFunction creates a specialized copy with substituted types.
/// 2. Monomorphized types (structs, enums) are cached using their cache key (e.g., "Vec&lt;i32&gt;").
/// 3. The IrBuilder generates IR using the already-monomorphized function symbols.
///
/// This pass can perform additional validation or optimization:
/// - Verify no unresolved generic types remain in the IR
/// - Inline small monomorphized functions
/// - Dead code elimination for unused monomorphizations
/// </summary>
public class GenericMonomorphizationPass : TransformPassBase
{
    public override string Name => "Generic Monomorphization";

    public override bool Transform(IrModule module)
    {
        bool changed = false;

        // Validate that no generic functions remain (all should be monomorphized)
        foreach (var func in module.Functions)
        {
            if (func.GenericParameters != null && func.GenericParameters.Count > 0 && !func.IsExtern)
            {
                // Generic function definitions should have been replaced by monomorphized versions
                // or are trait method placeholders. Only emit warning for non-trait definitions.
                if (!func.Name.Contains("::"))
                {
                    // Log warning: generic function not monomorphized (may be unused)
                    System.Console.WriteLine($"Warning: Generic function '{func.Name}' was not monomorphized (may be unused)");
                }
            }
        }

        // Validate that no unresolved generic types remain in function signatures
        foreach (var func in module.Functions.Where(f => !f.IsExtern))
        {
            if (ContainsUnresolvedGeneric(func.ReturnType))
            {
                throw new InvalidOperationException(
                    $"Function '{func.Name}' has unresolved generic in return type: {func.ReturnType.Name}");
            }

            foreach (var param in func.Parameters)
            {
                if (ContainsUnresolvedGeneric(param.Type))
                {
                    throw new InvalidOperationException(
                        $"Function '{func.Name}' parameter '{param.Name}' has unresolved generic type: {param.Type.Name}");
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Check if a type contains unresolved generic type parameters.
    /// </summary>
    private static bool ContainsUnresolvedGeneric(IrType type)
    {
        return type switch
        {
            IrGenericType => true,
            IrPointerType ptr => ContainsUnresolvedGeneric(ptr.PointeeType),
            IrArrayType arr => ContainsUnresolvedGeneric(arr.ElementType),
            IrStructType st when st.GenericParameters != null && st.GenericParameters.Count > 0 =>
                true, // Has generic parameters but wasn't monomorphized
            IrEnumType et when et.GenericParameters != null && et.GenericParameters.Count > 0 =>
                true, // Has generic parameters but wasn't monomorphized
            _ => false
        };
    }
}
