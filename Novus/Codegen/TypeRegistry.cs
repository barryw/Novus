using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Collects type definitions from all modules to generate a shared types header.
/// This eliminates duplicate type definitions across generated C files.
/// </summary>
public class TypeRegistry
{
    private readonly HashSet<IrEnumType> _enumTypes = new();
    private readonly HashSet<IrStructType> _structTypes = new();
    private bool _needsString = false;

    /// <summary>
    /// Register types from a module
    /// </summary>
    public void RegisterModule(IrModule module)
    {
        // Scan all functions for actually-used enum types
        // This includes return types, parameters, local variables, and instruction operands
        foreach (var function in module.Functions)
        {
            // Check return type
            if (function.ReturnType is IrEnumType enumRet && enumRet.GenericParameters.Count == 0)
            {
                if (!_enumTypes.Any(e => GetEnumName(e) == GetEnumName(enumRet)))
                    _enumTypes.Add(enumRet);
            }

            // Check parameters
            foreach (var param in function.Parameters)
            {
                if (param.Type is IrEnumType enumParam && enumParam.GenericParameters.Count == 0)
                {
                    if (!_enumTypes.Any(e => GetEnumName(e) == GetEnumName(enumParam)))
                        _enumTypes.Add(enumParam);
                }
            }

            // Check local variables
            foreach (var local in function.LocalVariables)
            {
                if (local.Type is IrEnumType enumLocal && enumLocal.GenericParameters.Count == 0)
                {
                    if (!_enumTypes.Any(e => GetEnumName(e) == GetEnumName(enumLocal)))
                        _enumTypes.Add(enumLocal);
                }
            }

            // Scan instructions for enum types
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrLocalDecl localDecl &&
                        localDecl.Type is IrEnumType enumDeclType &&
                        enumDeclType.GenericParameters.Count == 0)
                    {
                        if (!_enumTypes.Any(e => GetEnumName(e) == GetEnumName(enumDeclType)))
                            _enumTypes.Add(enumDeclType);
                    }

                    if (instruction is IrMatch match &&
                        match.MatchValue.Type is IrEnumType matchEnumType &&
                        matchEnumType.GenericParameters.Count == 0)
                    {
                        if (!_enumTypes.Any(e => GetEnumName(e) == GetEnumName(matchEnumType)))
                            _enumTypes.Add(matchEnumType);
                    }
                }
            }
        }

        // TODO: Add struct type collection when we implement structs
    }

    private bool UsesStringType(IrFunction function)
    {
        // Check return type
        if (function.ReturnType is IrStructType structType && structType.Name == "String")
            return true;

        // Check parameters
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrStructType st && st.Name == "String")
                return true;
        }

        // Check local variables and instructions (simplified check)
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl &&
                    localDecl.Type is IrStructType lst && lst.Name == "String")
                    return true;
            }
        }

        return false;
    }

    private string GetEnumName(IrEnumType enumType)
    {
        // Handle both regular enums and generic types like Result[T,E]
        if (enumType.GenericParameters.Count > 0)
        {
            return enumType.Name; // Use base name for comparison
        }
        return enumType.EnumName ?? enumType.Name;
    }

    public IEnumerable<IrEnumType> EnumTypes => _enumTypes;
    public IEnumerable<IrStructType> StructTypes => _structTypes;
    public bool NeedsString => _needsString;
}
