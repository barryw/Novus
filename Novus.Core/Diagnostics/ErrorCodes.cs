namespace Novus.Diagnostics;

/// <summary>
/// Centralized error codes for Novus compiler diagnostics.
/// Organized by category for clarity and maintainability.
/// </summary>
public static class ErrorCodes
{
    // ========================================================================
    // E20XX: Symbol Resolution Errors (types, variables, functions not found)
    // ========================================================================
    public const string TypeNotFound = "E2001";
    public const string VariableNotFound = "E2002";
    public const string FunctionNotFound = "E2003";
    public const string FieldNotFound = "E2004";
    public const string MethodNotFound = "E2005";
    public const string ModuleNotFound = "E2006";
    public const string StructNotFound = "E2007";
    public const string EnumNotFound = "E2008";
    public const string EnumVariantNotFound = "E2009";
    public const string TraitNotFound = "E2010";
    public const string GenericParameterNotFound = "E2011";

    // ========================================================================
    // E21XX: Type Errors (wrong types, type mismatches)
    // ========================================================================
    public const string CannotIndexType = "E2100";
    public const string CannotDereferenceType = "E2101";
    public const string CannotCallMethodOnType = "E2102";
    public const string CannotAccessMember = "E2103";
    public const string TypeMismatch = "E2104";
    public const string WrongNumberOfTypeArguments = "E2105";
    public const string WrongNumberOfArguments = "E2106";
    public const string InvalidOperatorForType = "E2107";
    public const string UnsupportedBitWidth = "E2108";

    // ========================================================================
    // E22XX: Trait/Interface Errors
    // ========================================================================
    public const string TraitNotImplemented = "E2200";
    public const string MissingTraitMethod = "E2201";
    public const string DisplayNotImplemented = "E2202";
    public const string IterableNotImplemented = "E2203";
    public const string FromNotImplemented = "E2204";

    // ========================================================================
    // E23XX: Control Flow Errors
    // ========================================================================
    public const string BreakOutsideLoop = "E2300";
    public const string ReturnTypeMismatch = "E2301";
    public const string MissingReturnValue = "E2302";
    public const string SelfOutsideImpl = "E2303";

    // ========================================================================
    // E24XX: Match Expression Errors
    // ========================================================================
    public const string MatchOnInvalidType = "E2400";
    public const string MissingMatchValue = "E2401";
    public const string WrongNumberOfVariantArgs = "E2402";

    // ========================================================================
    // E25XX: Variable/Assignment Errors
    // ========================================================================
    public const string MissingInitializer = "E2500";
    public const string MissingAssignmentValue = "E2501";
    public const string CannotAssignToExpression = "E2502";
    public const string UndefinedVariable = "E2503";

    // ========================================================================
    // E26XX: Literal/Constant Errors
    // ========================================================================
    public const string EmptyArrayLiteral = "E2600";
    public const string InvalidArrayRepeatCount = "E2601";
    public const string UnknownEscapeSequence = "E2602";
    public const string UnmatchedBracesInFString = "E2603";

    // ========================================================================
    // E27XX: Struct/Enum Errors
    // ========================================================================
    public const string MissingStructField = "E2700";
    public const string UninitializedField = "E2701";
    public const string UnknownStructField = "E2702";

    // ========================================================================
    // E28XX: Function/Method Call Errors
    // ========================================================================
    public const string VariadicArgumentCount = "E2800";
    public const string MethodRequiresSelf = "E2801";
    public const string NullMethodReceiver = "E2802";
    public const string InvalidFunctionCallTarget = "E2803";

    // ========================================================================
    // E29XX: Operator Errors
    // ========================================================================
    public const string TryOperatorInvalidContext = "E2900";
    public const string TryOperatorInvalidType = "E2901";
    public const string TryOperatorReturnMismatch = "E2902";
    public const string UnknownOperator = "E2903";
    public const string PreIncrementUnsupported = "E2904";

    // ========================================================================
    // E30XX: Module/Import Errors
    // ========================================================================
    public const string CannotImportPrivate = "E3000";
    public const string ModuleNotFoundAtPath = "E3001";

    // ========================================================================
    // E31XX: Generic/Monomorphization Errors
    // ========================================================================
    public const string CannotInferTypeArguments = "E3100";
    public const string FailedToInstantiateGeneric = "E3101";
    public const string GenericTypeParamNotInferred = "E3102";

    // ========================================================================
    // E32XX: Standard Library Errors
    // ========================================================================
    public const string StrTypeNotFound = "E3200";
    public const string StrMissingPtrField = "E3201";
    public const string StrMissingAsPtrMethod = "E3202";
    public const string FormatterTypeNotFound = "E3203";
    public const string FormatterMissingMethod = "E3204";
    public const string OptionTypeIncorrect = "E3205";
    public const string ResultTypeIncorrect = "E3206";

    // ========================================================================
    // E33XX: Expression Errors
    // ========================================================================
    public const string MissingExpression = "E3300";
    public const string InvalidExpressionType = "E3301";
    public const string IfLetInvalidType = "E3302";
    public const string PathMustReferenceType = "E3303";
    public const string TurboFishMustReferenceType = "E3304";

    // ========================================================================
    // E34XX: Miscellaneous Errors
    // ========================================================================
    public const string DropMethodNotFound = "E3400";
    public const string CannotGetMangledName = "E3401";
    public const string CannotGenerateDropCall = "E3402";
    public const string SizeofTypeUnknown = "E3403";
    public const string StructArrayInitializerInvalid = "E3404";
}
