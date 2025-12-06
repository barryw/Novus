using System.Collections.Immutable;

namespace Novus.Frontend;

/// <summary>
/// Strongly-typed cache key for method templates.
/// Format: "TypeName::MethodName"
/// </summary>
/// <param name="TypeName">The name of the type containing the method</param>
/// <param name="MethodName">The name of the method</param>
public readonly record struct MethodTemplateKey(string TypeName, string MethodName)
{
    /// <summary>
    /// Create a key from a combined string (for backward compatibility)
    /// </summary>
    public static MethodTemplateKey Parse(string combined)
    {
        var parts = combined.Split("::", 2);
        if (parts.Length == 2)
            return new MethodTemplateKey(parts[0], parts[1]);
        return new MethodTemplateKey("", combined);
    }

    public override string ToString() => $"{TypeName}::{MethodName}";
}

/// <summary>
/// Strongly-typed cache key for function templates.
/// </summary>
/// <param name="FunctionName">The name of the generic function</param>
public readonly record struct FunctionTemplateKey(string FunctionName)
{
    public override string ToString() => FunctionName;

    public static implicit operator FunctionTemplateKey(string name) => new(name);
}

/// <summary>
/// Strongly-typed cache key for instantiated generics.
/// Format: "BaseName<TypeArg1,TypeArg2,...>"
/// </summary>
public readonly record struct InstantiationKey
{
    public string BaseName { get; }
    public ImmutableArray<string> TypeArguments { get; }

    public InstantiationKey(string baseName, ImmutableArray<string> typeArguments)
    {
        BaseName = baseName;
        TypeArguments = typeArguments;
    }

    public InstantiationKey(string baseName, IEnumerable<string> typeArguments)
        : this(baseName, typeArguments.ToImmutableArray())
    {
    }

    public InstantiationKey(string baseName, params string[] typeArguments)
        : this(baseName, typeArguments.ToImmutableArray())
    {
    }

    /// <summary>
    /// Create a key from a cache key string (e.g., "Vec<i32>")
    /// </summary>
    public static InstantiationKey Parse(string cacheKey)
    {
        var genericStart = cacheKey.IndexOf('<');
        if (genericStart < 0)
            return new InstantiationKey(cacheKey, ImmutableArray<string>.Empty);

        var baseName = cacheKey[..genericStart];
        var typeArgsStr = cacheKey[(genericStart + 1)..^1]; // Remove < and >

        // Parse type arguments, handling nested generics
        var typeArgs = ParseTypeArguments(typeArgsStr);
        return new InstantiationKey(baseName, typeArgs);
    }

    private static ImmutableArray<string> ParseTypeArguments(string typeArgsStr)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        var depth = 0;
        var currentArg = new System.Text.StringBuilder();

        foreach (var c in typeArgsStr)
        {
            if (c == '<')
            {
                depth++;
                currentArg.Append(c);
            }
            else if (c == '>')
            {
                depth--;
                currentArg.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(currentArg.ToString().Trim());
                currentArg.Clear();
            }
            else
            {
                currentArg.Append(c);
            }
        }

        if (currentArg.Length > 0)
            result.Add(currentArg.ToString().Trim());

        return result.ToImmutable();
    }

    public override string ToString()
    {
        if (TypeArguments.IsEmpty)
            return BaseName;
        return $"{BaseName}<{string.Join(",", TypeArguments)}>";
    }

    /// <summary>
    /// Create a method instantiation key (e.g., "Vec<i32>::push")
    /// </summary>
    public InstantiationKey WithMethod(string methodName)
    {
        var newBaseName = $"{this}::{methodName}";
        return new InstantiationKey(newBaseName, ImmutableArray<string>.Empty);
    }
}

/// <summary>
/// Strongly-typed cache key for monomorphized types (structs/enums).
/// </summary>
public readonly record struct MonomorphizedTypeKey
{
    public string TypeName { get; }
    public ImmutableArray<string> TypeArguments { get; }

    public MonomorphizedTypeKey(string typeName, ImmutableArray<string> typeArguments)
    {
        TypeName = typeName;
        TypeArguments = typeArguments;
    }

    public MonomorphizedTypeKey(string typeName, IEnumerable<string> typeArguments)
        : this(typeName, typeArguments.ToImmutableArray())
    {
    }

    /// <summary>
    /// Create from cache key string (e.g., "Result<i32,Error>")
    /// </summary>
    public static MonomorphizedTypeKey Parse(string cacheKey)
    {
        var key = InstantiationKey.Parse(cacheKey);
        return new MonomorphizedTypeKey(key.BaseName, key.TypeArguments);
    }

    public override string ToString()
    {
        if (TypeArguments.IsEmpty)
            return TypeName;
        return $"{TypeName}<{string.Join(",", TypeArguments)}>";
    }
}
