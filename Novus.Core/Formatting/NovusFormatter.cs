using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Novus.Parser;

namespace Novus.Formatting;

/// <summary>
/// Formats Novus source code by traversing the AST and producing properly formatted output.
/// Preserves comments by reading them from the hidden channel.
/// </summary>
public class NovusFormatter : NovusParserBaseVisitor<object?>
{
    private readonly StringBuilder _output = new();
    private int _indentLevel = 0;
    private readonly int _indentSize;
    private readonly int _maxWidth;
    private bool _atLineStart = true;
    private readonly CommonTokenStream _tokenStream;
    private int _lastTokenIndex = -1;

    public NovusFormatter(int indentSize = 4, int maxWidth = 100)
    {
        _indentSize = indentSize;
        _maxWidth = maxWidth;
        _tokenStream = null!;
    }

    public NovusFormatter(CommonTokenStream tokenStream, int indentSize = 4, int maxWidth = 100)
    {
        _tokenStream = tokenStream;
        _indentSize = indentSize;
        _maxWidth = maxWidth;
    }

    public string Format(NovusParser.CompilationUnitContext tree)
    {
        _output.Clear();
        _indentLevel = 0;
        _atLineStart = true;
        _lastTokenIndex = -1;

        // Fill the token stream to include all tokens (including hidden channel)
        if (_tokenStream != null)
        {
            _tokenStream.Fill();

            // Emit any leading comments that appear before the first token
            EmitLeadingComments();
        }

        Visit(tree);

        // Emit any trailing comments
        if (_tokenStream != null)
        {
            EmitTrailingComments();
        }

        return _output.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Emit any comments that appear at the very start of the file (before the first real token)
    /// </summary>
    private void EmitLeadingComments()
    {
        if (_tokenStream == null) return;

        var allTokens = _tokenStream.GetTokens();
        if (allTokens == null) return;

        // Emit all hidden channel tokens that appear before the first DEFAULT channel token
        foreach (var token in allTokens)
        {
            if (token.Channel == 0) break; // Stop at first DEFAULT channel token

            if (token.Channel == 1) // HIDDEN channel
            {
                EmitComment(token);
                _lastTokenIndex = token.TokenIndex;
            }
        }
    }

    /// <summary>
    /// Emit comments and whitespace that appear between the last emitted token and the current token
    /// </summary>
    private void EmitHiddenTokensBefore(IToken token)
    {
        if (_tokenStream == null) return;

        var tokenIndex = token.TokenIndex;
        if (tokenIndex <= _lastTokenIndex) return;

        // We need to emit all tokens between _lastTokenIndex and tokenIndex that are on the HIDDEN channel
        var allTokens = _tokenStream.GetTokens();
        if (allTokens == null) return;

        for (int i = _lastTokenIndex + 1; i < tokenIndex; i++)
        {
            var t = allTokens[i];
            if (t.Channel == 1) // HIDDEN channel
            {
                EmitComment(t);
            }
        }

        _lastTokenIndex = tokenIndex;
    }

    /// <summary>
    /// Emit a single comment token, checking whether it's on the same line as the previous content
    /// </summary>
    private void EmitComment(IToken token)
    {
        if (token.Type == NovusLexer.LINE_COMMENT)
        {
            // Line comment - check if it's on the same line as previous content
            // by seeing if we're not at line start
            bool isInline = !_atLineStart;

            if (isInline)
            {
                Write("  "); // Two spaces before inline comment
            }
            Write(token.Text);
            WriteLine();
        }
        else if (token.Type == NovusLexer.BLOCK_COMMENT)
        {
            // Block comment
            bool isMultiline = token.Text.Contains('\n');

            if (isMultiline)
            {
                // Multi-line block comments always go on their own lines
                if (!_atLineStart)
                {
                    WriteLine();
                }
                Write(token.Text);
                WriteLine();
            }
            else
            {
                // Single-line block comment - can be inline or standalone
                if (!_atLineStart)
                {
                    Write(" ");
                }
                Write(token.Text);
                Write(" ");
            }
        }
    }

    /// <summary>
    /// Emit any trailing comments at the end of the file
    /// </summary>
    private void EmitTrailingComments()
    {
        if (_tokenStream == null) return;

        // Get all tokens including hidden ones from channel 1 (HIDDEN)
        var allTokens = _tokenStream.GetTokens();
        if (allTokens == null) return;

        // Emit all remaining hidden channel tokens after the last token we processed
        for (int i = _lastTokenIndex + 1; i < allTokens.Count; i++)
        {
            var token = allTokens[i];
            if (token.Type == TokenConstants.EOF) continue;
            if (token.Channel == 1) // HIDDEN channel
            {
                EmitComment(token);
            }
        }
    }

    /// <summary>
    /// Emit hidden tokens before a parse tree node's first token
    /// </summary>
    private void EmitHiddenTokensBefore(ParserRuleContext context)
    {
        if (context.Start != null)
        {
            EmitHiddenTokensBefore(context.Start);
        }
    }

    private void Write(string text)
    {
        if (_atLineStart && !string.IsNullOrWhiteSpace(text))
        {
            _output.Append(new string(' ', _indentLevel * _indentSize));
            _atLineStart = false;
        }
        _output.Append(text);
    }

    private void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            Write(text);
        }
        _output.AppendLine();
        _atLineStart = true;
    }

    /// <summary>
    /// Emit a newline, but first check if there are any trailing comments on the current line
    /// that should be emitted before the newline
    /// </summary>
    private void WriteLineWithTrailingComments(ParserRuleContext context)
    {
        // Emit any comments that appear after the end of this context but before the next newline
        if (_tokenStream != null && context.Stop != null)
        {
            var stopIndex = context.Stop.TokenIndex;
            var allTokens = _tokenStream.GetTokens();

            // Look for comments after the stop token but on the same line
            for (int i = stopIndex + 1; i < allTokens.Count; i++)
            {
                var token = allTokens[i];

                // If we hit a newline, stop looking
                if (token.Type == NovusLexer.NEWLINE) break;

                // If we hit EOF, stop looking
                if (token.Type == TokenConstants.EOF) break;

                // If it's a comment on the HIDDEN channel, emit it
                if (token.Channel == 1 && token.Type == NovusLexer.LINE_COMMENT)
                {
                    Write("  ");
                    Write(token.Text);
                    _lastTokenIndex = Math.Max(_lastTokenIndex, token.TokenIndex);
                    break; // Only one trailing comment per line
                }
            }
        }

        WriteLine();
    }

    private void WriteBlankLine()
    {
        // Only add blank line if we're not already at a blank line
        var str = _output.ToString();
        if (!str.EndsWith("\n\n") && !str.EndsWith(Environment.NewLine + Environment.NewLine))
        {
            WriteLine();
        }
    }

    private void Indent() => _indentLevel++;
    private void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);

    // ==================== Top-level declarations ====================

    public override object? VisitCompilationUnit(NovusParser.CompilationUnitContext context)
    {
        EmitHiddenTokensBefore(context);

        // Handle imports
        var imports = context.importDeclaration();
        foreach (var import in imports)
        {
            Visit(import);
        }

        // Handle reexports
        var reexports = context.reexportDeclaration();
        foreach (var reexport in reexports)
        {
            Visit(reexport);
        }

        // Add blank line after imports if there are any and more content follows
        if ((imports.Length > 0 || reexports.Length > 0) &&
            (context.constDeclaration().Length > 0 || context.staticDeclaration().Length > 0 ||
             context.globalVariableDeclaration().Length > 0 || context.structDeclaration().Length > 0 ||
             context.enumDeclaration().Length > 0 || context.traitDeclaration().Length > 0 ||
             context.implDeclaration().Length > 0 || context.functionDeclaration().Length > 0))
        {
            WriteBlankLine();
        }

        // Process other declarations in order of appearance
        var allDeclarations = new List<(int Index, IParseTree Node)>();

        foreach (var c in context.constDeclaration()) allDeclarations.Add((c.Start.StartIndex, c));
        foreach (var s in context.staticDeclaration()) allDeclarations.Add((s.Start.StartIndex, s));
        foreach (var g in context.globalVariableDeclaration()) allDeclarations.Add((g.Start.StartIndex, g));
        foreach (var st in context.structDeclaration()) allDeclarations.Add((st.Start.StartIndex, st));
        foreach (var e in context.enumDeclaration()) allDeclarations.Add((e.Start.StartIndex, e));
        foreach (var t in context.traitDeclaration()) allDeclarations.Add((t.Start.StartIndex, t));
        foreach (var i in context.implDeclaration()) allDeclarations.Add((i.Start.StartIndex, i));
        foreach (var f in context.functionDeclaration()) allDeclarations.Add((f.Start.StartIndex, f));

        allDeclarations.Sort((a, b) => a.Index.CompareTo(b.Index));

        bool firstTopLevel = true;
        foreach (var (_, node) in allDeclarations)
        {
            if (!firstTopLevel)
            {
                WriteBlankLine();
            }
            Visit(node);
            firstTopLevel = false;
        }

        return null;
    }

    public override object? VisitImportDeclaration(NovusParser.ImportDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("from ");
        Visit(context.modulePath());
        Write(" import ");
        Visit(context.importList());
        WriteLine();
        return null;
    }

    public override object? VisitModulePath(NovusParser.ModulePathContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write("::");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    public override object? VisitImportList(NovusParser.ImportListContext context)
    {
        if (context.GetText() == "*")
        {
            Write("*");
            return null;
        }

        var names = context.importName();
        for (int i = 0; i < names.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(names[i]);
        }
        return null;
    }

    public override object? VisitImportName(NovusParser.ImportNameContext context)
    {
        var identifiers = context.IDENTIFIER();

        if (context.importWildcard() != null)
        {
            Visit(context.importWildcard());
            // If there's an alias after the wildcard
            if (context.KW_AS() != null && identifiers.Length > 0)
            {
                Write(" as ");
                Write(identifiers[0].GetText());
            }
        }
        else
        {
            // First identifier is the import name
            if (identifiers.Length > 0)
            {
                Write(identifiers[0].GetText());
            }
            // Second identifier (if present) is the alias
            if (context.KW_AS() != null && identifiers.Length > 1)
            {
                Write(" as ");
                Write(identifiers[1].GetText());
            }
        }
        return null;
    }

    public override object? VisitImportWildcard(NovusParser.ImportWildcardContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitReexportDeclaration(NovusParser.ReexportDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("pub use ");
        Visit(context.modulePath());
        Write("::");
        if (context.reexportList() != null)
        {
            Visit(context.reexportList());
        }
        else
        {
            Write("*");
        }
        WriteLine();
        return null;
    }

    public override object? VisitReexportList(NovusParser.ReexportListContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write(", ");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    // ==================== Attributes ====================

    public override object? VisitAttribute(NovusParser.AttributeContext context)
    {
        EmitHiddenTokensBefore(context);
        if (context.GetChild(0).GetText() == "@")
        {
            Write("@");
            Write(context.IDENTIFIER().GetText());
            if (context.attributeArgList() != null)
            {
                Write("(");
                Visit(context.attributeArgList());
                Write(")");
            }
        }
        else
        {
            Write("#[");
            Write(context.IDENTIFIER().GetText());
            if (context.attributeArgList() != null)
            {
                Write("(");
                Visit(context.attributeArgList());
                Write(")");
            }
            Write("]");
        }
        WriteLine();
        return null;
    }

    public override object? VisitAttributeArgList(NovusParser.AttributeArgListContext context)
    {
        var args = context.attributeArg();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(args[i]);
        }
        return null;
    }

    public override object? VisitAttributeArg(NovusParser.AttributeArgContext context)
    {
        if (context.IDENTIFIER() != null)
        {
            Write(context.IDENTIFIER().GetText());
            Write(" = ");
        }
        Visit(context.expression());
        return null;
    }

    // ==================== Constants and Statics ====================

    public override object? VisitConstDeclaration(NovusParser.ConstDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("const ");
        Write(context.IDENTIFIER().GetText());

        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }

        Write(" = ");
        Visit(context.expression());
        WriteLine();
        return null;
    }

    public override object? VisitStaticDeclaration(NovusParser.StaticDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("static ");
        if (context.KW_MUT() != null) Write("mut ");
        Write(context.IDENTIFIER().GetText());

        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }

        Write(" = ");
        Visit(context.expression());
        WriteLine();
        return null;
    }

    public override object? VisitGlobalVariableDeclaration(NovusParser.GlobalVariableDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        Write("extern var ");
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        Visit(context.type());

        if (context.KW_AT() != null)
        {
            Write(" at ");
            Visit(context.expression());
        }

        WriteLine();
        return null;
    }

    // ==================== Functions ====================

    public override object? VisitFunctionDeclaration(NovusParser.FunctionDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_EXTERN() != null) Write("extern ");
        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("fn ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        Write("(");
        if (context.parameterList() != null)
        {
            Visit(context.parameterList());
        }
        Write(")");

        if (context.type() != null)
        {
            Write(" -> ");
            Visit(context.type());
        }

        if (context.whereClause() != null)
        {
            Write(" ");
            Visit(context.whereClause());
        }

        if (context.block() != null)
        {
            Write(" ");
            Visit(context.block());
        }
        else
        {
            WriteLine();
        }

        return null;
    }

    public override object? VisitGenericParams(NovusParser.GenericParamsContext context)
    {
        Write("<");
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write(", ");
            Write(identifiers[i].GetText());
        }
        Write(">");
        return null;
    }

    public override object? VisitParameterList(NovusParser.ParameterListContext context)
    {
        bool first = true;

        if (context.selfParameter() != null)
        {
            Visit(context.selfParameter());
            first = false;
        }

        foreach (var param in context.parameter())
        {
            if (!first) Write(", ");
            Visit(param);
            first = false;
        }

        if (context.variadicParameter() != null)
        {
            if (!first) Write(", ");
            Visit(context.variadicParameter());
        }

        return null;
    }

    public override object? VisitSelfParameter(NovusParser.SelfParameterContext context)
    {
        if (context.GetText().StartsWith("&"))
        {
            Write("&");
            if (context.KW_MUT() != null) Write("mut ");
            Write("self");
        }
        else
        {
            if (context.KW_CONSUMING() != null) Write("consuming ");
            Write("self");
        }
        return null;
    }

    public override object? VisitParameter(NovusParser.ParameterContext context)
    {
        if (context.KW_CONSUMING() != null) Write("consuming ");
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        Visit(context.type());
        return null;
    }

    public override object? VisitVariadicParameter(NovusParser.VariadicParameterContext context)
    {
        Write("...");
        Write(context.IDENTIFIER().GetText());
        return null;
    }

    public override object? VisitWhereClause(NovusParser.WhereClauseContext context)
    {
        Write("where ");
        var bounds = context.whereBound();
        for (int i = 0; i < bounds.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(bounds[i]);
        }
        return null;
    }

    public override object? VisitWhereBound(NovusParser.WhereBoundContext context)
    {
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        Visit(context.traitBound());
        return null;
    }

    public override object? VisitSingleTraitBound(NovusParser.SingleTraitBoundContext context)
    {
        Visit(context.typeName());
        if (context.genericTypeArgs() != null)
        {
            Visit(context.genericTypeArgs());
        }
        return null;
    }

    public override object? VisitMultipleTraitBound(NovusParser.MultipleTraitBoundContext context)
    {
        Visit(context.traitBound(0));
        Write(" + ");
        Visit(context.traitBound(1));
        return null;
    }

    // ==================== Structs ====================

    public override object? VisitStructDeclaration(NovusParser.StructDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("struct ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        if (context.whereClause() != null)
        {
            Write(" ");
            Visit(context.whereClause());
        }

        Write(" {");

        var fields = context.structField();
        if (fields.Length > 0)
        {
            WriteLine();
            Indent();
            foreach (var field in fields)
            {
                Visit(field);
            }
            Dedent();
        }

        WriteLine("}");
        return null;
    }

    public override object? VisitStructField(NovusParser.StructFieldContext context)
    {
        EmitHiddenTokensBefore(context);
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        Visit(context.type());
        WriteLine(",");
        return null;
    }

    // ==================== Enums ====================

    public override object? VisitEnumDeclaration(NovusParser.EnumDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("enum ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        if (context.whereClause() != null)
        {
            Write(" ");
            Visit(context.whereClause());
        }

        Write(" {");

        var variants = context.enumVariant();
        if (variants.Length > 0)
        {
            WriteLine();
            Indent();
            for (int i = 0; i < variants.Length; i++)
            {
                EmitHiddenTokensBefore(variants[i]);
                Visit(variants[i]);
                WriteLine(",");
            }
            Dedent();
        }

        WriteLine("}");
        return null;
    }

    public override object? VisitEnumVariant(NovusParser.EnumVariantContext context)
    {
        Write(context.IDENTIFIER().GetText());
        if (context.typeList() != null)
        {
            Write("(");
            Visit(context.typeList());
            Write(")");
        }
        return null;
    }

    // ==================== Traits ====================

    public override object? VisitTraitDeclaration(NovusParser.TraitDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        if (context.KW_PUB() != null) Write("pub ");
        if (context.KW_INTERNAL() != null) Write("internal ");

        Write("trait ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        Write(" {");

        var items = context.traitItem();
        if (items.Length > 0)
        {
            WriteLine();
            Indent();
            foreach (var item in items)
            {
                Visit(item);
            }
            Dedent();
        }

        WriteLine("}");
        return null;
    }

    public override object? VisitTraitMethodDeclaration(NovusParser.TraitMethodDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("fn ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        Write("(");
        if (context.parameterList() != null)
        {
            Visit(context.parameterList());
        }
        Write(")");

        if (context.type() != null)
        {
            Write(" -> ");
            Visit(context.type());
        }

        // Handle optional body (default implementation)
        if (context.block() != null)
        {
            Write(" ");
            Visit(context.block());
        }

        WriteLine();
        return null;
    }

    public override object? VisitFunctionSignature(NovusParser.FunctionSignatureContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("fn ");
        Write(context.IDENTIFIER().GetText());

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
        }

        Write("(");
        if (context.parameterList() != null)
        {
            Visit(context.parameterList());
        }
        Write(")");

        if (context.type() != null)
        {
            Write(" -> ");
            Visit(context.type());
        }

        WriteLine();
        return null;
    }

    // ==================== Impl ====================

    public override object? VisitImplDeclaration(NovusParser.ImplDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        foreach (var attr in context.attribute())
        {
            Visit(attr);
        }

        Write("impl ");

        if (context.genericParams() != null)
        {
            Visit(context.genericParams());
            Write(" ");
        }

        // Trait impl: impl Trait for Type
        if (context.traitTypeName != null && context.KW_FOR() != null)
        {
            Visit(context.traitTypeName);
            if (context.traitTypeArgs != null)
            {
                Visit(context.traitTypeArgs);
            }
            Write(" for ");
            Visit(context.implTargetType());
        }
        // Self impl: impl Type
        else if (context.targetTypeName != null)
        {
            Visit(context.targetTypeName);
            if (context.targetTypeArgs != null)
            {
                Visit(context.targetTypeArgs);
            }
        }

        if (context.whereClause() != null)
        {
            Write(" ");
            Visit(context.whereClause());
        }

        Write(" {");

        var items = context.implItem();
        if (items.Length > 0)
        {
            WriteLine();
            Indent();
            bool first = true;
            foreach (var item in items)
            {
                if (!first) WriteBlankLine();
                Visit(item);
                first = false;
            }
            Dedent();
        }

        WriteLine("}");
        return null;
    }

    public override object? VisitNamedImplTarget(NovusParser.NamedImplTargetContext context)
    {
        Visit(context.typeName());
        if (context.implTypeArgs != null)
        {
            Visit(context.implTypeArgs);
        }
        return null;
    }

    public override object? VisitPrimitiveImplTarget(NovusParser.PrimitiveImplTargetContext context)
    {
        Visit(context.primitiveTypeName());
        return null;
    }

    public override object? VisitPrimitiveTypeName(NovusParser.PrimitiveTypeNameContext context)
    {
        Write(context.GetText());
        return null;
    }

    // ==================== Types ====================

    public override object? VisitReferenceType(NovusParser.ReferenceTypeContext context)
    {
        Write("&");
        if (context.KW_MUT() != null) Write("mut ");
        Visit(context.type());
        return null;
    }

    public override object? VisitPointerType(NovusParser.PointerTypeContext context)
    {
        Write("*");
        Visit(context.type());
        return null;
    }

    public override object? VisitArrayTypeWithSize(NovusParser.ArrayTypeWithSizeContext context)
    {
        Write("[");
        Visit(context.type());
        Write("; ");
        Visit(context.expression());
        Write("]");
        return null;
    }

    public override object? VisitArrayTypeInferred(NovusParser.ArrayTypeInferredContext context)
    {
        Write("[");
        Visit(context.type());
        Write("]");
        return null;
    }

    public override object? VisitUnitType(NovusParser.UnitTypeContext context)
    {
        Write("()");
        return null;
    }

    public override object? VisitTupleType(NovusParser.TupleTypeContext context)
    {
        Write("(");
        var types = context.type();
        for (int i = 0; i < types.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(types[i]);
        }
        Write(")");
        return null;
    }

    public override object? VisitFunctionPointerType(NovusParser.FunctionPointerTypeContext context)
    {
        Write("fn(");
        if (context.typeList() != null)
        {
            Visit(context.typeList());
        }
        Write(")");
        if (context.type() != null)
        {
            Write(" -> ");
            Visit(context.type());
        }
        return null;
    }

    public override object? VisitSelfType(NovusParser.SelfTypeContext context)
    {
        Write("Self");
        return null;
    }

    public override object? VisitPrimitiveType(NovusParser.PrimitiveTypeContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitNamedType(NovusParser.NamedTypeContext context)
    {
        Visit(context.typeName());
        if (context.genericTypeArgs() != null)
        {
            Write("<");
            Visit(context.genericTypeArgs().typeList());
            Write(">");
        }
        return null;
    }

    public override object? VisitTypeName(NovusParser.TypeNameContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write("::");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    public override object? VisitTypeList(NovusParser.TypeListContext context)
    {
        var types = context.type();
        for (int i = 0; i < types.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(types[i]);
        }
        return null;
    }

    public override object? VisitGenericTypeArgs(NovusParser.GenericTypeArgsContext context)
    {
        Write("<");
        Visit(context.typeList());
        Write(">");
        return null;
    }

    // ==================== Blocks and Statements ====================

    public override object? VisitBlock(NovusParser.BlockContext context)
    {
        WriteLine("{");
        Indent();

        var statements = context.statement();
        foreach (var stmt in statements)
        {
            Visit(stmt);
        }

        Dedent();
        WriteLine("}");
        return null;
    }

    public override object? VisitReturnStatement(NovusParser.ReturnStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("return");
        if (context.expression() != null)
        {
            Write(" ");
            Visit(context.expression());
        }
        if (context.postfixCondition() != null)
        {
            Write(" ");
            Visit(context.postfixCondition());
        }
        WriteLine();
        return null;
    }

    public override object? VisitVariableDeclaration(NovusParser.VariableDeclarationContext context)
    {
        EmitHiddenTokensBefore(context);

        if (context.KW_LET() != null) Write("let ");
        else Write("var ");

        if (context.KW_MUT() != null) Write("mut ");

        if (context.IDENTIFIER() != null)
        {
            Write(context.IDENTIFIER().GetText());
        }
        else if (context.tuplePattern() != null)
        {
            Visit(context.tuplePattern());
        }
        else
        {
            Write("_");
        }

        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }

        Write(" = ");
        Visit(context.expression());
        WriteLineWithTrailingComments(context);
        return null;
    }

    public override object? VisitTuplePattern(NovusParser.TuplePatternContext context)
    {
        Write("(");
        var children = new List<string>();
        foreach (var child in context.children)
        {
            if (child is ITerminalNode term)
            {
                var text = term.GetText();
                if (text == "_" || (term.Symbol.Type != NovusLexer.NEWLINE &&
                                    text != "(" && text != ")" && text != ","))
                {
                    children.Add(text);
                }
            }
        }
        Write(string.Join(", ", children));
        Write(")");
        return null;
    }

    public override object? VisitAssignmentStatement(NovusParser.AssignmentStatementContext context)
    {
        EmitHiddenTokensBefore(context);

        // Handle prefix dereferences
        var prefixStars = context.children
            .TakeWhile(c => c.GetText() == "*")
            .Count();
        for (int i = 0; i < prefixStars; i++) Write("*");

        // Write identifier or self
        if (context.KW_SELF() != null)
        {
            Write("self");
        }
        else
        {
            Write(context.IDENTIFIER().GetText());
        }

        // Write suffixes
        foreach (var suffix in context.lvalueSuffix())
        {
            Visit(suffix);
        }

        // Write assignment operator and expression (or increment/decrement)
        var text = context.GetText();
        if (text.Contains("++"))
        {
            Write("++");
        }
        else if (text.Contains("--"))
        {
            Write("--");
        }
        else
        {
            // Find the assignment operator
            var ops = new[] { "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=" };
            foreach (var op in ops)
            {
                if (context.GetChild(0) is not null)
                {
                    for (int i = 0; i < context.ChildCount; i++)
                    {
                        if (context.GetChild(i).GetText() == op)
                        {
                            Write($" {op} ");
                            break;
                        }
                    }
                }
            }
            Visit(context.expression());
        }

        if (context.postfixCondition() != null)
        {
            Write(" ");
            Visit(context.postfixCondition());
        }

        WriteLine();
        return null;
    }

    public override object? VisitLvalueSuffix(NovusParser.LvalueSuffixContext context)
    {
        if (context.IDENTIFIER() != null)
        {
            Write(".");
            Write(context.IDENTIFIER().GetText());
        }
        else
        {
            Write("[");
            Visit(context.expression());
            Write("]");
        }
        return null;
    }

    public override object? VisitPostfixCondition(NovusParser.PostfixConditionContext context)
    {
        if (context.KW_IF() != null)
        {
            Write("if ");
        }
        else
        {
            Write("unless ");
        }
        Visit(context.expression());
        return null;
    }

    public override object? VisitIfStatement(NovusParser.IfStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("if ");
        Visit(context.ifCondition());
        Write(" ");

        // Visit the first block (if body)
        Visit(context.block(0));

        // Handle else
        if (context.KW_ELSE() != null)
        {
            // Remove the last newline to put else on same line
            TrimTrailingNewline();
            Write(" else ");

            if (context.ifStatement() != null)
            {
                Visit(context.ifStatement());
            }
            else if (context.block().Length > 1)
            {
                Visit(context.block(1));
            }
        }

        return null;
    }

    private void TrimTrailingNewline()
    {
        var str = _output.ToString();
        if (str.EndsWith(Environment.NewLine))
        {
            _output.Length -= Environment.NewLine.Length;
            _atLineStart = false;
        }
        else if (str.EndsWith("\n"))
        {
            _output.Length--;
            _atLineStart = false;
        }
    }

    public override object? VisitIfConditionExpression(NovusParser.IfConditionExpressionContext context)
    {
        Visit(context.expression());
        return null;
    }

    public override object? VisitIfConditionLet(NovusParser.IfConditionLetContext context)
    {
        Write("let ");
        Write(context.IDENTIFIER().GetText());
        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }
        Write(" = ");
        Visit(context.expression());
        return null;
    }

    public override object? VisitIfConditionVar(NovusParser.IfConditionVarContext context)
    {
        Write("var ");
        Write(context.IDENTIFIER().GetText());
        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }
        Write(" = ");
        Visit(context.expression());
        return null;
    }

    public override object? VisitWhileStatement(NovusParser.WhileStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("while ");
        Visit(context.expression());
        Write(" ");
        Visit(context.block());
        return null;
    }

    public override object? VisitForCStyle(NovusParser.ForCStyleContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("for ");

        // Init
        if (context.variableDeclaration() != null)
        {
            VisitVariableDeclarationInline(context.variableDeclaration());
        }
        else
        {
            VisitAssignmentStatementInline(context.assignmentStatement(0));
        }

        Write("; ");
        Visit(context.expression());
        Write("; ");
        VisitAssignmentStatementInline(context.assignmentStatement(context.variableDeclaration() != null ? 0 : 1));
        Write(" ");
        Visit(context.block());
        return null;
    }

    private void VisitVariableDeclarationInline(NovusParser.VariableDeclarationContext context)
    {
        if (context.KW_LET() != null) Write("let ");
        else Write("var ");

        if (context.KW_MUT() != null) Write("mut ");

        if (context.IDENTIFIER() != null)
        {
            Write(context.IDENTIFIER().GetText());
        }
        else if (context.tuplePattern() != null)
        {
            Visit(context.tuplePattern());
        }
        else
        {
            Write("_");
        }

        if (context.type() != null)
        {
            Write(": ");
            Visit(context.type());
        }

        Write(" = ");
        Visit(context.expression());
    }

    private void VisitAssignmentStatementInline(NovusParser.AssignmentStatementContext context)
    {
        var prefixStars = context.children
            .TakeWhile(c => c.GetText() == "*")
            .Count();
        for (int i = 0; i < prefixStars; i++) Write("*");

        if (context.KW_SELF() != null)
        {
            Write("self");
        }
        else
        {
            Write(context.IDENTIFIER().GetText());
        }

        foreach (var suffix in context.lvalueSuffix())
        {
            Visit(suffix);
        }

        var text = context.GetText();
        if (text.Contains("++"))
        {
            Write("++");
        }
        else if (text.Contains("--"))
        {
            Write("--");
        }
        else
        {
            var ops = new[] { "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=" };
            foreach (var op in ops)
            {
                for (int i = 0; i < context.ChildCount; i++)
                {
                    if (context.GetChild(i).GetText() == op)
                    {
                        Write($" {op} ");
                        break;
                    }
                }
            }
            Visit(context.expression());
        }
    }

    public override object? VisitForInLoop(NovusParser.ForInLoopContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("for ");
        if (context.KW_MUT() != null) Write("mut ");
        Write(context.IDENTIFIER().GetText());
        Write(" in ");
        Visit(context.expression());
        Write(" ");
        Visit(context.block());
        return null;
    }

    public override object? VisitForeverStatement(NovusParser.ForeverStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("forever ");
        Visit(context.block());
        return null;
    }

    public override object? VisitLabeledLoop(NovusParser.LabeledLoopContext context)
    {
        EmitHiddenTokensBefore(context);
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        if (context.whileStatement() != null)
        {
            Visit(context.whileStatement());
        }
        else if (context.forStatement() != null)
        {
            Visit(context.forStatement());
        }
        else
        {
            Visit(context.foreverStatement());
        }
        return null;
    }

    public override object? VisitBreakStatement(NovusParser.BreakStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("break");
        if (context.IDENTIFIER() != null)
        {
            Write(" ");
            Write(context.IDENTIFIER().GetText());
        }
        if (context.postfixCondition() != null)
        {
            Write(" ");
            Visit(context.postfixCondition());
        }
        WriteLine();
        return null;
    }

    public override object? VisitContinueStatement(NovusParser.ContinueStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("continue");
        if (context.IDENTIFIER() != null)
        {
            Write(" ");
            Write(context.IDENTIFIER().GetText());
        }
        if (context.postfixCondition() != null)
        {
            Write(" ");
            Visit(context.postfixCondition());
        }
        WriteLine();
        return null;
    }

    public override object? VisitDeferBlock(NovusParser.DeferBlockContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("defer ");
        Visit(context.block());
        return null;
    }

    public override object? VisitDeferExpression(NovusParser.DeferExpressionContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("defer ");
        Visit(context.expression());
        WriteLine();
        return null;
    }

    public override object? VisitAssertStatement(NovusParser.AssertStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("assert!(");
        Visit(context.expression());
        if (context.STRING_LITERAL() != null)
        {
            Write(", ");
            Write(context.STRING_LITERAL().GetText());
        }
        Write(")");
        WriteLine();
        return null;
    }

    public override object? VisitPanicStatement(NovusParser.PanicStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("panic!(");
        Write(context.STRING_LITERAL().GetText());
        Write(")");
        WriteLine();
        return null;
    }

    public override object? VisitUnsafeBlock(NovusParser.UnsafeBlockContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("unsafe ");
        Visit(context.block());
        return null;
    }

    public override object? VisitUsingStatement(NovusParser.UsingStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Write("using ");
        Visit(context.expression());
        Write(" ");
        Visit(context.block());
        return null;
    }

    public override object? VisitExpressionStatement(NovusParser.ExpressionStatementContext context)
    {
        EmitHiddenTokensBefore(context);
        Visit(context.expression());
        if (context.postfixCondition() != null)
        {
            Write(" ");
            Visit(context.postfixCondition());
        }
        WriteLine();
        return null;
    }

    // ==================== Expressions ====================

    public override object? VisitPrimaryExpr(NovusParser.PrimaryExprContext context)
    {
        Visit(context.primaryExpression());
        return null;
    }

    public override object? VisitBoolLiteral(NovusParser.BoolLiteralContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitNullLiteral(NovusParser.NullLiteralContext context)
    {
        Write("null");
        return null;
    }

    public override object? VisitSelfExpr(NovusParser.SelfExprContext context)
    {
        Write("self");
        return null;
    }

    public override object? VisitSizeofExpr(NovusParser.SizeofExprContext context)
    {
        if (context.GetText().StartsWith("@"))
        {
            Write("@sizeof(");
        }
        else
        {
            Write("sizeof(");
        }
        Visit(context.type());
        Write(")");
        return null;
    }

    public override object? VisitInterpolatedStringLiteral(NovusParser.InterpolatedStringLiteralContext context)
    {
        Write(context.F_STRING_LITERAL().GetText());
        return null;
    }

    public override object? VisitCharLiteral(NovusParser.CharLiteralContext context)
    {
        Write(context.CHAR_LITERAL().GetText());
        return null;
    }

    public override object? VisitStringLiteral(NovusParser.StringLiteralContext context)
    {
        Write(context.STRING_LITERAL().GetText());
        return null;
    }

    public override object? VisitFloatLiteral(NovusParser.FloatLiteralContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitIntegerLiteral(NovusParser.IntegerLiteralContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitBinaryLiteral(NovusParser.BinaryLiteralContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitHexLiteral(NovusParser.HexLiteralContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitIdentifierExpr(NovusParser.IdentifierExprContext context)
    {
        Visit(context.identifier());
        return null;
    }

    public override object? VisitIdentifier(NovusParser.IdentifierContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write("::");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    public override object? VisitParenExpr(NovusParser.ParenExprContext context)
    {
        Write("(");
        Visit(context.expression());
        Write(")");
        return null;
    }

    public override object? VisitUnitLiteral(NovusParser.UnitLiteralContext context)
    {
        Write("()");
        return null;
    }

    public override object? VisitTupleLiteral(NovusParser.TupleLiteralContext context)
    {
        Write("(");
        var exprs = context.expression();
        for (int i = 0; i < exprs.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(exprs[i]);
        }
        Write(")");
        return null;
    }

    public override object? VisitArrayLiteral(NovusParser.ArrayLiteralContext context)
    {
        Write("[");
        var exprs = context.expression();
        for (int i = 0; i < exprs.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(exprs[i]);
        }
        Write("]");
        return null;
    }

    public override object? VisitArrayRepeatLiteral(NovusParser.ArrayRepeatLiteralContext context)
    {
        Write("[");
        Visit(context.expression(0));
        Write("; ");
        Visit(context.expression(1));
        Write("]");
        return null;
    }

    public override object? VisitStructLiteral(NovusParser.StructLiteralContext context)
    {
        Visit(context.typeName());
        Write(" { ");
        var fields = context.structFieldInit();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(fields[i]);
        }
        Write(" }");
        return null;
    }

    public override object? VisitStructFieldInit(NovusParser.StructFieldInitContext context)
    {
        Write(context.IDENTIFIER().GetText());
        Write(": ");
        Visit(context.expression());
        return null;
    }

    public override object? VisitStructArrayInit(NovusParser.StructArrayInitContext context)
    {
        Visit(context.typeName());
        Write(" { ");
        Visit(context.arrayLiteralInit());
        Write(" }");
        return null;
    }

    public override object? VisitArrayLiteralInit(NovusParser.ArrayLiteralInitContext context)
    {
        Write("[");
        var exprs = context.expression();
        for (int i = 0; i < exprs.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(exprs[i]);
        }
        Write("]");
        return null;
    }

    public override object? VisitMatchExpr(NovusParser.MatchExprContext context)
    {
        Write("match ");
        Visit(context.expression());
        WriteLine(" {");
        Indent();

        var arms = context.matchArm();
        for (int i = 0; i < arms.Length; i++)
        {
            VisitMatchArmInternal(arms[i], isLast: i == arms.Length - 1);
        }

        Dedent();
        Write("}");
        return null;
    }

    private void VisitMatchArmInternal(NovusParser.MatchArmContext context, bool isLast)
    {
        EmitHiddenTokensBefore(context);
        Visit(context.pattern());

        if (context.KW_IF() != null)
        {
            Write(" if ");
            Visit(context.expression(0));
        }

        Write(" => ");

        if (context.block() != null)
        {
            // For blocks, we need to handle them inline without extra newline
            Write("{");
            WriteLine();
            Indent();

            var statements = context.block().statement();
            foreach (var stmt in statements)
            {
                Visit(stmt);
            }

            Dedent();
            Write("}");
            if (!isLast)
            {
                WriteLine(",");
            }
            else
            {
                WriteLine();
            }
        }
        else if (context.returnStatement() != null)
        {
            Write("return");
            if (context.returnStatement().expression() != null)
            {
                Write(" ");
                Visit(context.returnStatement().expression());
            }
            if (!isLast)
            {
                WriteLine(",");
            }
            else
            {
                WriteLine();
            }
        }
        else
        {
            // Expression - get the right one (might be index 0 or 1 depending on guard)
            var exprIndex = context.KW_IF() != null ? 1 : 0;
            Visit(context.expression(exprIndex));
            if (!isLast)
            {
                WriteLine(",");
            }
            else
            {
                WriteLine();
            }
        }
    }

    public override object? VisitMatchArm(NovusParser.MatchArmContext context)
    {
        // Default visit for match arms outside of match expressions (shouldn't happen normally)
        VisitMatchArmInternal(context, isLast: true);
        return null;
    }

    // Pattern visitors
    public override object? VisitPipePattern(NovusParser.PipePatternContext context)
    {
        Visit(context.pattern(0));
        Write(" | ");
        Visit(context.pattern(1));
        return null;
    }

    public override object? VisitReferencePattern(NovusParser.ReferencePatternContext context)
    {
        Write("&");
        Visit(context.pattern());
        return null;
    }

    public override object? VisitVariantPattern(NovusParser.VariantPatternContext context)
    {
        Visit(context.variantName());
        Write("(");
        if (context.patternList() != null)
        {
            Visit(context.patternList());
        }
        Write(")");
        return null;
    }

    public override object? VisitSimpleVariantPattern(NovusParser.SimpleVariantPatternContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write("::");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    public override object? VisitIdentifierPattern(NovusParser.IdentifierPatternContext context)
    {
        Write(context.IDENTIFIER().GetText());
        return null;
    }

    public override object? VisitMutIdentifierPattern(NovusParser.MutIdentifierPatternContext context)
    {
        Write("mut ");
        Write(context.IDENTIFIER().GetText());
        return null;
    }

    public override object? VisitLiteralPattern(NovusParser.LiteralPatternContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitBoolLiteralPattern(NovusParser.BoolLiteralPatternContext context)
    {
        Write(context.GetText());
        return null;
    }

    public override object? VisitNullLiteralPattern(NovusParser.NullLiteralPatternContext context)
    {
        Write("null");
        return null;
    }

    public override object? VisitWildcardPattern(NovusParser.WildcardPatternContext context)
    {
        Write("_");
        return null;
    }

    public override object? VisitVariantName(NovusParser.VariantNameContext context)
    {
        var identifiers = context.IDENTIFIER();
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i > 0) Write("::");
            Write(identifiers[i].GetText());
        }
        return null;
    }

    public override object? VisitPatternList(NovusParser.PatternListContext context)
    {
        var patterns = context.pattern();
        for (int i = 0; i < patterns.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(patterns[i]);
        }
        return null;
    }

    // Binary and unary expressions
    public override object? VisitTurboFishExpr(NovusParser.TurboFishExprContext context)
    {
        Visit(context.expression());
        Write("::");
        Visit(context.genericTypeArgs());
        return null;
    }

    public override object? VisitPathExpr(NovusParser.PathExprContext context)
    {
        Visit(context.expression());
        Write("::");
        Write(context.IDENTIFIER().GetText());
        return null;
    }

    public override object? VisitMemberAccessExpr(NovusParser.MemberAccessExprContext context)
    {
        Visit(context.expression());
        Write(".");
        Write(context.IDENTIFIER().GetText());
        return null;
    }

    public override object? VisitCallExpr(NovusParser.CallExprContext context)
    {
        Visit(context.expression());
        Write("(");
        if (context.argumentList() != null)
        {
            Visit(context.argumentList());
        }
        Write(")");
        return null;
    }

    public override object? VisitArgumentList(NovusParser.ArgumentListContext context)
    {
        var exprs = context.expression();
        for (int i = 0; i < exprs.Length; i++)
        {
            if (i > 0) Write(", ");
            Visit(exprs[i]);
        }
        return null;
    }

    public override object? VisitIndexExpr(NovusParser.IndexExprContext context)
    {
        Visit(context.expression(0));
        Write("[");
        Visit(context.expression(1));
        Write("]");
        return null;
    }

    public override object? VisitPostIncrementExpr(NovusParser.PostIncrementExprContext context)
    {
        Visit(context.expression());
        Write("++");
        return null;
    }

    public override object? VisitPostDecrementExpr(NovusParser.PostDecrementExprContext context)
    {
        Visit(context.expression());
        Write("--");
        return null;
    }

    public override object? VisitTryExpr(NovusParser.TryExprContext context)
    {
        Visit(context.expression());
        Write("?");
        return null;
    }

    public override object? VisitRangeExpr(NovusParser.RangeExprContext context)
    {
        Visit(context.expression(0));
        Write("..");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitRangeInclusiveExpr(NovusParser.RangeInclusiveExprContext context)
    {
        Visit(context.expression(0));
        Write("..=");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitCastExpr(NovusParser.CastExprContext context)
    {
        Write("(");
        Visit(context.type());
        Write(")");
        Visit(context.expression());
        return null;
    }

    public override object? VisitAsCastExpr(NovusParser.AsCastExprContext context)
    {
        Visit(context.expression());
        Write(" as ");
        Visit(context.type());
        return null;
    }

    public override object? VisitBorrowExpr(NovusParser.BorrowExprContext context)
    {
        Write("&");
        if (context.KW_MUT() != null) Write("mut ");
        Visit(context.expression());
        return null;
    }

    public override object? VisitUnaryExpr(NovusParser.UnaryExprContext context)
    {
        Write(context.GetChild(0).GetText());
        Visit(context.expression());
        return null;
    }

    public override object? VisitPreIncrementExpr(NovusParser.PreIncrementExprContext context)
    {
        Write("++");
        Visit(context.expression());
        return null;
    }

    public override object? VisitPreDecrementExpr(NovusParser.PreDecrementExprContext context)
    {
        Write("--");
        Visit(context.expression());
        return null;
    }

    public override object? VisitDereferenceExpr(NovusParser.DereferenceExprContext context)
    {
        Write("*");
        Visit(context.expression());
        return null;
    }

    public override object? VisitMultiplicativeExpr(NovusParser.MultiplicativeExprContext context)
    {
        Visit(context.expression(0));
        Write(" ");
        Write(context.GetChild(1).GetText());
        Write(" ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitAdditiveExpr(NovusParser.AdditiveExprContext context)
    {
        Visit(context.expression(0));
        Write(" ");
        Write(context.GetChild(1).GetText());
        Write(" ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitShiftExpr(NovusParser.ShiftExprContext context)
    {
        Visit(context.expression(0));
        Write(" ");
        if (context.LSHIFT() != null)
        {
            Write("<<");
        }
        else
        {
            Write(">>");
        }
        Write(" ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitBitwiseAndExpr(NovusParser.BitwiseAndExprContext context)
    {
        Visit(context.expression(0));
        Write(" & ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitBitwiseXorExpr(NovusParser.BitwiseXorExprContext context)
    {
        Visit(context.expression(0));
        Write(" ^ ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitBitwiseOrExpr(NovusParser.BitwiseOrExprContext context)
    {
        Visit(context.expression(0));
        Write(" | ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitComparisonExpr(NovusParser.ComparisonExprContext context)
    {
        Visit(context.expression(0));
        Write(" ");

        // Get the comparison operator
        for (int i = 0; i < context.ChildCount; i++)
        {
            var child = context.GetChild(i);
            var text = child.GetText();
            if (text == "==" || text == "!=" || text == "<" || text == ">" || text == "<=" || text == ">=")
            {
                Write(text);
                break;
            }
        }

        Write(" ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitLogicalAndExpr(NovusParser.LogicalAndExprContext context)
    {
        Visit(context.expression(0));
        Write(" && ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitLogicalOrExpr(NovusParser.LogicalOrExprContext context)
    {
        Visit(context.expression(0));
        Write(" || ");
        Visit(context.expression(1));
        return null;
    }

    public override object? VisitIfExpr(NovusParser.IfExprContext context)
    {
        Write("if ");
        Visit(context.expression());
        Write(" ");
        VisitBlockInline(context.block(0));
        Write(" else ");
        if (context.ifElseChain() != null)
        {
            Visit(context.ifElseChain());
        }
        else
        {
            VisitBlockInline(context.block(1));
        }
        return null;
    }

    public override object? VisitIfElseChain(NovusParser.IfElseChainContext context)
    {
        Write("if ");
        Visit(context.expression());
        Write(" ");
        VisitBlockInline(context.block(0));
        Write(" else ");
        if (context.ifElseChain() != null)
        {
            Visit(context.ifElseChain());
        }
        else
        {
            VisitBlockInline(context.block(1));
        }
        return null;
    }

    /// <summary>
    /// Formats a block inline (on a single line) for if-expressions.
    /// Format: { expr } or { stmt1; stmt2 }
    /// </summary>
    private void VisitBlockInline(NovusParser.BlockContext context)
    {
        Write("{ ");
        var statements = context.statement();
        for (int i = 0; i < statements.Length; i++)
        {
            if (i > 0) Write("; ");
            VisitStatementInline(statements[i]);
        }
        Write(" }");
    }

    /// <summary>
    /// Formats a statement inline (without trailing newline) for if-expression blocks.
    /// </summary>
    private void VisitStatementInline(NovusParser.StatementContext stmt)
    {
        // For expression statements, just visit the expression
        if (stmt.expressionStatement() != null)
        {
            Visit(stmt.expressionStatement().expression());
        }
        else if (stmt.returnStatement() != null)
        {
            Write("return");
            if (stmt.returnStatement().expression() != null)
            {
                Write(" ");
                Visit(stmt.returnStatement().expression());
            }
        }
        else if (stmt.breakStatement() != null)
        {
            Write("break");
        }
        else if (stmt.continueStatement() != null)
        {
            Write("continue");
        }
        else
        {
            // For other statement types, fall back to normal visit
            // (this may include newlines but handles edge cases)
            Visit(stmt);
        }
    }
}
