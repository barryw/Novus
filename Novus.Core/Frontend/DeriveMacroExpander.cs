using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Expands #[derive(...)] attributes into synthesized trait implementations.
/// Supported derive traits:
/// - Eq: Generates field-by-field equality comparison
/// - Hash: Generates field-by-field hash combination using FNV-1a
/// </summary>
public class DeriveMacroExpander
{
    private readonly DiagnosticBag _diagnostics;
    private readonly TypeInterner _typeInterner;
    private readonly IrModule _module;
    private readonly SymbolTable _symbols;

    // FNV-1a constants for 32-bit hash
    private const uint FnvPrime = 16777619;
    private const uint FnvOffsetBasis = 2166136261;

    public DeriveMacroExpander(
        DiagnosticBag diagnostics,
        TypeInterner typeInterner,
        IrModule module,
        SymbolTable symbols)
    {
        _diagnostics = diagnostics;
        _typeInterner = typeInterner;
        _module = module;
        _symbols = symbols;
    }

    /// <summary>
    /// Process all registered structs and generate derive implementations
    /// </summary>
    public void ExpandDerives()
    {
        foreach (var structType in _module.Structs.ToList()) // ToList() to avoid modification during iteration
        {
            if (structType.Attributes == null) continue;

            var deriveAttr = structType.Attributes.Get(KnownAttributes.Derive);
            if (deriveAttr == null) continue;

            // Skip generic structs - they need to be handled during monomorphization
            if (structType.GenericParameters.Count > 0) continue;

            var derivedTraits = ParseDeriveTraits(deriveAttr);

            foreach (var trait in derivedTraits)
            {
                switch (trait)
                {
                    case "Eq":
                        GenerateEqImpl(structType);
                        break;
                    case "Hash":
                        GenerateHashImpl(structType);
                        break;
                    default:
                        _diagnostics.ReportError(
                            ErrorCodes.UnknownDeriveTrait,
                            $"Unknown derive trait '{trait}'. Supported traits: Eq, Hash",
                            deriveAttr.Location
                        );
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Parse the derive attribute to get the list of traits to derive
    /// </summary>
    private List<string> ParseDeriveTraits(AttributeInfo deriveAttr)
    {
        var traits = new List<string>();

        // Derive arguments can be positional: #[derive(Eq, Hash)]
        foreach (var arg in deriveAttr.PositionalArgs)
        {
            if (arg is string traitName)
            {
                traits.Add(traitName);
            }
        }

        return traits;
    }

    /// <summary>
    /// Generate impl Eq for StructName { fn eq(&self, other: &Self) -> bool }
    /// </summary>
    private void GenerateEqImpl(IrStructType structType)
    {
        var typeName = structType.Name;
        // Use inherent impl naming for method lookup compatibility
        var mangledName = $"{typeName}::eq";

        // Check if already implemented
        if (_module.Functions.Any(f => f.Name == mangledName))
        {
            return;
        }

        var boolType = IrBoolType.Instance;
        var selfPtrType = _typeInterner.GetPointerType(structType);

        var function = new IrFunction(mangledName, boolType, Visibility.Public, false);
        function.Parameters.Add(new IrParameter("self", selfPtrType));
        function.Parameters.Add(new IrParameter("other", selfPtrType));

        // Create entry block
        var entryBlock = function.CreateBasicBlock("entry");

        if (structType.Fields.Count == 0)
        {
            // Empty struct - always equal
            entryBlock.Instructions.Add(new IrReturn(new IrBoolConstant(true)));
        }
        else
        {
            // Compare each field, short-circuit on mismatch
            int labelCounter = 0;
            var currentBlock = entryBlock;

            for (int i = 0; i < structType.Fields.Count; i++)
            {
                var field = structType.Fields[i];
                var isLast = i == structType.Fields.Count - 1;

                // Load self.field
                var selfVar = new IrVariable("self", selfPtrType);
                var selfFieldVal = new IrVariable($"self_{field.Name}", field.Type);
                function.LocalVariables.Add(new IrLocalVariable(selfFieldVal.Name, field.Type, false));
                currentBlock.Instructions.Add(new IrMemberAccess(
                    selfFieldVal.Name, selfVar, field.Name, field.Type, field.Offset));

                // Load other.field
                var otherVar = new IrVariable("other", selfPtrType);
                var otherFieldVal = new IrVariable($"other_{field.Name}", field.Type);
                function.LocalVariables.Add(new IrLocalVariable(otherFieldVal.Name, field.Type, false));
                currentBlock.Instructions.Add(new IrMemberAccess(
                    otherFieldVal.Name, otherVar, field.Name, field.Type, field.Offset));

                // Compare fields
                var cmpResult = new IrVariable($"cmp_{i}", boolType);
                function.LocalVariables.Add(new IrLocalVariable(cmpResult.Name, boolType, false));

                if (IsComparableType(field.Type))
                {
                    // Primitive types: direct comparison
                    currentBlock.Instructions.Add(new IrBinaryOp(
                        cmpResult.Name, IrBinaryOp.OpKind.Eq, selfFieldVal, otherFieldVal, boolType));
                }
                else if (field.Type is IrStructType nestedStruct)
                {
                    // Nested struct: call its eq method using field references as pointers
                    var nestedEqName = $"{nestedStruct.Name}::eq";
                    var selfFieldRef = new IrFieldReference(selfVar, field.Name, _typeInterner.GetPointerType(field.Type));
                    var otherFieldRef = new IrFieldReference(otherVar, field.Name, _typeInterner.GetPointerType(field.Type));

                    var call = new IrCall(nestedEqName, boolType, cmpResult.Name);
                    call.Arguments.Add(selfFieldRef);
                    call.Arguments.Add(otherFieldRef);
                    currentBlock.Instructions.Add(call);
                }
                else
                {
                    // Unknown type: assume equal
                    currentBlock.Instructions.Add(new IrStore(cmpResult.Name, new IrBoolConstant(true)));
                }

                if (isLast)
                {
                    // Last field: return the comparison result
                    currentBlock.Instructions.Add(new IrReturn(cmpResult));
                }
                else
                {
                    // Not last: branch on result
                    var nextBlock = function.CreateBasicBlock($"check_{labelCounter++}");
                    var falseBlock = function.CreateBasicBlock($"return_false_{labelCounter}");

                    currentBlock.Instructions.Add(new IrConditionalBranch(cmpResult, nextBlock.Label, falseBlock.Label));

                    // False block returns false
                    falseBlock.Instructions.Add(new IrReturn(new IrBoolConstant(false)));

                    currentBlock = nextBlock;
                }
            }
        }

        _module.AddFunction(function);

        // Register the trait implementation
        var traitImpl = new IrTraitImpl("Eq", new List<IrType>(), typeName, structType, new List<string>());
        _module.AddTraitImpl(traitImpl);
    }

    /// <summary>
    /// Generate impl Hash for StructName { fn hash(&self) -> u32 }
    /// Uses FNV-1a algorithm to combine field hashes
    /// </summary>
    private void GenerateHashImpl(IrStructType structType)
    {
        var typeName = structType.Name;
        // Use inherent impl naming for method lookup compatibility
        var mangledName = $"{typeName}::hash";

        // Check if already implemented
        if (_module.Functions.Any(f => f.Name == mangledName))
        {
            return;
        }

        var u32Type = IrIntType.U32;
        var selfPtrType = _typeInterner.GetPointerType(structType);

        var function = new IrFunction(mangledName, u32Type, Visibility.Public, false);
        function.Parameters.Add(new IrParameter("self", selfPtrType));

        // Create entry block
        var entryBlock = function.CreateBasicBlock("entry");

        // Initialize hash to FNV offset basis
        var hashVar = new IrVariable("hash", u32Type);
        function.LocalVariables.Add(new IrLocalVariable(hashVar.Name, u32Type, true));
        entryBlock.Instructions.Add(new IrLocalDecl(hashVar.Name, u32Type, true,
            new IrConstant(unchecked((long)FnvOffsetBasis), u32Type)));

        if (structType.Fields.Count == 0)
        {
            // Empty struct - return offset basis
            entryBlock.Instructions.Add(new IrReturn(hashVar));
        }
        else
        {
            var primeConst = new IrConstant(unchecked((long)FnvPrime), u32Type);

            for (int i = 0; i < structType.Fields.Count; i++)
            {
                var field = structType.Fields[i];

                // Load self.field
                var selfVar = new IrVariable("self", selfPtrType);
                var selfFieldVal = new IrVariable($"self_{field.Name}", field.Type);
                function.LocalVariables.Add(new IrLocalVariable(selfFieldVal.Name, field.Type, false));
                entryBlock.Instructions.Add(new IrMemberAccess(
                    selfFieldVal.Name, selfVar, field.Name, field.Type, field.Offset));

                // Get field hash
                var fieldHash = new IrVariable($"field_hash_{i}", u32Type);
                function.LocalVariables.Add(new IrLocalVariable(fieldHash.Name, u32Type, false));

                if (IsHashableType(field.Type))
                {
                    // Primitive types: cast to u32 for simple hashing
                    var castValue = new IrCastValue(selfFieldVal, field.Type, u32Type);
                    entryBlock.Instructions.Add(new IrStore(fieldHash.Name, castValue));
                }
                else if (field.Type is IrStructType nestedStruct)
                {
                    // Nested struct: call its hash method using field reference as pointer
                    var nestedHashName = $"{nestedStruct.Name}::hash";
                    var selfFieldRef = new IrFieldReference(selfVar, field.Name, _typeInterner.GetPointerType(field.Type));

                    var call = new IrCall(nestedHashName, u32Type, fieldHash.Name);
                    call.Arguments.Add(selfFieldRef);
                    entryBlock.Instructions.Add(call);
                }
                else
                {
                    // Unknown type: hash as 0
                    entryBlock.Instructions.Add(new IrStore(fieldHash.Name, new IrConstant(0, u32Type)));
                }

                // FNV-1a: hash = hash XOR byte; hash = hash * prime
                var xorResult = new IrVariable($"xor_{i}", u32Type);
                function.LocalVariables.Add(new IrLocalVariable(xorResult.Name, u32Type, false));
                entryBlock.Instructions.Add(new IrBinaryOp(
                    xorResult.Name, IrBinaryOp.OpKind.Xor, hashVar, fieldHash, u32Type));

                var mulResult = new IrVariable($"mul_{i}", u32Type);
                function.LocalVariables.Add(new IrLocalVariable(mulResult.Name, u32Type, false));
                entryBlock.Instructions.Add(new IrBinaryOp(
                    mulResult.Name, IrBinaryOp.OpKind.Mul, xorResult, primeConst, u32Type));

                // Update hash variable
                entryBlock.Instructions.Add(new IrStore(hashVar.Name, mulResult));
            }

            entryBlock.Instructions.Add(new IrReturn(hashVar));
        }

        _module.AddFunction(function);

        // Register the trait implementation
        var traitImpl = new IrTraitImpl("Hash", new List<IrType>(), typeName, structType, new List<string>());
        _module.AddTraitImpl(traitImpl);
    }

    /// <summary>
    /// Check if a type supports direct equality comparison
    /// </summary>
    private bool IsComparableType(IrType type)
    {
        return type is IrIntType ||
               type is IrBoolType ||
               type is IrPointerType ||
               IsSimpleEnum(type);  // Simple enums (no associated data)
    }

    /// <summary>
    /// Check if a type can be directly hashed (cast to integer)
    /// </summary>
    private bool IsHashableType(IrType type)
    {
        return type is IrIntType ||
               type is IrBoolType ||
               type is IrPointerType ||
               IsSimpleEnum(type);
    }

    /// <summary>
    /// Check if an enum has no associated data on any variant (C-style enum)
    /// </summary>
    private bool IsSimpleEnum(IrType type)
    {
        if (type is not IrEnumType enumType) return false;
        return enumType.Variants.All(v => v.AssociatedData.Count == 0);
    }
}
