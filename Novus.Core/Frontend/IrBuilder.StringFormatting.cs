using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing string formatting and interpolation methods.
/// This file contains methods for processing:
/// - Interpolated strings (f"Hello {name}!")
/// - String literals with escape sequences
/// - Format helpers for different types (integer, bool, string)
/// - StackFormatter integration for zero-allocation string building
/// </summary>
public partial class IrBuilder
{
    /// <summary>
    /// Visit an interpolated string literal: f"Hello {name}!"
    /// Generates IR that uses StackFormatter for zero-allocation string building.
    /// </summary>
    public override object? VisitInterpolatedStringLiteral([NotNull] NovusParser.InterpolatedStringLiteralContext context)
    {
        // Get the f-string text and parse it into segments
        var fstring = context.GetChild(0)?.GetText();
        if (fstring == null)
        {
            return null;
        }

        // Strip prefix and suffix based on whether it's f"..." or just "..."
        var content = fstring.StartsWith("f\"")
            ? fstring.Substring(2, fstring.Length - 3)  // f"..." -> ...
            : fstring.Substring(1, fstring.Length - 2);  // "..." -> ...

        return HandleInterpolatedStringImpl(content, context);
    }

    /// <summary>
    /// Implementation for handling interpolated strings.
    /// Creates a StackFormatter, writes segments to it, and returns the final Str.
    /// </summary>
    private object? HandleInterpolatedStringImpl(string content, ParserRuleContext context)
    {
        // Auto-import required modules for f-string support
        // F-strings need:
        // - std::strings::display for Display trait implementations on primitive types
        // - std::strings::format for StackFormatter type (stack-allocated, zero heap allocation!)
        // - std::strings::core for Str type
        bool isStdLibraryModule = _inputFilePath != null && _inputFilePath.Contains(System.IO.Path.DirectorySeparatorChar + "std" + System.IO.Path.DirectorySeparatorChar);

        if (!isStdLibraryModule)
        {
            // Always import these modules to ensure all necessary types and methods are available
            // The ImportModule function handles already-processed modules correctly
            ImportModule("std::strings::display", importAll: true);
            ImportModule("std::strings::format", importAll: true);
            ImportModule("std::strings::core", importAll: true);
        }

        // Parse the string into string and expression segments
        var segments = ParseInterpolatedString(content);

        // Look up the StackFormatter type (stack-allocated, no heap allocation!)
        var formatterType = _symbols.LookupStruct("StackFormatter");
        if (formatterType == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Interpolated strings require StackFormatter type from std::fmt module",
                errorLocation
            );
            return null;
        }

        // Create a temporary variable for the StackFormatter
        var formatterVarName = $"_formatter{_tempCounter++}";

        // Call StackFormatter::new() to create the formatter
        // This returns StackFormatter directly (no Option wrapping - always succeeds!)
        var formatterNewMethodName = "StackFormatter::new";
        var formatterNewMethod = _module.GetFunction(formatterNewMethodName);
        if (formatterNewMethod == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "StackFormatter::new() method not found. Ensure std::fmt is imported.",
                errorLocation
            );
            return null;
        }

        // Call StackFormatter::new() - returns StackFormatter directly
        var formatterNewResultName = $"%t{_tempCounter++}";
        var formatterNewCall = new IrCall(formatterNewMethodName, formatterNewMethod.ReturnType, formatterNewResultName);
        formatterNewCall.Location = GetLocation(context);
        _currentBlock!.AddInstruction(formatterNewCall);
        var formatterNewResult = new IrVariable(formatterNewResultName, formatterNewMethod.ReturnType);

        // Store formatter in a local variable (mutable)
        var formatterVar = new IrLocalVariable(formatterVarName, formatterType, true);
        _currentFunction!.LocalVariables.Add(formatterVar);
        _localVariables[formatterVarName] = formatterVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(formatterVarName, formatterType, true, formatterNewResult));

        // Process each segment
        foreach (var segment in segments)
        {
            if (segment.IsStringSegment)
            {
                // Call f.write_str("segment")
                EmitWriteStrLiteral(formatterVarName, formatterType, segment.StringContent);
            }
            else
            {
                // Parse the expression and call expr.fmt(&mut f)
                EmitFormatExpression(formatterVarName, formatterType, segment.Expression);
            }
        }

        // Call f.as_str() to get a Str (pointer into the stack buffer)
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var asStrMethodName = _module.FindTraitMethod("StackFormatter", "as_str");
        if (asStrMethodName == null)
        {
            asStrMethodName = "StackFormatter::as_str";
        }

        var asStrMethod = _module.GetFunction(asStrMethodName);
        if (asStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "StackFormatter::as_str() method not found",
                errorLocation
            );
            return null;
        }

        // Call as_str() - takes &self (borrow)
        var asStrResultName = $"%t{_tempCounter++}";
        var asStrCall = new IrCall(asStrMethodName, asStrMethod.ReturnType, asStrResultName);
        asStrCall.Location = GetLocation(context);
        // as_str takes &self, so we need to borrow the formatter
        var formatterBorrow = new IrBorrowValue(formatterVarRef, asStrMethod.Parameters[0].Type, false);
        asStrCall.Arguments.Add(formatterBorrow);
        _currentBlock!.AddInstruction(asStrCall);

        // Return the Str result (no heap allocation, just a pointer into the stack buffer!)
        return new IrVariable(asStrResultName, asStrMethod.ReturnType);
    }

    /// <summary>
    /// Emit a write_str call for a string literal segment.
    /// </summary>
    private void EmitWriteStrLiteral(string formatterVarName, IrType formatterType, string stringContent)
    {
        // Create a string literal for the segment
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringContent, label);
        StringLiterals.Add(stringLiteral);

        // Create Str struct
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var strFieldValues = new Dictionary<string, IrValue>
        {
            ["ptr"] = stringLiteral,
            ["len"] = new IrConstant(stringContent.Length, IrIntType.U32)
        };
        var strValue = new IrStructLiteral(strType, strFieldValues);

        // Call the other EmitWriteStr method with the Str value
        EmitWriteStr(formatterVarName, formatterType, strValue);
    }

    /// <summary>
    /// Emit code to format an expression and write it to the formatter.
    /// Handles different types appropriately (Str, String, integers, bools, custom types).
    /// </summary>
    private void EmitFormatExpression(string formatterVarName, IrType formatterType, string expressionText)
    {
        // Parse the expression text into an AST node
        var inputStream = new AntlrInputStream(expressionText);
        var lexer = new NovusLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokens);

        // Parse as an expression
        var exprContext = parser.expression();

        // Visit the expression to generate IR
        var exprValue = Visit(exprContext) as IrValue;
        if (exprValue == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Failed to evaluate expression in f-string: {expressionText}",
                errorLocation
            );
            return;
        }

        // Get the type of the expression
        var exprType = exprValue.Type;

        // Handle different types appropriately
        if (exprType is IrStructType st && st.StructName == "Str")
        {
            // For Str types, just write the string directly
            EmitWriteStr(formatterVarName, formatterType, exprValue);
        }
        else if (exprType is IrStructType strSt && strSt.StructName == "String")
        {
            // For String types, call String::as_str() and write the result
            // This bypasses the Display trait which expects Formatter, not StackFormatter
            EmitFormatString(formatterVarName, formatterType, exprValue);
        }
        else if (exprType is IrIntType intType)
        {
            // For integer types, convert to string using built-in functions
            EmitFormatInteger(formatterVarName, formatterType, exprValue, intType);
        }
        else if (exprType is IrBoolType)
        {
            // For bool, write "true" or "false"
            EmitFormatBool(formatterVarName, formatterType, exprValue);
        }
        else
        {
            // For other types, try to find Display::fmt() implementation
            var typeName = exprType switch
            {
                IrStructType structType => structType.StructName,
                IrEnumType enumType => enumType.EnumName,
                _ => exprType.Name
            };
            var fmtMethodName = _module.FindTraitMethod(typeName, "fmt");
            if (fmtMethodName == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Type '{typeName}' does not implement Display trait. All types in f-strings must implement Display.",
                    errorLocation
                );
                return;
            }

            var fmtMethod = _module.GetFunction(fmtMethodName);
            if (fmtMethod == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Display::fmt() method not found for type '{typeName}'",
                    errorLocation
                );
                return;
            }

            // Call expr.fmt(&mut formatter)
            // First parameter is &self (the expression value)
            var exprBorrow = new IrBorrowValue(exprValue, fmtMethod.Parameters[0].Type, false);

            // Second parameter is &mut Formatter
            var formatterVarRef = new IrVariable(formatterVarName, formatterType);
            var formatterBorrow = new IrBorrowValue(formatterVarRef, fmtMethod.Parameters[1].Type, true);

            var fmtResultName = $"%t{_tempCounter++}";
            var fmtCall = new IrCall(fmtMethodName, fmtMethod.ReturnType, fmtResultName);
            fmtCall.Location = GetLocation(exprContext);
            fmtCall.Arguments.Add(exprBorrow);
            fmtCall.Arguments.Add(formatterBorrow);
            _currentBlock!.AddInstruction(fmtCall);
        }
    }

    /// <summary>
    /// Emit a write_str call to write a Str value to the formatter.
    /// </summary>
    private void EmitWriteStr(string formatterVarName, IrType formatterType, IrValue strValue)
    {
        // Determine the formatter type name (StackFormatter or Formatter)
        string formatterTypeName = "Formatter";
        if (formatterType is IrStructType structType)
        {
            formatterTypeName = structType.StructName;
        }

        // Find write_str method for the specific formatter type
        var writeStrMethodName = $"{formatterTypeName}::write_str";

        var writeStrMethod = _module.GetFunction(writeStrMethodName);
        if (writeStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"{writeStrMethodName}() method not found",
                errorLocation
            );
            return;
        }

        // Call f.write_str(str_value)
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var formatterBorrow = new IrBorrowValue(formatterVarRef, writeStrMethod.Parameters[0].Type, true);
        var writeStrResultName = $"%t{_tempCounter++}";
        var writeStrCall = new IrCall(writeStrMethodName, writeStrMethod.ReturnType, writeStrResultName);
        writeStrCall.Arguments.Add(formatterBorrow);
        writeStrCall.Arguments.Add(strValue);
        _currentBlock!.AddInstruction(writeStrCall);
    }

    /// <summary>
    /// Emit code to format an integer value and write it to the formatter.
    /// Uses runtime functions (i8_to_string, u32_to_string, etc.) directly
    /// to bypass the Display trait and minimize code size.
    /// </summary>
    private void EmitFormatInteger(string formatterVarName, IrType formatterType, IrValue intValue, IrIntType intType)
    {
        // Bypass Display trait and directly call runtime i{N}_to_string functions
        // This avoids pulling in ALL Display implementations and reduces code size dramatically

        // Determine the runtime function name based on integer type
        string? runtimeFuncName = intType.Name switch
        {
            "i8" => "i8_to_string",
            "i16" => "i16_to_string",
            "i32" => "i32_to_string",
            "i64" => "i64_to_string",
            "u8" => "u8_to_string",
            "u16" => "u16_to_string",
            "u32" => "u32_to_string",
            "u64" => "u64_to_string",
            _ => null
        };

        if (runtimeFuncName == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Integer type '{intType.Name}' not supported in f-strings",
                errorLocation
            );
            return;
        }

        // Ensure the runtime function is declared as external
        if (!_module.Functions.Any(f => f.Name == runtimeFuncName))
        {
            var runtimeFunc = new IrFunction(
                runtimeFuncName,
                IrIntType.U32,
                Visibility.Private,
                isExtern: true
            );
            runtimeFunc.Parameters.Add(new IrParameter("value", intType));
            runtimeFunc.Parameters.Add(new IrParameter("buffer", new IrPointerType(IrIntType.U8)));
            runtimeFunc.Parameters.Add(new IrParameter("buffer_size", IrIntType.U32));
            _module.Functions.Add(runtimeFunc);
        }

        // Allocate a small stack buffer for the string (16 bytes is enough for any 64-bit integer)
        const int bufferSize = 16;
        var bufferVarName = $"_intbuf{_tempCounter++}";
        var bufferType = new IrArrayType(IrIntType.U8, bufferSize);
        var bufferVar = new IrLocalVariable(bufferVarName, bufferType, true);
        _currentFunction!.LocalVariables.Add(bufferVar);
        _localVariables[bufferVarName] = bufferVar;

        // Initialize buffer to zeros
        var zeroArray = new IrArrayLiteral(bufferType);
        for (int i = 0; i < bufferSize; i++)
        {
            zeroArray.Elements.Add(new IrConstant(0, IrIntType.U8));
        }
        _currentBlock!.AddInstruction(new IrLocalDecl(bufferVarName, bufferType, true, zeroArray));

        // Get pointer to buffer: (*u8)&buffer
        var bufferVarRef = new IrVariable(bufferVarName, bufferType);
        var refType = new IrReferenceType(bufferType);
        var borrowExpr = new IrBorrowValue(bufferVarRef, refType, true); // Mutable borrow since function writes to buffer
        var pointerType = new IrPointerType(IrIntType.U8);
        var bufferPtr = new IrCastValue(borrowExpr, refType, pointerType);

        // Call runtime function: len = i16_to_string(value, buffer_ptr, 16)
        var lenResultName = $"%t{_tempCounter++}";
        var toStringCall = new IrCall(runtimeFuncName, IrIntType.U32, lenResultName);
        toStringCall.Arguments.Add(intValue);
        toStringCall.Arguments.Add(bufferPtr);
        toStringCall.Arguments.Add(new IrConstant(bufferSize, IrIntType.U32));
        _currentBlock!.AddInstruction(toStringCall);

        // Create Str from buffer pointer and returned length
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var strValue = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = bufferPtr,
            ["len"] = new IrVariable(lenResultName, IrIntType.U32)
        });

        // Write the Str to the formatter
        EmitWriteStr(formatterVarName, formatterType, strValue);
    }

    /// <summary>
    /// Emit code to format a bool value and write "true" or "false" to the formatter.
    /// </summary>
    private void EmitFormatBool(string formatterVarName, IrType formatterType, IrValue boolValue)
    {
        // Create string literals for "true" and "false"
        var trueLabel = $"_str{_stringCounter++}";
        var trueLiteral = new IrStringLiteral("true", trueLabel);
        StringLiterals.Add(trueLiteral);

        var falseLabel = $"_str{_stringCounter++}";
        var falseLiteral = new IrStringLiteral("false", falseLabel);
        StringLiterals.Add(falseLiteral);

        // Create Str structs
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var trueStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = trueLiteral,
            ["len"] = new IrConstant(4, IrIntType.U32)
        });

        var falseStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = falseLiteral,
            ["len"] = new IrConstant(5, IrIntType.U32)
        });

        // Use conditional to select the right string
        var trueLabel2 = $"_true{_labelCounter++}";
        var falseLabel2 = $"_false{_labelCounter++}";
        var endLabel = $"_end{_labelCounter++}";

        _currentBlock!.AddInstruction(new IrConditionalBranch(boolValue, trueLabel2, falseLabel2));

        // True branch
        _currentBlock!.AddInstruction(new IrLabel(trueLabel2));
        EmitWriteStr(formatterVarName, formatterType, trueStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch
        _currentBlock!.AddInstruction(new IrLabel(falseLabel2));
        EmitWriteStr(formatterVarName, formatterType, falseStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
    }

    /// <summary>
    /// Emit code to format a String value by calling String::as_str() first.
    /// This bypasses the Display trait which expects Formatter, not StackFormatter.
    /// </summary>
    private void EmitFormatString(string formatterVarName, IrType formatterType, IrValue stringValue)
    {
        // Call String::as_str() to get a Str, then write it to the formatter
        // This bypasses the Display trait which expects Formatter, not StackFormatter

        var asStrMethodName = "String::as_str";
        var asStrMethod = _module.GetFunction(asStrMethodName);
        if (asStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "String::as_str() method not found",
                errorLocation
            );
            return;
        }

        // Borrow the String value
        var stringBorrow = new IrBorrowValue(stringValue, asStrMethod.Parameters[0].Type, false);

        // Call String::as_str(&self)
        var asStrResultName = $"%t{_tempCounter++}";
        var asStrCall = new IrCall(asStrMethodName, asStrMethod.ReturnType, asStrResultName);
        asStrCall.Arguments.Add(stringBorrow);
        _currentBlock!.AddInstruction(asStrCall);

        // Get the Str result and write it to the formatter
        var strValue = new IrVariable(asStrResultName, asStrMethod.ReturnType);
        EmitWriteStr(formatterVarName, formatterType, strValue);

        // Drop the temporary String after we've extracted and copied its data
        // This prevents memory leaks when functions like move_to() return String values
        // that get used in f-strings and then need to be cleaned up.
        EmitDropForTemporaryString(stringValue);
    }

    /// <summary>
    /// Emit a Drop call for a temporary String value used in f-strings.
    /// This is necessary because String contains Vec&lt;u8&gt; which owns heap memory.
    ///
    /// NOTE: We call Vec_u8_drop directly on the String's vec field rather than
    /// String_Drop_drop because String intentionally doesn't implement Drop.
    /// This is due to a compiler bug where defer cleanup runs before return
    /// value is copied out, causing double-free when functions return String.
    /// </summary>
    private void EmitDropForTemporaryString(IrValue stringValue)
    {
        // Get the String type and find the 'vec' field
        var stringType = stringValue.Type as IrStructType;
        if (stringType == null || stringType.StructName != "String")
        {
            return;
        }

        var vecField = stringType.Fields.FirstOrDefault(f => f.Name == "vec");
        if (vecField == null)
        {
            return;
        }

        // Get the Vec<u8> type from the field
        var vecType = vecField.Type as IrStructType;
        if (vecType == null)
        {
            return;
        }

        // Ensure the Drop method for Vec<u8> is instantiated
        // This may trigger generic instantiation if not already done
        EnsureDropMethodInstantiated(vecType);

        // Now look for the drop method - try trait impl name first (Vec<u8>_Drop_drop),
        // then regular method name (Vec_u8_drop)
        var typeName = vecType.CacheKey ?? vecType.StructName;
        var dropMethodName = $"{typeName}_Drop_drop";
        var dropMethod = _module.GetFunction(dropMethodName);

        if (dropMethod == null)
        {
            // Try the regular method name (mangled: Vec<u8> -> Vec_u8)
            dropMethodName = $"{typeName.Replace("<", "_").Replace(">", "_").TrimEnd('_')}_drop";
            dropMethod = _module.GetFunction(dropMethodName);
        }

        if (dropMethod == null)
        {
            // Vec doesn't have drop instantiated - this shouldn't happen but handle gracefully
            return;
        }

        // Use IrMemberAccess to load the 'vec' field into a temp variable
        var vecTempName = $"%t{_tempCounter++}";
        var memberAccess = new IrMemberAccess(vecTempName, stringValue, "vec", vecField.Type, vecField.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        // Create a variable reference to the loaded vec field
        var vecVarRef = new IrVariable(vecTempName, vecField.Type);

        // Create a mutable borrow of the vec for calling drop(&mut self)
        var vecMutBorrow = new IrBorrowValue(vecVarRef, new IrMutReferenceType(vecField.Type), isMutable: true);

        // Call Vec_u8_drop(&mut string.vec)
        var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
        dropCall.Arguments.Add(vecMutBorrow);
        _currentBlock!.AddInstruction(dropCall);
    }

    /// <summary>
    /// Parse an interpolated string into a list of segments.
    /// Each segment is either a string literal or an expression to format.
    /// </summary>
    private List<InterpolationSegment> ParseInterpolatedString(string content)
    {
        var segments = new List<InterpolationSegment>();
        var i = 0;
        var currentString = new System.Text.StringBuilder();

        while (i < content.Length)
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                // Handle escape sequences
                var nextChar = content[i + 1];
                if (nextChar == '{' || nextChar == '}')
                {
                    // Escaped brace: \{ -> {, \} -> }
                    currentString.Append(nextChar);
                    i += 2;
                }
                else if (nextChar == 'x' && i + 3 < content.Length)
                {
                    // Hex escape: \xNN
                    var hexDigits = content.Substring(i + 2, 2);
                    var byteValue = Convert.ToByte(hexDigits, 16);
                    currentString.Append((char)byteValue);
                    i += 4;
                }
                else
                {
                    // Standard escape sequences
                    var escapeChar = nextChar switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        'b' => '\b',
                        'f' => '\f',
                        '"' => '"',
                        '\'' => '\'',
                        '\\' => '\\',
                        '0' => '\0',
                        _ => nextChar  // Unknown escape sequence - just use the character as-is
                    };
                    currentString.Append(escapeChar);
                    i += 2;
                }
            }
            else if (content[i] == '{')
            {
                // Start of interpolation
                // Save any accumulated string content
                if (currentString.Length > 0)
                {
                    segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
                    currentString.Clear();
                }

                // Find the matching closing brace
                var braceDepth = 1;
                var exprStart = i + 1;
                i++;

                while (i < content.Length && braceDepth > 0)
                {
                    if (content[i] == '{')
                    {
                        braceDepth++;
                    }
                    else if (content[i] == '}')
                    {
                        braceDepth--;
                    }

                    if (braceDepth > 0)
                    {
                        i++;
                    }
                }

                if (braceDepth != 0)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        "Unmatched braces in f-string interpolation",
                        errorLocation
                    );
                    // Return empty list for error recovery
                    return new List<InterpolationSegment>();
                }

                // Extract the expression
                var expression = content.Substring(exprStart, i - exprStart);
                segments.Add(new InterpolationSegment { IsStringSegment = false, Expression = expression });
                i++; // Skip the closing brace
            }
            else
            {
                // Regular character
                currentString.Append(content[i]);
                i++;
            }
        }

        // Add any remaining string content
        if (currentString.Length > 0)
        {
            segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
        }

        return segments;
    }

    /// <summary>
    /// Helper class representing a segment of an interpolated string.
    /// </summary>
    private class InterpolationSegment
    {
        public bool IsStringSegment { get; set; }
        public string StringContent { get; set; } = "";
        public string Expression { get; set; } = "";
    }

    // NOTE: ProcessEscapeSequences is defined in IrBuilder.Expressions.cs
    // since it's used by both VisitStringLiteral (there) and f-strings (here)
}
