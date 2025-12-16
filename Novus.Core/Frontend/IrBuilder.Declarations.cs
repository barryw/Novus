using Novus.Diagnostics;
using Novus.Frontend.Generics;
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
        // Parse where clause for constraint checking during monomorphization
        var whereClause = AstParsingHelpers.ParseWhereClause(funcDecl.whereClause());
        var template = new Generics.GenericTemplate(genericParams, funcDecl, templateConstants, whereClause);
        _genericInstantiator.RegisterMethodTemplate(templateKey, template);
    }

    /// <summary>
    /// Generate a mangled name for a method.
    /// Delegates to the centralized InstantiationKeyBuilder utility.
    /// </summary>
    private string GenerateMethodMangledName(string typeName, string methodName, bool isTraitImpl, string? traitName, List<IrType> traitTypeArgs)
    {
        return InstantiationKeyBuilder.BuildMethodMangledName(typeName, methodName, isTraitImpl, traitName, traitTypeArgs);
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Looks up the implementing type by name from the symbol table.
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, string typeName)
    {
        if (selfParam == null) return;

        // Resolve type name to IrType
        IrType? implType = _symbols.LookupStruct(typeName)
                        ?? _symbols.LookupEnum(typeName)
                        ?? MapPrimitiveTypeName(typeName);

        if (implType == null)
        {
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                $"Type '{typeName}' not found for impl block",
                GetLocation(selfParam)
            );
            return;
        }

        // Delegate to the IrType overload
        ParseSelfParameter(selfParam, function, implType);
    }

    /// <summary>
    /// Parse self parameter and add it to the function.
    /// Uses the provided implementing type directly (useful for monomorphized types).
    /// </summary>
    private void ParseSelfParameter(NovusParser.SelfParameterContext? selfParam, IrFunction function, IrType implementingType)
    {
        if (selfParam == null) return;

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

        // Parse attributes first - may affect how we process this static
        // Also filters out module-level attributes (stack_size, cpu) and applies them to the module
        var attributes = ProcessAndFilterModuleAttributes(context.attribute());

        // Check for audio-related attributes
        var audioAttr = attributes.Get(SemanticAnalysis.KnownAttributes.Audio);
        var audioRawAttr = attributes.Get(SemanticAnalysis.KnownAttributes.AudioRaw);
        var modAttr = attributes.Get(SemanticAnalysis.KnownAttributes.Mod);
        var chipRamAttr = attributes.Get(SemanticAnalysis.KnownAttributes.ChipRam);

        // Handle @audio attribute - compile-time audio conversion
        if (audioAttr != null)
        {
            RegisterStaticAudio(name, audioAttr, context);
            return;
        }

        // Handle @audio_raw attribute - raw PCM include
        if (audioRawAttr != null)
        {
            RegisterStaticAudioRaw(name, audioRawAttr, context);
            return;
        }

        // Handle @mod attribute - MOD file include
        if (modAttr != null)
        {
            RegisterStaticMod(name, modAttr, context);
            return;
        }

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
            // Handle type - either explicit or inferred from initial value
            IrType type;
            if (context.type() != null)
            {
                // Explicit type annotation provided
                type = ParseType(context.type());
            }
            else
            {
                // Infer type from the initial value
                type = initialValue.Type;
            }

            // Check for @chip_ram attribute to force memory section
            var section = chipRamAttr != null ? MemorySection.Chip : MemorySection.Default;

            var staticVar = new IrStaticVariable(name, type, visibility, isMutable, initialValue, section);
            _module.StaticVariables.Add(staticVar);
        }
    }

    /// <summary>
    /// Register a static audio sample with compile-time conversion.
    /// Handles @audio("file.wav", sample_rate: 11025, normalize: true) attribute.
    /// </summary>
    private void RegisterStaticAudio(string name, SemanticAnalysis.AttributeInfo audioAttr, NovusParser.StaticDeclarationContext context)
    {
        var (visibility, _, _) = AstModifierHelper.ParseModifiers(context, 5);

        // Get the file path from the first positional argument
        if (audioAttr.PositionalArgs.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@audio attribute requires a file path as the first argument",
                errorLocation
            );
            return;
        }

        var filePath = audioAttr.PositionalArgs[0]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@audio attribute requires a non-empty file path",
                errorLocation
            );
            return;
        }

        // Resolve path relative to input file
        var resolvedPath = ResolveAssetPath(filePath);
        if (!System.IO.File.Exists(resolvedPath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileNotFound,
                $"Audio file not found: {resolvedPath}",
                errorLocation
            );
            return;
        }

        // Build conversion options from attribute arguments
        var options = new Audio.AudioConverter.ConversionOptions();

        if (audioAttr.GetInt("sample_rate") is int targetRate)
        {
            options.TargetSampleRate = targetRate;
        }

        if (audioAttr.GetBool("normalize") is bool normalize)
        {
            options.Normalize = normalize;
        }

        if (audioAttr.GetBool("trim_silence") is bool trimSilence)
        {
            options.TrimSilence = trimSilence;
        }

        if (audioAttr.GetString("channel") is string channel)
        {
            options.ChannelMode = channel;
        }

        // Perform the audio conversion
        Audio.AudioConverter.ConversionResult? result;
        try
        {
            result = Audio.AudioConverter.ConvertFile(resolvedPath, options);
        }
        catch (Exception ex)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileReadError,
                $"Failed to convert audio file '{resolvedPath}': {ex.Message}",
                errorLocation
            );
            return;
        }

        if (result == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileReadError,
                $"Audio conversion returned no data for '{resolvedPath}'",
                errorLocation
            );
            return;
        }

        // Check for chip = false parameter (defaults to true for direct chip RAM placement)
        // When chip = false, sample data goes to fast RAM and must be copied to chip RAM at runtime
        var useChipRam = true;
        if (audioAttr.NamedArgs.TryGetValue("chip", out var chipArg) && chipArg is bool chipBool)
        {
            useChipRam = chipBool;
        }

        var section = useChipRam ? MemorySection.Chip : MemorySection.Default;

        // Create static byte array for sample data (chip or fast RAM)
        var dataName = useChipRam ? $"{name}_data" : $"{name}_data";
        CreateStaticAudioData(dataName, result.Data, visibility, section);

        if (useChipRam)
        {
            // Create the AudioSample struct with metadata (data in chip RAM)
            CreateStaticAudioSample(name, dataName, result, visibility);
        }
        else
        {
            // Create an AudioAsset struct for automatic chip RAM management
            CreateStaticAudioAsset(name, dataName, result, visibility);
        }
    }

    /// <summary>
    /// Register a static audio sample from raw PCM data.
    /// Handles @audio_raw("file.raw", sample_rate: 8000) attribute.
    /// </summary>
    private void RegisterStaticAudioRaw(string name, SemanticAnalysis.AttributeInfo audioRawAttr, NovusParser.StaticDeclarationContext context)
    {
        var (visibility, _, _) = AstModifierHelper.ParseModifiers(context, 5);

        // Get the file path from the first positional argument
        if (audioRawAttr.PositionalArgs.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@audio_raw attribute requires a file path as the first argument",
                errorLocation
            );
            return;
        }

        var filePath = audioRawAttr.PositionalArgs[0]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@audio_raw attribute requires a non-empty file path",
                errorLocation
            );
            return;
        }

        // Resolve path relative to input file
        var resolvedPath = ResolveAssetPath(filePath);
        if (!System.IO.File.Exists(resolvedPath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileNotFound,
                $"Raw audio file not found: {resolvedPath}",
                errorLocation
            );
            return;
        }

        // Read raw PCM data
        byte[] data;
        try
        {
            data = System.IO.File.ReadAllBytes(resolvedPath);
        }
        catch (Exception ex)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileReadError,
                $"Failed to read raw audio file '{resolvedPath}': {ex.Message}",
                errorLocation
            );
            return;
        }

        // Get sample rate from attribute (required for raw files)
        var sampleRate = audioRawAttr.GetInt("sample_rate") ?? 8000;

        // Pad to even length
        if (data.Length % 2 != 0)
        {
            var paddedData = new byte[data.Length + 1];
            Array.Copy(data, paddedData, data.Length);
            data = paddedData;
        }

        // Create conversion result with the raw data
        var result = new Audio.AudioConverter.ConversionResult
        {
            Data = data,
            OriginalSampleRate = sampleRate,
            FinalSampleRate = sampleRate
        };

        // Create static byte array for sample data
        var dataName = $"{name}_data";
        CreateStaticAudioData(dataName, result.Data, visibility);

        // Create the AudioSample struct with metadata
        CreateStaticAudioSample(name, dataName, result, visibility);
    }

    /// <summary>
    /// Register a static MOD file include.
    /// Handles @mod("music.mod") attribute.
    /// </summary>
    private void RegisterStaticMod(string name, SemanticAnalysis.AttributeInfo modAttr, NovusParser.StaticDeclarationContext context)
    {
        var (visibility, _, _) = AstModifierHelper.ParseModifiers(context, 5);

        // Get the file path from the first positional argument
        if (modAttr.PositionalArgs.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@mod attribute requires a file path as the first argument",
                errorLocation
            );
            return;
        }

        var filePath = modAttr.PositionalArgs[0]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidAttribute,
                "@mod attribute requires a non-empty file path",
                errorLocation
            );
            return;
        }

        // Resolve path relative to input file
        var resolvedPath = ResolveAssetPath(filePath);
        if (!System.IO.File.Exists(resolvedPath))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileNotFound,
                $"MOD file not found: {resolvedPath}",
                errorLocation
            );
            return;
        }

        // Read MOD file data
        byte[] data;
        try
        {
            data = System.IO.File.ReadAllBytes(resolvedPath);
        }
        catch (Exception ex)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FileReadError,
                $"Failed to read MOD file '{resolvedPath}': {ex.Message}",
                errorLocation
            );
            return;
        }

        // Pad to even length if needed
        if (data.Length % 2 != 0)
        {
            var paddedData = new byte[data.Length + 1];
            Array.Copy(data, paddedData, data.Length);
            data = paddedData;
        }

        // Check for chip = false parameter (defaults to true for backwards compatibility)
        // When chip = false, asset goes to fast RAM and must use ChipCache for DMA
        var useChipRam = true;
        if (modAttr.NamedArgs.TryGetValue("chip", out var chipArg) && chipArg is bool chipBool)
        {
            useChipRam = chipBool;
        }

        var section = useChipRam ? MemorySection.Chip : MemorySection.Default;

        // Create static byte array for MOD data (internal, with _data suffix when chip=false)
        var dataName = useChipRam ? name : name + "_data";
        var arrayType = new IrArrayType(IrIntType.U8, data.Length);
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var b in data)
        {
            arrayLiteral.Elements.Add(new IrConstant(b, IrIntType.U8));
        }

        var staticVar = new IrStaticVariable(dataName, arrayType, visibility, false, arrayLiteral, section);
        _module.StaticVariables.Add(staticVar);

        // When chip=false, also create a ModAsset struct with pointer and size
        // This allows automatic chip RAM management via init_asset()
        if (!useChipRam)
        {
            CreateStaticModAsset(name, dataName, data.Length, visibility);
        }
    }

    /// <summary>
    /// Create a static ModAsset struct that references MOD data.
    /// </summary>
    private void CreateStaticModAsset(string name, string dataName, int dataLength, Visibility visibility)
    {
        // ModAsset struct layout:
        // - data: *u8 (pointer to the data array)
        // - size: u32 (size in bytes)

        // Try to look up the real ModAsset struct from imports
        var modAssetStruct = _symbols.LookupStruct("ModAsset");
        if (modAssetStruct == null)
        {
            // ModAsset not imported - create an anonymous struct type with the same layout
            var fields = new List<IrStructField>
            {
                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                new IrStructField("size", IrIntType.U32)
            };
            modAssetStruct = new IrStructType("ModAsset", fields);
        }
        // Always add to module structs so code generator can emit the typedef
        // The code generator will check if it's already defined before emitting
        if (!_module.Structs.Any(s => s.StructName == modAssetStruct.StructName))
        {
            _module.AddStruct(modAssetStruct);
        }

        // Build field values dictionary
        // data: pointer to the static array we created
        // Use IrGlobalVariable to reference the static data array
        var dataArrayType = new IrArrayType(IrIntType.U8, dataLength);
        var dataArrayRef = new IrGlobalVariable(dataName, dataArrayType);
        var dataPtr = new IrCastValue(dataArrayRef, dataArrayType, new IrPointerType(IrIntType.U8));

        var fieldValues = new Dictionary<string, IrValue>
        {
            { "data", dataPtr },
            { "size", new IrConstant((uint)dataLength, IrIntType.U32) }
        };

        // Create the struct literal with field values
        var structLiteral = new IrStructLiteral(modAssetStruct, fieldValues);

        var assetVar = new IrStaticVariable(name, modAssetStruct, visibility, false, structLiteral, MemorySection.Default);
        _module.StaticVariables.Add(assetVar);
    }

    /// <summary>
    /// Create a static byte array for audio sample data.
    /// </summary>
    /// <param name="name">Name of the static variable</param>
    /// <param name="data">Binary audio data</param>
    /// <param name="visibility">Visibility modifier</param>
    /// <param name="section">Memory section (Chip for DMA access, Default for fast RAM)</param>
    private void CreateStaticAudioData(string name, byte[] data, Visibility visibility, MemorySection section = MemorySection.Chip)
    {
        var arrayType = new IrArrayType(IrIntType.U8, data.Length);
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var b in data)
        {
            arrayLiteral.Elements.Add(new IrConstant(b, IrIntType.U8));
        }

        var staticVar = new IrStaticVariable(name, arrayType, visibility, false, arrayLiteral, section);
        _module.StaticVariables.Add(staticVar);
    }

    /// <summary>
    /// Create a static AudioSample struct with metadata about the converted audio.
    /// </summary>
    private void CreateStaticAudioSample(string name, string dataName, Audio.AudioConverter.ConversionResult result, Visibility visibility)
    {
        // Look up or create AudioSample struct type
        // The struct should match std::audio::paula::SampleHandle layout:
        // - data: *u8 (pointer to sample data)
        // - length_bytes: u32
        // - length_words: u16
        // - sample_rate: u32
        // - period_pal: u16
        // - period_ntsc: u16

        // For now, create a simple struct value that the code generator can handle
        // We'll generate a struct literal that references the data array

        // Create a struct literal value
        var sampleStruct = _symbols.LookupStruct("AudioSample");
        if (sampleStruct == null)
        {
            // AudioSample struct not imported - create a compatible anonymous struct type
            // This allows the audio attribute to work even without importing std::audio
            var fields = new List<IrStructField>
            {
                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                new IrStructField("length_bytes", IrIntType.U32),
                new IrStructField("length_words", IrIntType.U16),
                new IrStructField("sample_rate", IrIntType.U32),
                new IrStructField("period_pal", IrIntType.U16),
                new IrStructField("period_ntsc", IrIntType.U16)
            };
            sampleStruct = new IrStructType($"__AudioSample_{name}", fields);
            _module.AddStruct(sampleStruct);
        }

        // Build field values dictionary
        // data: pointer to the static array we created
        // Use IrGlobalVariable to reference the static data array
        var dataArrayType = new IrArrayType(IrIntType.U8, result.Data.Length);
        var dataArrayRef = new IrGlobalVariable(dataName, dataArrayType);
        var dataPtr = new IrCastValue(dataArrayRef, dataArrayType, new IrPointerType(IrIntType.U8));

        var fieldValues = new Dictionary<string, IrValue>
        {
            { "data", dataPtr },
            { "length_bytes", new IrConstant(result.Data.Length, IrIntType.U32) },
            { "length_words", new IrConstant((result.Data.Length + 1) / 2, IrIntType.U16) },
            { "sample_rate", new IrConstant(result.FinalSampleRate, IrIntType.U32) },
            { "period_pal", new IrConstant(result.PeriodPal, IrIntType.U16) },
            { "period_ntsc", new IrConstant(result.PeriodNtsc, IrIntType.U16) }
        };

        // Create the struct literal with field values
        var structLiteral = new IrStructLiteral(sampleStruct, fieldValues);

        var staticVar = new IrStaticVariable(name, sampleStruct, visibility, false, structLiteral, MemorySection.Default);
        _module.StaticVariables.Add(staticVar);
    }

    /// <summary>
    /// Create a static AudioAsset struct for audio data in fast RAM.
    /// AudioAsset includes all metadata needed for automatic chip RAM management.
    /// Similar to ModAsset but includes sample rate and period information.
    /// </summary>
    private void CreateStaticAudioAsset(string name, string dataName, Audio.AudioConverter.ConversionResult result, Visibility visibility)
    {
        // AudioAsset struct layout (matches std::audio::ptplayer::AudioAsset):
        // - data: *u8 (pointer to the data array in fast RAM)
        // - size: u32 (size in bytes)
        // - sample_rate: u32 (original sample rate)
        // - period_pal: u16 (pre-calculated period for PAL)
        // - period_ntsc: u16 (pre-calculated period for NTSC)

        // Try to look up the real AudioAsset struct from imports
        var audioAssetStruct = _symbols.LookupStruct("AudioAsset");
        if (audioAssetStruct == null)
        {
            // AudioAsset not imported - create an anonymous struct type with the same layout
            var fields = new List<IrStructField>
            {
                new IrStructField("data", new IrPointerType(IrIntType.U8)),
                new IrStructField("size", IrIntType.U32),
                new IrStructField("sample_rate", IrIntType.U32),
                new IrStructField("period_pal", IrIntType.U16),
                new IrStructField("period_ntsc", IrIntType.U16)
            };
            audioAssetStruct = new IrStructType("AudioAsset", fields);
        }
        // Always add to module structs so code generator can emit the typedef
        // The code generator will check if it's already defined before emitting
        if (!_module.Structs.Any(s => s.StructName == audioAssetStruct.StructName))
        {
            _module.AddStruct(audioAssetStruct);
        }

        // Build field values dictionary
        // data: pointer to the static array we created
        // Use IrGlobalVariable to reference the static data array
        var dataArrayType = new IrArrayType(IrIntType.U8, result.Data.Length);
        var dataArrayRef = new IrGlobalVariable(dataName, dataArrayType);
        var dataPtr = new IrCastValue(dataArrayRef, dataArrayType, new IrPointerType(IrIntType.U8));

        var fieldValues = new Dictionary<string, IrValue>
        {
            { "data", dataPtr },
            { "size", new IrConstant((uint)result.Data.Length, IrIntType.U32) },
            { "sample_rate", new IrConstant((uint)result.FinalSampleRate, IrIntType.U32) },
            { "period_pal", new IrConstant((ushort)result.PeriodPal, IrIntType.U16) },
            { "period_ntsc", new IrConstant((ushort)result.PeriodNtsc, IrIntType.U16) }
        };

        // Create the struct literal with field values
        var structLiteral = new IrStructLiteral(audioAssetStruct, fieldValues);

        var assetVar = new IrStaticVariable(name, audioAssetStruct, visibility, false, structLiteral, MemorySection.Default);
        _module.StaticVariables.Add(assetVar);
    }

    /// <summary>
    /// Resolve an asset path relative to the input file being compiled.
    /// </summary>
    private string ResolveAssetPath(string assetPath)
    {
        // If the path is absolute, use it directly
        if (System.IO.Path.IsPathRooted(assetPath))
        {
            return assetPath;
        }

        // Resolve relative to the input file's directory
        if (_inputFilePath != null)
        {
            var inputDir = System.IO.Path.GetDirectoryName(_inputFilePath);
            if (!string.IsNullOrEmpty(inputDir))
            {
                return System.IO.Path.Combine(inputDir, assetPath);
            }
        }

        // Fall back to current directory
        return assetPath;
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
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _symbols, registerInSymbolTable: true);

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
        // Also filters out module-level attributes (stack_size, cpu) and applies them to the module
        var attributes = ProcessAndFilterModuleAttributes(context.attribute());

        // Handle generic parameters if present
        var genericParams = AstParsingHelpers.ParseGenericParameters(context.genericParams(), _symbols, registerInSymbolTable: true);

        // Register placeholder struct FIRST to allow self-referential types
        // (but only if not already registered as a stub in Pass 2a.5)
        if (existingStruct == null)
        {
            var placeholderStruct = new IrStructType(name, new List<IrStructField>(), genericParams, null, attributes);
            _symbols.RegisterStruct(name, placeholderStruct);
            existingStruct = placeholderStruct;
        }

        // Now parse struct fields and add them DIRECTLY to the existing placeholder
        // This is critical because pointer types (*StructName) that were already created
        // hold references to this placeholder instance. If we create a new struct,
        // those pointer types would still reference the empty placeholder and not see the fields.
        foreach (var fieldCtx in context.structField())
        {
            var fieldName = fieldCtx.IDENTIFIER().GetText();
            var fieldType = ParseType(fieldCtx.type());
            existingStruct.Fields.Add(new IrStructField(fieldName, fieldType));
        }

        // Parse where clause and update the existing struct
        var whereClause = ParseWhereClause(context.whereClause());
        if (whereClause != null)
        {
            existingStruct.WhereClause = whereClause;
        }

        // Update attributes on the existing struct if they were parsed
        if (attributes != null && attributes.Count > 0)
        {
            existingStruct.Attributes = attributes;
        }

        // Clear generic params from scope after struct registration
        _symbols.ClearGenericParameters();

        // Force offset calculation by accessing SizeInBytes (only for non-generic structs)
        // Generic structs will be monomorphized later when instantiated with concrete types
        if (genericParams.Count == 0)
        {
            _ = existingStruct.SizeInBytes;

            // Validate packed struct field alignment after offsets are calculated
            ValidatePackedStructAlignment(existingStruct, context);
        }

        // Add struct to the module (if not already added)
        if (!_module.Structs.Contains(existingStruct))
        {
            _module.AddStruct(existingStruct);
        }
    }

    /// <summary>
    /// Validates that packed struct fields won't cause address errors on 68000.
    /// On 68000, accessing a 16-bit or 32-bit value at an odd address causes a bus error.
    /// When structs are marked with #[packed], fields may be placed at misaligned offsets.
    /// </summary>
    private void ValidatePackedStructAlignment(IrStructType structType, NovusParser.StructDeclarationContext context)
    {
        // Only check packed structs
        if (structType.Attributes?.Has("packed") != true)
        {
            return;
        }

        // Check each field for misalignment
        foreach (var field in structType.Fields)
        {
            // For arrays, check the element type, not the array size
            // An array of bytes can start at any offset, but an array of u16/u32 must be word-aligned
            IrType typeToCheck = field.Type;
            if (field.Type is IrArrayType arrayType)
            {
                typeToCheck = arrayType.ElementType;
            }

            var elementSize = typeToCheck.SizeInBytes;

            // Fields larger than 1 byte (i16, i32, u16, u32, pointers, references, structs with alignment > 1)
            // must be aligned to even addresses on 68000
            if (elementSize > 1 && field.Offset % 2 != 0)
            {
                var fieldCtx = context.structField()
                    .FirstOrDefault(f => f.IDENTIFIER().GetText() == field.Name);

                _diagnostics.ReportError(
                    ErrorCodes.PackedStructMisalignedField,
                    $"Packed struct '{structType.StructName}' has misaligned field '{field.Name}' " +
                    $"at offset {field.Offset}. This will cause an address error (bus error) on 68000. " +
                    $"Fields with size > 1 byte must be at even offsets in packed structs.",
                    fieldCtx != null ? GetLocation(fieldCtx) : GetLocation(context)
                );
            }
        }
    }

    private void RegisterTrait(NovusParser.TraitDeclarationContext context)
    {
        var name = context.IDENTIFIER().GetText();

        // Parse attributes
        // Also filters out module-level attributes (stack_size, cpu) and applies them to the module
        var attributes = ProcessAndFilterModuleAttributes(context.attribute());

        // Handle generic parameters if present
        var genericParams = ParseGenericParameters(context.genericParams(), registerInSymbolTable: true);

        // Parse trait method signatures (and optional default implementations)
        var methods = new List<IrTraitMethod>();
        foreach (var itemCtx in context.traitItem())
        {
            // After grammar change, we now use traitMethodDeclaration() instead of functionSignature()
            var methodDecl = itemCtx.traitMethodDeclaration();
            if (methodDecl != null)
            {
                var methodName = methodDecl.IDENTIFIER().GetText();

                // Parse method generic parameters (if any)
                var methodGenericParams = AstParsingHelpers.ParseGenericParameters(methodDecl.genericParams(), _symbols, registerInSymbolTable: true);

                // Parse parameters
                var parameters = new List<IrParameter>();
                if (methodDecl.parameterList() != null)
                {
                    var paramList = methodDecl.parameterList();

                    // Handle self parameter
                    if (paramList.selfParameter() != null)
                    {
                        var selfParam = paramList.selfParameter();
                        var selfText = selfParam.GetText();

                        // Create placeholder self type (will be replaced during trait impl)
                        IrType selfType;
                        if (selfText.StartsWith("&var"))
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
                var returnType = ParseReturnType(methodDecl.type());

                var traitMethod = new IrTraitMethod(methodName, parameters, returnType, methodGenericParams.Count > 0 ? methodGenericParams : null);

                // Check if there's a default implementation (body block)
                if (methodDecl.block() != null)
                {
                    // Store the AST context for the default implementation body
                    // This will be processed later during semantic analysis when needed
                    traitMethod.DefaultBodyContext = methodDecl.block();
                }

                methods.Add(traitMethod);

                // Clear method-level generic params.
                // Trait-level generic params persist; only method-specific ones are cleared.
                // The current design uses ClearGenericParameters at trait boundary.
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
    /// Process module-level attributes found on declarations.
    /// Module attributes (stack_size, cpu) can appear as regular attributes on any declaration.
    /// This method extracts them and applies them to the module.
    /// Returns attributes that are NOT module-level (i.e., should be applied to the declaration).
    /// </summary>
    private Novus.SemanticAnalysis.AttributeCollection ProcessAndFilterModuleAttributes(NovusParser.AttributeContext[]? attributeContexts)
    {
        var collection = new Novus.SemanticAnalysis.AttributeCollection();
        if (attributeContexts == null || attributeContexts.Length == 0)
            return collection;

        foreach (var attrCtx in attributeContexts)
        {
            var attrName = attrCtx.IDENTIFIER().GetText();
            var errorLocation = new Novus.Diagnostics.SourceLocation(_inputFilePath ?? "<unknown>", attrCtx.Start.Line, attrCtx.Start.Column, 0, "");

            if (attrName == Novus.SemanticAnalysis.KnownAttributes.StackSize)
            {
                // Parse #[stack_size(N)] attribute - module-level
                if (attrCtx.attributeArgList() != null)
                {
                    var args = attrCtx.attributeArgList().attributeArg();
                    if (args.Length > 0)
                    {
                        var exprText = args[0].expression().GetText();
                        if (int.TryParse(exprText, out var stackSize))
                        {
                            if (stackSize < 4096)
                            {
                                _diagnostics.ReportError(ErrorCodes.InvalidAttribute, $"Stack size {stackSize} is too small (minimum 4096 bytes)", errorLocation);
                            }
                            else if (stackSize > 16 * 1024 * 1024) // 16MB max
                            {
                                _diagnostics.ReportError(ErrorCodes.InvalidAttribute, $"Stack size {stackSize} is too large (maximum 16MB)", errorLocation);
                            }
                            else
                            {
                                _module.StackSize = stackSize;
                            }
                        }
                        else
                        {
                            _diagnostics.ReportError(ErrorCodes.InvalidAttribute, $"#[stack_size] expects an integer argument, got '{exprText}'", errorLocation);
                        }
                    }
                }
                else
                {
                    _diagnostics.ReportError(ErrorCodes.InvalidAttribute, "#[stack_size] requires a size argument, e.g., #[stack_size(65536)]", errorLocation);
                }
                // Don't add to collection - it's module-level
            }
            else if (attrName == Novus.SemanticAnalysis.KnownAttributes.Cpu)
            {
                // Parse #[cpu("68020")] attribute - module-level
                if (attrCtx.attributeArgList() != null)
                {
                    var args = attrCtx.attributeArgList().attributeArg();
                    if (args.Length > 0)
                    {
                        var exprText = args[0].expression().GetText();
                        // Remove quotes if present
                        var cpuValue = exprText.Trim('"', '\'');

                        var validCpus = new HashSet<string> { "68000", "68010", "68020", "68030", "68040", "68060", "68080" };
                        if (validCpus.Contains(cpuValue))
                        {
                            _module.CpuOverride = cpuValue;
                            // Emit warning that this overrides project/CLI settings
                            _diagnostics.ReportWarning(ErrorCodes.AttributeOverride, $"#[cpu(\"{cpuValue}\")] overrides project and command-line CPU settings", errorLocation);
                        }
                        else
                        {
                            _diagnostics.ReportError(ErrorCodes.InvalidAttribute, $"Invalid CPU target '{cpuValue}'. Valid values are: 68000, 68010, 68020, 68030, 68040, 68060, 68080", errorLocation);
                        }
                    }
                    else
                    {
                        _diagnostics.ReportError(ErrorCodes.InvalidAttribute, "#[cpu] requires a CPU target argument, e.g., #[cpu(\"68020\")]", errorLocation);
                    }
                }
                else
                {
                    _diagnostics.ReportError(ErrorCodes.InvalidAttribute, "#[cpu] requires a CPU target argument, e.g., #[cpu(\"68020\")]", errorLocation);
                }
                // Don't add to collection - it's module-level
            }
            else
            {
                // Regular declaration attribute - add to collection
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
                            // Keep as string (e.g., identifiers like Eq, Hash)
                            value = exprText;
                        }

                        // Check if this is a named argument
                        if (argCtx.IDENTIFIER() != null)
                        {
                            var argName = argCtx.IDENTIFIER().GetText();
                            attr.NamedArgs[argName] = value;
                        }
                        else
                        {
                            attr.PositionalArgs.Add(value);
                        }
                    }
                }

                collection.Add(attr);
            }
        }

        return collection;
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
            var errorLocation = new Novus.Diagnostics.SourceLocation(_inputFilePath ?? "<unknown>", attrCtx.Start.Line, attrCtx.Start.Column, 0, "");
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
