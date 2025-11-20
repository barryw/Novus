using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing declaration registration methods.
/// This file contains methods for registering constants, statics, enums, structs, and traits.
/// </summary>
public partial class IrBuilder
{
    private void StoreGenericMethodTemplate(string typeName, string methodName, List<string> genericParams, NovusParser.FunctionDeclarationContext funcDecl)
    {
        var templateKey = $"{typeName}::{methodName}";
        // Capture current constants dictionary (make a copy so imports don't affect templates)
        var templateConstants = GetConstantsAsTuples();
        _genericMethodTemplates[templateKey] = (genericParams, funcDecl, templateConstants);
    }

    /// <summary>
    /// Generate a mangled name for a method.
    /// Trait impls: Type_Trait_TypeArg1_TypeArg2_method (e.g., Counter_Iterator_i32_next)
    /// Inherent impls: Type::method
    /// </summary>
    private string GenerateMethodMangledName(string typeName, string methodName, bool isTraitImpl, string? traitName, List<IrType> traitTypeArgs)
    {
        if (isTraitImpl && traitName != null)
        {
            var typeArgsSuffix = traitTypeArgs.Count > 0
                ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                : "";
            return $"{typeName}_{traitName}{typeArgsSuffix}_{methodName}";
        }
        else
        {
            return $"{typeName}::{methodName}";
        }
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Looks up the implementing type by name from the symbol table.
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, string typeName)
    {
        if (selfParam == null) return;

        var isMutable = selfParam.KW_MUT() != null;
        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

        // Determine self type - look up the implementing type (struct, enum, or primitive)
        IrType? implType = null;
        var foundStruct = _symbols.LookupStruct(typeName);
        var foundEnum = _symbols.LookupEnum(typeName);

        if (foundStruct != null)
        {
            implType = foundStruct;
        }
        else if (foundEnum != null)
        {
            implType = foundEnum;
        }
        else
        {
            // Try primitive types
            implType = MapPrimitiveTypeName(typeName);

            if (implType == null)
            {
                var errorLocation = selfParam != null
                    ? GetLocation(selfParam)
                    : new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.TypeNotFound,
                    $"Type '{typeName}' not found for impl block",
                    errorLocation
                );
                return;
            }
        }

        IrType selfType = implType;
        if (isBorrowed)
        {
            // Use pointer types for borrowed self parameters (& in Novus produces *T, not &T)
            selfType = _typeInterner.GetPointerType(selfType);
        }

        function.Parameters.Add(new IrParameter("self", selfType));
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Uses the provided implementing type directly (useful for monomorphized types).
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, IrType implementingType)
    {
        if (selfParam == null) return;

        var isMutable = selfParam.KW_MUT() != null;
        var isBorrowed = selfParam.GetChild(0).GetText() == "&";

        IrType selfType = implementingType;
        if (isBorrowed)
        {
            // Use pointer types for borrowed self parameters (& in Novus produces *T, not &T)
            selfType = _typeInterner.GetPointerType(selfType);
        }

        function.Parameters.Add(new IrParameter("self", selfType));
    }

    private void RegisterConstant(NovusParser.ConstDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check for pub/internal keywords
        var (visibility, _, _) = AstModifierHelper.ParseModifiers(context, 3);

        // Evaluate the constant expression using the evaluator
        var valueExpr = context.expression();

        // Convert constants dict to use object values for evaluator
        var constantValues = GetConstantValues();

        var evaluator = new SemanticAnalysis.ConstantExpressionEvaluator(constantValues);
        int? value = evaluator.Visit(valueExpr);

        if (value != null)
        {
            // Handle type - either explicit or inferred
            IrType type;
            if (context.type() != null)
            {
                // Explicit type annotation provided
                type = ParseType(context.type());
            }
            else
            {
                // Infer type from the evaluated value
                // Default to i32 for integer literals
                type = IrIntType.I32;
            }

            _symbols.RegisterConstant(name, type, value);
            // Also store in the IR module for code generator access
            _module.Constants[name] = (visibility, type, value);
        }
    }

    private void RegisterStatic(NovusParser.StaticDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Check for pub/internal/mut keywords
        var (visibility, _, isMutable) = AstModifierHelper.ParseModifiers(context, 5);

        // Evaluate the initial value expression
        var valueExpr = context.expression();

        // For now, we'll create a temporary function context to evaluate the expression
        // In the future, we should allow const expressions only
        _currentFunction = new IrFunction("__static_init", IrVoidType.Instance);
        _currentBlock = _currentFunction.CreateBasicBlock("entry");

        var initialValue = (IrValue?)Visit(valueExpr);

        // Restore state
        _currentFunction = null;
        _currentBlock = null;

        if (initialValue != null)
        {
            var staticVar = new IrStaticVariable(name, type, visibility, isMutable, initialValue);
            _module.StaticVariables.Add(staticVar);
        }
    }

    private void RegisterExternalVariable(NovusParser.GlobalVariableDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var type = ParseType(context.type());

        // Check for optional 'at <address>' clause
        long? address = null;
        if (context.KW_AT() != null && context.expression() != null)
        {
            // Evaluate the address expression (must be a compile-time constant)
            var constantValues = GetConstantValues();

            var evaluator = new SemanticAnalysis.ConstantExpressionEvaluator(constantValues);
            int? addrValue = evaluator.Visit(context.expression());
            if (addrValue.HasValue)
            {
                address = addrValue.Value;
            }
        }

        var externVar = new IrExternalVariable(name, type, address);
        _module.ExternalVariables.Add(externVar);
    }

    private void RegisterEnum(NovusParser.EnumDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check if this enum is already registered (two-phase registration during imports)
        var existingEnum = _symbols.LookupEnum(name);
        if (existingEnum != null)
        {
            // This is phase 2 - fill in the variants for a placeholder enum
            FillEnumVariants(context, existingEnum);

            // Ensure the enum is in the module (in case it was registered by RegisterEnumStubsForImport)
            if (!_module.Enums.Contains(existingEnum))
            {
                _module.AddEnum(existingEnum);
            }
            return;
        }

        // Phase 1: Register placeholder enum FIRST to allow circular references
        // This is especially important during imports where enums may reference each other

        // Handle generic parameters if present
        var genericParams = ParseGenericParameters(context.genericParams(), registerInSymbolTable: true);

        // Create placeholder enum with empty variants
        var placeholderEnum = new IrEnumType(name, new List<IrEnumVariant>(), genericParams.Count > 0 ? genericParams : null);
        _symbols.RegisterEnum(name, placeholderEnum);
        _module.AddEnum(placeholderEnum);

        // Phase 2: Now parse and fill in the variants (can now reference other enums including this one)
        FillEnumVariants(context, placeholderEnum);

        // Clear generic parameters after enum registration
        _symbols.ClearGenericParameters();
    }

    private void FillEnumVariants(NovusParser.EnumDeclarationContext context, IrEnumType enumType)
    {
        // If variants are already filled (non-empty), skip
        if (enumType.Variants.Count > 0)
        {
            return;
        }

        var name = context.IDENTIFIER().GetText();

        // Handle generic parameters if present (need them in scope for variant type parsing)
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }

        // Parse enum variants
        var variants = new List<IrEnumVariant>();
        int tag = 0;

        foreach (var variantCtx in context.enumVariant())
        {
            var variantName = variantCtx.IDENTIFIER().GetText();
            var associatedData = new List<IrType>();

            if (variantCtx.typeList() != null)
            {
                foreach (var typeCtx in variantCtx.typeList().type())
                {
                    var dataType = ParseType(typeCtx);
                    associatedData.Add(dataType);
                }
            }

            variants.Add(new IrEnumVariant(variantName, tag++, associatedData));
        }

        // Parse where clause and update enum
        var whereClause = ParseWhereClause(context.whereClause());
        enumType.WhereClause = whereClause;

        // Fill in the variants
        enumType.Variants.Clear();
        foreach (var variant in variants)
        {
            enumType.Variants.Add(variant);
        }

        // Force size calculation for non-generic enums
        if (genericParams.Count == 0)
        {
            _ = enumType.SizeInBytes;
        }

        // Clear generic parameters after variant parsing
        _symbols.ClearGenericParameters();
    }

    private void RegisterStruct(NovusParser.StructDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Check if this struct is already registered as a stub (from Pass 2a.5)
        var existingStruct = _symbols.LookupStruct(name);

        // Parse attributes (for @library and other struct attributes)
        var attributes = ParseAttributesSimple(context.attribute());

        // Handle generic parameters if present
        var genericParams = new List<string>();
        if (context.genericParams() != null)
        {
            foreach (var paramId in context.genericParams().IDENTIFIER())
            {
                var paramName = paramId.GetText();
                genericParams.Add(paramName);

                // Add to generic param scope for field parsing
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }

        // Register placeholder struct FIRST to allow self-referential types
        // (but only if not already registered as a stub in Pass 2a.5)
        if (existingStruct == null)
        {
            var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams, null, attributes);
            _symbols.RegisterStruct(name, placeholderStruct);
        }

        // Now parse struct fields (can now reference the struct being defined)
        var fields = new List<IrStructField>();
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            fields.Add(new IrStructField(fieldName, fieldType));
        }

        // Parse where clause
        var whereClause = ParseWhereClause(context.whereClause());

        // Clear generic params from scope after struct registration
        _symbols.ClearGenericParameters();

        // Replace placeholder with complete struct type
        var structType = new IrStructType(name, fields, genericParams, null, attributes, whereClause);

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        // Generic structs will be monomorphized later when instantiated with concrete types
        if (genericParams.Count == 0)
        {
            _ = structType.SizeInBytes;
        }

        // Add all structs to the module (both generic and non-generic) - but only if not already added
        if (existingStruct == null || !_module.Structs.Contains(structType))
        {
            _module.Structs.Add(structType);
        }
        _symbols.RegisterStruct(name, structType);
    }

    private void RegisterTrait(NovusParser.TraitDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Parse attributes
        var attributes = ParseAttributesSimple(context.attribute());

        // Handle generic parameters if present
        var genericParams = ParseGenericParameters(context.genericParams(), registerInSymbolTable: true);

        // Parse trait method signatures
        var methods = new List<IrTraitMethod>();
        foreach (var itemCtx in context.traitItem())
        {
            var funcSig = itemCtx.functionSignature();
            if (funcSig != null)
            {
                var methodName = funcSig.IDENTIFIER().GetText();

                // Parse method generic parameters (if any)
                var methodGenericParams = new List<string>();
                if (funcSig.genericParams() != null)
                {
                    foreach (var paramId in funcSig.genericParams().IDENTIFIER())
                    {
                        var paramName = paramId.GetText();
                        methodGenericParams.Add(paramName);
                        _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                    }
                }

                // Parse parameters
                var parameters = new List<IrParameter>();
                if (funcSig.parameterList() != null)
                {
                    var paramList = funcSig.parameterList();

                    // Handle self parameter
                    if (paramList.selfParameter() != null)
                    {
                        var selfParam = paramList.selfParameter();
                        var selfText = selfParam.GetText();

                        // Create placeholder self type (will be replaced during trait impl)
                        IrType selfType;
                        if (selfText.StartsWith("&mut"))
                        {
                            selfType = new IrMutReferenceType(IrVoidType.Instance); // Placeholder
                        }
                        else if (selfText.StartsWith("&"))
                        {
                            selfType = new IrReferenceType(IrVoidType.Instance); // Placeholder
                        }
                        else
                        {
                            selfType = IrVoidType.Instance; // Placeholder
                        }

                        parameters.Add(new IrParameter("self", selfType));
                    }

                    // Regular parameters
                    ParseRegularParameters(paramList, parameters);

                    // Variadic parameters
                    ParseVariadicParameter(paramList, parameters);
                }

                // Parse return type
                var returnType = ParseReturnType(funcSig.type());

                methods.Add(new IrTraitMethod(methodName, parameters, returnType, methodGenericParams.Count > 0 ? methodGenericParams : null));

                // Clear method-level generic params
                // Note: For traits, we don't need to clear individual params, just the whole set
                // TODO: Revisit if we need more granular control
            }
        }

        // Parse visibility
        var visibility = Visibility.Private;
        for (int i = 0; i < Math.Min(3, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            if (childText == "pub") visibility = Visibility.Public;
            if (childText == "internal") visibility = Visibility.Internal;
        }

        var trait = new IrTrait(name, methods, genericParams.Count > 0 ? genericParams : null, visibility, attributes);
        _symbols.RegisterTrait(name, trait);
        _module.AddTrait(trait);

        // Clear generic parameters after trait registration
        _symbols.ClearGenericParameters();
    }

    /// <summary>
    /// Simple attribute parser for IrBuilder (doesn't validate - just extracts)
    /// </summary>
    private Novus.SemanticAnalysis.AttributeCollection ParseAttributesSimple(NovusParser.AttributeContext[]? attributeContexts)
    {
        var collection = new Novus.SemanticAnalysis.AttributeCollection();
        if (attributeContexts == null || attributeContexts.Length == 0)
            return collection;

        foreach (var attrCtx in attributeContexts)
        {
            var attrName = attrCtx.IDENTIFIER().GetText();
            // Simple location - just use line/column from token
            var errorLocation = new Novus.Diagnostics.SourceLocation(_inputFilePath, attrCtx.Start.Line, attrCtx.Start.Column, 0, "");
            var attr = new Novus.SemanticAnalysis.AttributeInfo(attrName, errorLocation);

            // Parse attribute arguments if present
            if (attrCtx.attributeArgList() != null)
            {
                foreach (var argCtx in attrCtx.attributeArgList().attributeArg())
                {
                    var expr = argCtx.expression();
                    var exprText = expr.GetText();

                    // Simple value extraction
                    object? value = null;
                    if (int.TryParse(exprText, out var intValue))
                    {
                        value = intValue;
                    }
                    else if (exprText.StartsWith("\"") && exprText.EndsWith("\""))
                    {
                        value = exprText.Trim('"');
                    }
                    else if (exprText == "true")
                    {
                        value = true;
                    }
                    else if (exprText == "false")
                    {
                        value = false;
                    }
                    else
                    {
                        value = exprText;
                    }

                    // Check if it's a named argument
                    if (argCtx.IDENTIFIER() != null)
                    {
                        var argName = argCtx.IDENTIFIER().GetText();
                        attr.NamedArgs[argName] = value;
                    }
                    else
                    {
                        // Positional argument
                        attr.PositionalArgs.Add(value);
                    }
                }
            }

            collection.Add(attr);
        }

        return collection;
    }

}
