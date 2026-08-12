using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Handles resolution of overloaded functions based on argument types.
/// Uses C#-style overload resolution: exact matches preferred over implicit conversions.
/// </summary>
public static class OverloadResolution
{
    /// <summary>
    /// Result of overload resolution
    /// </summary>
    public enum ResolutionResult
    {
        /// <summary>Single best match found</summary>
        Success,
        /// <summary>No matching overload found</summary>
        NoMatch,
        /// <summary>Multiple overloads match equally well</summary>
        Ambiguous
    }

    /// <summary>
    /// Represents a match score for overload resolution
    /// </summary>
    private class OverloadMatch
    {
        public FunctionSymbol Function { get; }
        public int Score { get; }
        public List<CoercionKind> Coercions { get; }

        public OverloadMatch(FunctionSymbol function, int score, List<CoercionKind> coercions)
        {
            Function = function;
            Score = score;
            Coercions = coercions;
        }
    }

    /// <summary>
    /// Types of coercion that can be applied
    /// </summary>
    public enum CoercionKind
    {
        /// <summary>Types match exactly, no coercion needed</summary>
        None,
        /// <summary>Implicit numeric widening (e.g., i16 -> i32)</summary>
        NumericWidening,
        /// <summary>Implicit reference coercion</summary>
        ReferenceCoercion,
        /// <summary>No valid coercion exists</summary>
        Invalid
    }

    /// <summary>
    /// Resolves which overload to call given argument types.
    /// </summary>
    /// <param name="candidates">List of function overloads with the same name</param>
    /// <param name="argumentTypes">Types of the arguments at the call site</param>
    /// <param name="selectedFunction">The selected function if resolution succeeds</param>
    /// <returns>Resolution result indicating success, no match, or ambiguity</returns>
    public static ResolutionResult Resolve(
        IReadOnlyList<FunctionSymbol> candidates,
        IReadOnlyList<IrType> argumentTypes,
        out FunctionSymbol? selectedFunction)
    {
        selectedFunction = null;

        if (candidates.Count == 0)
            return ResolutionResult.NoMatch;

        // Single candidate - just check if it matches
        if (candidates.Count == 1)
        {
            var candidate = candidates[0];
            if (MatchesSignature(candidate, argumentTypes, out _))
            {
                selectedFunction = candidate;
                return ResolutionResult.Success;
            }
            return ResolutionResult.NoMatch;
        }

        // Multiple candidates - find best match
        var matches = new List<OverloadMatch>();

        foreach (var candidate in candidates)
        {
            if (MatchesSignature(candidate, argumentTypes, out var coercions))
            {
                var score = CalculateScore(coercions);
                matches.Add(new OverloadMatch(candidate, score, coercions));
            }
        }

        if (matches.Count == 0)
            return ResolutionResult.NoMatch;

        // Sort by score (highest first)
        matches.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Check for ambiguity (multiple matches with same highest score)
        if (matches.Count > 1 && matches[0].Score == matches[1].Score)
            return ResolutionResult.Ambiguous;

        selectedFunction = matches[0].Function;
        return ResolutionResult.Success;
    }

    /// <summary>
    /// Resolves which IrFunction overload to call given argument types.
    /// </summary>
    public static ResolutionResult Resolve(
        IReadOnlyList<IrFunction> candidates,
        IReadOnlyList<IrType> argumentTypes,
        out IrFunction? selectedFunction)
    {
        selectedFunction = null;

        if (candidates.Count == 0)
            return ResolutionResult.NoMatch;

        // Single candidate - just check if it matches
        if (candidates.Count == 1)
        {
            var candidate = candidates[0];
            if (MatchesSignature(candidate, argumentTypes, out _))
            {
                selectedFunction = candidate;
                return ResolutionResult.Success;
            }
            return ResolutionResult.NoMatch;
        }

        // Multiple candidates - find best match
        var matches = new List<(IrFunction Function, int Score, List<CoercionKind> Coercions)>();

        foreach (var candidate in candidates)
        {
            if (MatchesSignature(candidate, argumentTypes, out var coercions))
            {
                var score = CalculateScore(coercions);
                matches.Add((candidate, score, coercions));
            }
        }

        if (matches.Count == 0)
            return ResolutionResult.NoMatch;

        // Sort by score (highest first)
        matches.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Check for ambiguity
        if (matches.Count > 1 && matches[0].Score == matches[1].Score)
            return ResolutionResult.Ambiguous;

        selectedFunction = matches[0].Function;
        return ResolutionResult.Success;
    }

    /// <summary>
    /// Checks if a function's parameter types match the given argument types.
    /// </summary>
    private static bool MatchesSignature(
        FunctionSymbol function,
        IReadOnlyList<IrType> argumentTypes,
        out List<CoercionKind> coercions)
    {
        coercions = new List<CoercionKind>();

        // Count non-variadic parameters
        var nonVariadicParams = function.Parameters.Where(p => !p.IsVariadic).ToList();

        // For variadic functions, we need at least the non-variadic params
        if (function.IsVariadic)
        {
            if (argumentTypes.Count < nonVariadicParams.Count)
                return false;
        }
        else
        {
            // Non-variadic: exact count match required
            if (argumentTypes.Count != nonVariadicParams.Count)
                return false;
        }

        // Check each argument against parameter
        for (int i = 0; i < nonVariadicParams.Count; i++)
        {
            var coercion = GetCoercionKind(argumentTypes[i], nonVariadicParams[i].Type);
            if (coercion == CoercionKind.Invalid)
                return false;
            coercions.Add(coercion);
        }

        // For variadic functions, remaining arguments are always valid
        for (int i = nonVariadicParams.Count; i < argumentTypes.Count; i++)
        {
            coercions.Add(CoercionKind.None); // Variadic args don't affect score
        }

        return true;
    }

    /// <summary>
    /// Checks if an IrFunction's parameter types match the given argument types.
    /// </summary>
    private static bool MatchesSignature(
        IrFunction function,
        IReadOnlyList<IrType> argumentTypes,
        out List<CoercionKind> coercions)
    {
        coercions = new List<CoercionKind>();

        // Count non-variadic parameters
        var nonVariadicParams = function.Parameters.Where(p => !p.IsVariadic).ToList();

        // For variadic functions, we need at least the non-variadic params
        if (function.Parameters.Any(p => p.IsVariadic))
        {
            if (argumentTypes.Count < nonVariadicParams.Count)
                return false;
        }
        else
        {
            // Non-variadic: exact count match required
            if (argumentTypes.Count != nonVariadicParams.Count)
                return false;
        }

        // Check each argument against parameter
        for (int i = 0; i < nonVariadicParams.Count; i++)
        {
            var coercion = GetCoercionKind(argumentTypes[i], nonVariadicParams[i].Type);
            if (coercion == CoercionKind.Invalid)
                return false;
            coercions.Add(coercion);
        }

        // For variadic functions, remaining arguments are always valid
        for (int i = nonVariadicParams.Count; i < argumentTypes.Count; i++)
        {
            coercions.Add(CoercionKind.None);
        }

        return true;
    }

    /// <summary>
    /// Determines what kind of coercion (if any) is needed to convert argType to paramType.
    /// </summary>
    public static CoercionKind GetCoercionKind(IrType argType, IrType paramType)
    {
        // Exact match
        if (TypesEqual(argType, paramType))
            return CoercionKind.None;

        // Numeric widening (smaller int to larger int)
        if (argType is IrIntType argInt && paramType is IrIntType paramInt)
        {
            if (CanWidenNumeric(argInt, paramInt))
                return CoercionKind.NumericWidening;
        }

        // Pointer coercion: *T can coerce to *void (like C's void*)
        if (argType is IrPointerType argPtr && paramType is IrPointerType paramPtr)
        {
            if (paramPtr.PointeeType is IrVoidType)
                return CoercionKind.ReferenceCoercion;
            if (TypesEqual(argPtr.PointeeType, paramPtr.PointeeType))
                return CoercionKind.None;
        }

        // Reference to pointer coercion: &T can coerce to *T
        if (argType is IrReferenceType argRef && paramType is IrPointerType ptrParam)
        {
            if (TypesEqual(argRef.PointeeType, ptrParam.PointeeType))
                return CoercionKind.ReferenceCoercion;
        }

        return CoercionKind.Invalid;
    }

    /// <summary>
    /// Checks if a smaller integer type can be widened to a larger one.
    /// </summary>
    private static bool CanWidenNumeric(IrIntType from, IrIntType to)
    {
        // Can only widen if signedness matches
        if (from.IsSigned != to.IsSigned)
            return false;

        // Can widen if target is larger
        return GetIntBitWidth(to) > GetIntBitWidth(from);
    }

    private static int GetIntBitWidth(IrIntType intType)
    {
        return intType.BitWidth;
    }

    /// <summary>
    /// Calculates a score for an overload match. Higher is better.
    /// Exact matches score higher than coerced matches.
    /// </summary>
    private static int CalculateScore(List<CoercionKind> coercions)
    {
        int score = 0;
        foreach (var coercion in coercions)
        {
            score += coercion switch
            {
                CoercionKind.None => 3,              // Exact match
                CoercionKind.NumericWidening => 1,   // Widening allowed but not preferred
                CoercionKind.ReferenceCoercion => 1, // Reference coercion
                _ => 0
            };
        }
        return score;
    }

    /// <summary>
    /// Checks if two types are equal for overload resolution purposes.
    /// </summary>
    private static bool TypesEqual(IrType a, IrType b)
    {
        if (ReferenceEquals(a, b))
            return true;

        // Handle int types
        if (a is IrIntType intA && b is IrIntType intB)
            return intA.BitWidth == intB.BitWidth && intA.IsSigned == intB.IsSigned;

        // Handle pointer types
        if (a is IrPointerType ptrA && b is IrPointerType ptrB)
            return TypesEqual(ptrA.PointeeType, ptrB.PointeeType);

        // Handle immutable reference types
        if (a is IrReferenceType refA && b is IrReferenceType refB)
            return TypesEqual(refA.PointeeType, refB.PointeeType);

        // Handle mutable reference types
        if (a is IrMutReferenceType mutRefA && b is IrMutReferenceType mutRefB)
            return TypesEqual(mutRefA.PointeeType, mutRefB.PointeeType);

        // Handle array types
        if (a is IrArrayType arrA && b is IrArrayType arrB)
            return arrA.Length == arrB.Length && arrA.LengthParameter == arrB.LengthParameter &&
                   TypesEqual(arrA.ElementType, arrB.ElementType);

        // Handle struct types
        if (a is IrStructType structA && b is IrStructType structB)
            return structA.StructName == structB.StructName &&
                   (structA.CacheKey ?? structA.StructName) == (structB.CacheKey ?? structB.StructName);

        // Handle enum types
        if (a is IrEnumType enumA && b is IrEnumType enumB)
            return enumA.EnumName == enumB.EnumName &&
                   (enumA.CacheKey ?? enumA.EnumName) == (enumB.CacheKey ?? enumB.EnumName);

        // Handle void
        if (a is IrVoidType && b is IrVoidType)
            return true;

        // Handle bool
        if (a is IrBoolType && b is IrBoolType)
            return true;

        // Handle float types
        if (a is IrFloatType floatA && b is IrFloatType floatB)
            return floatA.BitWidth == floatB.BitWidth;

        // Handle fixed types
        if (a is IrFixedType fixedA && b is IrFixedType fixedB)
            return fixedA.BitWidth == fixedB.BitWidth;

        // Handle tuple types
        if (a is IrTupleType tupleA && b is IrTupleType tupleB)
        {
            if (tupleA.ElementTypes.Count != tupleB.ElementTypes.Count)
                return false;
            for (int i = 0; i < tupleA.ElementTypes.Count; i++)
            {
                if (!TypesEqual(tupleA.ElementTypes[i], tupleB.ElementTypes[i]))
                    return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates a signature string for a function (used for duplicate detection and mangling).
    /// Format: "funcName(type1,type2,type3)"
    /// </summary>
    public static string GetSignatureKey(string functionName, IReadOnlyList<ParameterSymbol> parameters)
    {
        var paramTypes = parameters
            .Where(p => !p.IsVariadic)
            .Select(p => GetTypeKey(p.Type));
        return $"{functionName}({string.Join(",", paramTypes)})";
    }

    /// <summary>
    /// Generates a signature string for an IrFunction.
    /// </summary>
    public static string GetSignatureKey(IrFunction function)
    {
        var paramTypes = function.Parameters
            .Where(p => !p.IsVariadic)
            .Select(p => GetTypeKey(p.Type));
        return $"{function.Name}({string.Join(",", paramTypes)})";
    }

    /// <summary>
    /// Gets a unique string key for a type (used in signature generation).
    /// </summary>
    public static string GetTypeKey(IrType type)
    {
        return type switch
        {
            IrIntType intType => $"{(intType.IsSigned ? "i" : "u")}{intType.BitWidth}",
            IrBoolType => "bool",
            IrVoidType => "void",
            IrFloatType floatType => $"f{floatType.BitWidth}",
            IrFixedType fixedType => $"fixed{fixedType.BitWidth}",
            IrPointerType ptrType => $"*{GetTypeKey(ptrType.PointeeType)}",
            IrReferenceType refType => $"&{GetTypeKey(refType.PointeeType)}",
            IrMutReferenceType mutRefType => $"&mut {GetTypeKey(mutRefType.PointeeType)}",
            IrArrayType arrType => $"[{GetTypeKey(arrType.ElementType)};{arrType.LengthParameter ?? arrType.Length.ToString()}]",
            IrTupleType tupleType => $"({string.Join(",", tupleType.ElementTypes.Select(GetTypeKey))})",
            IrStructType structType => structType.CacheKey ?? structType.StructName,
            IrEnumType enumType => enumType.CacheKey ?? enumType.EnumName,
            IrGenericType genType => genType.Name,
            _ => type.GetType().Name
        };
    }

    /// <summary>
    /// Generates a mangled suffix for overloaded functions based on parameter types.
    /// Used to create unique C symbol names.
    /// </summary>
    public static string GetOverloadSuffix(IReadOnlyList<IrType> parameterTypes)
    {
        if (parameterTypes.Count == 0)
            return "";

        var parts = parameterTypes.Select(t => MangleTypeForSuffix(t));
        return "__" + string.Join("_", parts);
    }

    /// <summary>
    /// Generates a mangled suffix for a function's parameters.
    /// </summary>
    public static string GetOverloadSuffix(IrFunction function)
    {
        var paramTypes = function.Parameters
            .Where(p => !p.IsVariadic)
            .Select(p => p.Type)
            .ToList();
        return GetOverloadSuffix(paramTypes);
    }

    /// <summary>
    /// Mangles a type name for use in C symbol suffix.
    /// </summary>
    private static string MangleTypeForSuffix(IrType type)
    {
        return type switch
        {
            IrIntType intType => $"{(intType.IsSigned ? "i" : "u")}{intType.BitWidth}",
            IrBoolType => "bool",
            IrVoidType => "void",
            IrFloatType floatType => $"f{floatType.BitWidth}",
            IrFixedType fixedType => $"fixed{fixedType.BitWidth}",
            IrPointerType ptrType => $"ptr_{MangleTypeForSuffix(ptrType.PointeeType)}",
            IrReferenceType refType => $"ref_{MangleTypeForSuffix(refType.PointeeType)}",
            IrMutReferenceType mutRefType => $"mutref_{MangleTypeForSuffix(mutRefType.PointeeType)}",
            IrArrayType arrType => $"arr{arrType.LengthParameter ?? arrType.Length.ToString()}_{MangleTypeForSuffix(arrType.ElementType)}",
            IrTupleType tupleType => $"tuple_{string.Join("_", tupleType.ElementTypes.Select(MangleTypeForSuffix))}",
            IrStructType structType => (structType.CacheKey ?? structType.StructName)
                .Replace("::", "_").Replace("<", "_").Replace(">", "").Replace(",", "_").Replace(" ", ""),
            IrEnumType enumType => (enumType.CacheKey ?? enumType.EnumName)
                .Replace("::", "_").Replace("<", "_").Replace(">", "").Replace(",", "_").Replace(" ", ""),
            _ => "unknown"
        };
    }
}
