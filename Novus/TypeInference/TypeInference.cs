using Novus.IR;
using Antlr4.Runtime;
using System.Text;

namespace Novus.TypeInference;

/// <summary>
/// Represents a type variable - a placeholder for an unknown type that will be solved
/// </summary>
public class TypeVariable
{
    private static int _counter = 0;

    public int Id { get; }
    public string Name => $"?T{Id}";
    public IrType? ResolvedType { get; set; } // Null until solved

    public TypeVariable()
    {
        Id = _counter++;
    }

    public override string ToString() => ResolvedType?.Name ?? Name;
}

/// <summary>
/// Base class for type constraints
/// </summary>
public abstract class Constraint
{
    public ParserRuleContext? Context { get; set; }

    public abstract override string ToString();
}

/// <summary>
/// Equality constraint: T1 = T2
/// </summary>
public class EqualityConstraint : Constraint
{
    public object Left { get; set; }  // TypeVariable or IrType
    public object Right { get; set; } // TypeVariable or IrType

    public EqualityConstraint(object left, object right)
    {
        Left = left;
        Right = right;
    }

    public override string ToString() => $"{FormatType(Left)} = {FormatType(Right)}";

    private string FormatType(object t)
    {
        return t switch
        {
            TypeVariable tv => tv.Name,
            IrType irType => irType.Name,
            _ => t.ToString() ?? "?"
        };
    }
}

/// <summary>
/// Method call constraint: receiver.method(args) -> return_type
/// Used to propagate type information through method calls
/// </summary>
public class MethodCallConstraint : Constraint
{
    public object ReceiverType { get; set; }    // TypeVariable or IrType
    public string MethodName { get; set; }
    public List<object> ArgumentTypes { get; set; } // List of TypeVariable or IrType
    public object ReturnType { get; set; }      // TypeVariable or IrType

    public MethodCallConstraint(object receiverType, string methodName, List<object> argumentTypes, object returnType)
    {
        ReceiverType = receiverType;
        MethodName = methodName;
        ArgumentTypes = argumentTypes;
        ReturnType = returnType;
    }

    public override string ToString()
    {
        var args = string.Join(", ", ArgumentTypes.Select(FormatType));
        return $"{FormatType(ReceiverType)}.{MethodName}({args}) -> {FormatType(ReturnType)}";
    }

    private string FormatType(object t)
    {
        return t switch
        {
            TypeVariable tv => tv.Name,
            IrType irType => irType.Name,
            _ => t.ToString() ?? "?"
        };
    }
}

/// <summary>
/// Generic instantiation constraint: Type<T1, T2, ...>
/// Used when we know a generic type but need to solve its type parameters
/// </summary>
public class GenericInstantiationConstraint : Constraint
{
    public object ResultType { get; set; }          // TypeVariable - the result of instantiation
    public string GenericTypeName { get; set; }     // e.g., "Vec", "Option"
    public List<object> TypeArguments { get; set; } // TypeVariable or IrType for each parameter

    public GenericInstantiationConstraint(object resultType, string genericTypeName, List<object> typeArguments)
    {
        ResultType = resultType;
        GenericTypeName = genericTypeName;
        TypeArguments = typeArguments;
    }

    public override string ToString()
    {
        var args = string.Join(", ", TypeArguments.Select(FormatType));
        return $"{FormatType(ResultType)} = {GenericTypeName}<{args}>";
    }

    private string FormatType(object t)
    {
        return t switch
        {
            TypeVariable tv => tv.Name,
            IrType irType => irType.Name,
            _ => t.ToString() ?? "?"
        };
    }
}

/// <summary>
/// Constraint solver using unification algorithm
/// </summary>
public class ConstraintSolver
{
    private readonly List<Constraint> _constraints;
    private readonly Dictionary<int, TypeVariable> _typeVariables; // Track all type variables by ID
    private readonly IrModule _module; // Need module for looking up method signatures

    public ConstraintSolver(List<Constraint> constraints, Dictionary<int, TypeVariable> typeVariables, IrModule module)
    {
        _constraints = constraints;
        _typeVariables = typeVariables;
        _module = module;
    }

    /// <summary>
    /// Solve all constraints using unification algorithm
    /// </summary>
    public bool Solve(out List<string> errors)
    {
        errors = new List<string>();

        // Keep iterating until no more progress can be made
        bool madeProgress;
        int maxIterations = 100;
        int iteration = 0;

        do
        {
            madeProgress = false;
            iteration++;

            if (iteration > maxIterations)
            {
                errors.Add("Type inference failed: maximum iterations reached. Possible infinite loop in type constraints.");
                return false;
            }

            // Process each constraint
            foreach (var constraint in _constraints)
            {
                bool progress = constraint switch
                {
                    EqualityConstraint eq => SolveEquality(eq, errors),
                    MethodCallConstraint mc => SolveMethodCall(mc, errors),
                    GenericInstantiationConstraint gi => SolveGenericInstantiation(gi, errors),
                    _ => false
                };

                if (progress)
                {
                    madeProgress = true;
                }
            }

        } while (madeProgress);

        // Check if all type variables are resolved
        var unresolvedVars = _typeVariables.Values.Where(tv => tv.ResolvedType == null).ToList();
        if (unresolvedVars.Any())
        {
            foreach (var tv in unresolvedVars)
            {
                errors.Add($"Cannot infer type for {tv.Name}. Please add type annotation.");
            }
            return false;
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Resolve a type (TypeVariable or IrType) to a concrete IrType
    /// </summary>
    private IrType? ResolveType(object type)
    {
        return type switch
        {
            TypeVariable tv => tv.ResolvedType,
            IrType irType => irType,
            _ => null
        };
    }

    /// <summary>
    /// Solve an equality constraint: T1 = T2
    /// </summary>
    private bool SolveEquality(EqualityConstraint constraint, List<string> errors)
    {
        var left = ResolveType(constraint.Left);
        var right = ResolveType(constraint.Right);

        // Both unknown - can't make progress yet
        if (left == null && right == null)
            return false;

        // One side is known - unify
        if (left != null && right == null && constraint.Right is TypeVariable rightTv)
        {
            rightTv.ResolvedType = left;
            return true;
        }

        if (right != null && left == null && constraint.Left is TypeVariable leftTv)
        {
            leftTv.ResolvedType = right;
            return true;
        }

        // Both known - check compatibility
        if (left != null && right != null)
        {
            if (!TypesCompatible(left, right))
            {
                errors.Add($"Type mismatch: expected {left.Name}, got {right.Name}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Solve a method call constraint: receiver.method(args) -> return
    /// </summary>
    private bool SolveMethodCall(MethodCallConstraint constraint, List<string> errors)
    {
        var receiverType = ResolveType(constraint.ReceiverType);

        // Can't solve without knowing receiver type
        if (receiverType == null)
            return false;

        // Look up method signature
        var methodSig = LookupMethod(receiverType, constraint.MethodName);
        if (methodSig == null)
        {
            errors.Add($"Method {constraint.MethodName} not found on type {receiverType.Name}");
            return false;
        }

        bool madeProgress = false;

        // Unify argument types
        for (int i = 0; i < constraint.ArgumentTypes.Count && i < methodSig.ParameterTypes.Count; i++)
        {
            var argType = ResolveType(constraint.ArgumentTypes[i]);
            var paramType = methodSig.ParameterTypes[i];

            if (argType == null && constraint.ArgumentTypes[i] is TypeVariable argTv)
            {
                argTv.ResolvedType = paramType;
                madeProgress = true;
            }
            else if (argType != null && !TypesCompatible(argType, paramType))
            {
                errors.Add($"Argument type mismatch: expected {paramType.Name}, got {argType.Name}");
            }
        }

        // Unify return type
        var returnType = ResolveType(constraint.ReturnType);
        if (returnType == null && constraint.ReturnType is TypeVariable retTv)
        {
            retTv.ResolvedType = methodSig.ReturnType;
            madeProgress = true;
        }

        return madeProgress;
    }

    /// <summary>
    /// Solve a generic instantiation constraint: Type<T1, T2, ...>
    /// </summary>
    private bool SolveGenericInstantiation(GenericInstantiationConstraint constraint, List<string> errors)
    {
        // Check if all type arguments are resolved
        var resolvedArgs = new List<IrType>();
        foreach (var arg in constraint.TypeArguments)
        {
            var resolved = ResolveType(arg);
            if (resolved == null)
                return false; // Can't instantiate yet
            resolvedArgs.Add(resolved);
        }

        // All arguments resolved - create monomorphized type
        var resultType = ResolveType(constraint.ResultType);
        if (resultType != null)
            return false; // Already solved

        if (constraint.ResultType is not TypeVariable resultTv)
            return false;

        // Create the monomorphized struct type
        var monomorphizedType = CreateMonomorphizedType(constraint.GenericTypeName, resolvedArgs);
        if (monomorphizedType == null)
        {
            errors.Add($"Failed to instantiate generic type {constraint.GenericTypeName}");
            return false;
        }

        resultTv.ResolvedType = monomorphizedType;
        return true;
    }

    /// <summary>
    /// Look up method signature for a type
    /// </summary>
    private MethodSignature? LookupMethod(IrType receiverType, string methodName)
    {
        // Build mangled name for the method
        string mangledName = $"{receiverType.Name}_{methodName}";

        // Look up in module functions
        var function = _module.Functions.FirstOrDefault(f =>
            f.Name == mangledName || f.Name.Contains($"{receiverType.Name}::{methodName}"));

        if (function != null)
        {
            var paramTypes = function.Parameters.Skip(1).Select(p => p.Type).ToList(); // Skip 'self'
            return new MethodSignature(function.ReturnType, paramTypes);
        }

        return null;
    }

    /// <summary>
    /// Create a monomorphized generic type (e.g., Vec&lt;i32&gt;).
    /// Returns null if the generic type template is not available.
    /// Actual monomorphization is handled by SemanticAnalyzer and IrBuilder.
    /// </summary>
    private IrType? CreateMonomorphizedType(string genericName, List<IrType> typeArgs)
    {
        // Generic type monomorphization is handled at the semantic analysis phase.
        // The constraint solver collects type constraints; actual type creation
        // happens when the IrBuilder processes the AST with resolved types.
        // This method returns null to signal that the caller should use
        // the standard monomorphization path through SemanticAnalyzer.
        return null;
    }

    /// <summary>
    /// Check if two types are compatible for constraint solving.
    /// Handles exact matches, pointer/reference compatibility, and numeric coercion.
    /// </summary>
    private bool TypesCompatible(IrType t1, IrType t2)
    {
        // Exact match
        if (t1.Name == t2.Name)
            return true;

        // Pointer/reference compatibility: *T is compatible with &T
        if (t1 is IrPointerType p1 && t2 is IrReferenceType r2)
            return TypesCompatible(p1.PointeeType, r2.PointeeType);
        if (t1 is IrReferenceType r1 && t2 is IrPointerType p2)
            return TypesCompatible(r1.PointeeType, p2.PointeeType);

        // Mutable reference to immutable reference
        if (t1 is IrMutReferenceType m1 && t2 is IrReferenceType r3)
            return TypesCompatible(m1.PointeeType, r3.PointeeType);

        // Numeric coercion: smaller integer types can coerce to larger ones
        if (t1 is IrIntType i1 && t2 is IrIntType i2)
        {
            // Same signedness and t1 is smaller or equal
            if (i1.IsSigned == i2.IsSigned && i1.BitWidth <= i2.BitWidth)
                return true;
        }

        return false;
    }
}

/// <summary>
/// Method signature for constraint solving
/// </summary>
public class MethodSignature
{
    public IrType ReturnType { get; set; }
    public List<IrType> ParameterTypes { get; set; }

    public MethodSignature(IrType returnType, List<IrType> parameterTypes)
    {
        ReturnType = returnType;
        ParameterTypes = parameterTypes;
    }
}
