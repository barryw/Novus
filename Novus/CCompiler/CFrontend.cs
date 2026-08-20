using Novus.IR;

namespace Novus.CCompiler;

/// <summary>Small C99 frontend that lowers supported C directly into Novus IR.</summary>
public sealed class CFrontend
{
    private readonly List<Token> _tokens;
    private int _position;
    private int _temporary;
    private IrBasicBlock? _block;
    private Dictionary<string, IrType> _variables = new(StringComparer.Ordinal);
    private Dictionary<string, IrValue> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (IrType ReturnType, List<IrType> Parameters)> _functions = new(StringComparer.Ordinal);

    private readonly record struct Token(string Text, int Line);

    public CFrontend(string source) => _tokens = Lex(source);

    public IrModule Parse()
    {
        var module = new IrModule();
        while (!AtEnd) module.AddFunction(ParseFunction());
        return module;
    }

    private IrFunction ParseFunction()
    {
        var returnType = ParseType();
        var name = Identifier();
        Expect("(");
        var function = new IrFunction(name, returnType, Visibility.Public);
        _variables = new Dictionary<string, IrType>(StringComparer.Ordinal);
        _values = new Dictionary<string, IrValue>(StringComparer.Ordinal);
        if (!Match(")"))
        {
            if (Peek("void") && Peek(1, ")")) { Take(); Expect(")"); }
            else
            {
                do
                {
                    var type = ParseType();
                    var parameterName = Identifier();
                    function.Parameters.Add(new IrParameter(parameterName, type));
                    _variables.Add(parameterName, type);
                } while (Match(","));
                Expect(")");
            }
        }
        _functions[name] = (returnType, function.Parameters.Select(parameter => parameter.Type).ToList());
        if (Match(";"))
        {
            function.IsExtern = true;
            return function;
        }
        Expect("{");
        _block = function.CreateBasicBlock("entry");
        var returned = false;
        while (!Peek("}"))
        {
            if (Match("return"))
            {
                var value = returnType == IrVoidType.Instance ? null : Expression();
                Expect(";");
                _block.AddInstruction(new IrReturn(value));
                returned = true;
                continue;
            }
            if (IsTypeStart(Current.Text))
            {
                var type = ParseType();
                var localName = Identifier();
                if (!Match("=")) Fail("uninitialized locals are not supported yet");
                var initialValue = Expression();
                Expect(";");
                _variables.Add(localName, type);
                _values.Add(localName, initialValue);
                continue;
            }
            var assignedName = Identifier();
            if (!_variables.TryGetValue(assignedName, out _)) Fail($"unknown identifier '{assignedName}'");
            Expect("=");
            var assignedValue = Expression();
            Expect(";");
            _values[assignedName] = assignedValue;
        }
        if (!returned && returnType == IrVoidType.Instance)
            _block.AddInstruction(new IrReturn(null));
        else if (!returned)
            Fail("expected return statement");
        Expect("}");
        _block = null;
        return function;
    }

    private IrValue Expression(int minimumPrecedence = 0)
    {
        var left = Primary();
        while (BinaryOperator(Current.Text, out var operation, out var precedence) && precedence >= minimumPrecedence)
        {
            Take();
            var right = Expression(precedence + 1);
            var resultType = operation >= IrBinaryOp.OpKind.Eq ? IrBoolType.Instance : left.Type;
            var name = $"c_tmp_{_temporary++}";
            _block!.AddInstruction(new IrBinaryOp(name, operation, left, right, resultType));
            left = new IrVariable(name, resultType);
        }
        return left;
    }

    private IrValue Primary()
    {
        if (Match("(")) { var value = Expression(); Expect(")"); return value; }
        if (Match("-"))
        {
            var value = Primary();
            var name = $"c_tmp_{_temporary++}";
            _block!.AddInstruction(new IrBinaryOp(name, IrBinaryOp.OpKind.Sub,
                new IrConstant(0, value.Type), value, value.Type));
            return new IrVariable(name, value.Type);
        }
        var token = Take();
        if (long.TryParse(token.Text, out var number)) return new IrConstant(number, IrIntType.I32);
        if (Peek("("))
        {
            if (!_functions.TryGetValue(token.Text, out var signature))
                throw Error(token, $"function '{token.Text}' must be declared before use");
            Take();
            var arguments = new List<IrValue>();
            if (!Match(")"))
            {
                do arguments.Add(Expression()); while (Match(","));
                Expect(")");
            }
            if (arguments.Count != signature.Parameters.Count)
                throw Error(token, $"function '{token.Text}' expects {signature.Parameters.Count} arguments, got {arguments.Count}");
            var resultName = signature.ReturnType == IrVoidType.Instance ? null : $"c_tmp_{_temporary++}";
            var call = new IrCall(token.Text, signature.ReturnType, resultName);
            call.Arguments.AddRange(arguments);
            _block!.AddInstruction(call);
            return resultName == null
                ? throw Error(token, "void function cannot be used as a value")
                : new IrVariable(resultName, signature.ReturnType);
        }
        if (_values.TryGetValue(token.Text, out var currentValue)) return currentValue;
        if (_variables.TryGetValue(token.Text, out var type)) return new IrVariable(token.Text, type);
        throw Error(token, $"unknown identifier '{token.Text}'");
    }

    private IrType ParseType()
    {
        var unsigned = Match("unsigned");
        Match("signed");
        var token = Take();
        return token.Text switch
        {
            "void" when !unsigned => IrVoidType.Instance,
            "char" => unsigned ? IrIntType.U8 : IrIntType.I8,
            "short" => unsigned ? IrIntType.U16 : IrIntType.I16,
            "int" or "long" => unsigned ? IrIntType.U32 : IrIntType.I32,
            _ => throw Error(token, $"unsupported C type '{token.Text}'")
        };
    }

    private static bool IsTypeStart(string text) =>
        text is "unsigned" or "signed" or "void" or "char" or "short" or "int" or "long";

    private static bool BinaryOperator(string text, out IrBinaryOp.OpKind operation, out int precedence)
    {
        (operation, precedence) = text switch
        {
            "*" => (IrBinaryOp.OpKind.Mul, 6), "/" => (IrBinaryOp.OpKind.Div, 6), "%" => (IrBinaryOp.OpKind.Mod, 6),
            "+" => (IrBinaryOp.OpKind.Add, 5), "-" => (IrBinaryOp.OpKind.Sub, 5),
            "<" => (IrBinaryOp.OpKind.Lt, 4), "<=" => (IrBinaryOp.OpKind.Le, 4),
            ">" => (IrBinaryOp.OpKind.Gt, 4), ">=" => (IrBinaryOp.OpKind.Ge, 4),
            "==" => (IrBinaryOp.OpKind.Eq, 3), "!=" => (IrBinaryOp.OpKind.Ne, 3),
            "&" => (IrBinaryOp.OpKind.And, 2), "^" => (IrBinaryOp.OpKind.Xor, 1), "|" => (IrBinaryOp.OpKind.Or, 0),
            _ => default
        };
        return text is "*" or "/" or "%" or "+" or "-" or "<" or "<=" or ">" or ">=" or "==" or "!=" or "&" or "^" or "|";
    }

    private string Identifier()
    {
        var token = Take();
        if (token.Text.Length == 0 || !(char.IsLetter(token.Text[0]) || token.Text[0] == '_'))
            throw Error(token, $"expected identifier, found '{token.Text}'");
        return token.Text;
    }

    private bool AtEnd => _position >= _tokens.Count;
    private Token Current => AtEnd ? new Token("<end>", _tokens.Count == 0 ? 1 : _tokens[^1].Line) : _tokens[_position];
    private bool Peek(string text) => Current.Text == text;
    private bool Peek(int offset, string text) => _position + offset < _tokens.Count && _tokens[_position + offset].Text == text;
    private Token Take() { if (AtEnd) Fail("unexpected end of file"); return _tokens[_position++]; }
    private bool Match(string text) { if (!Peek(text)) return false; _position++; return true; }
    private void Expect(string text) { if (!Match(text)) Fail($"expected '{text}', found '{Current.Text}'"); }
    private void Fail(string message) => throw Error(Current, message);
    private static FormatException Error(Token token, string message) => new($"line {token.Line}: {message}");

    private static List<Token> Lex(string source)
    {
        var tokens = new List<Token>();
        var line = 1;
        for (var index = 0; index < source.Length;)
        {
            if (char.IsWhiteSpace(source[index])) { if (source[index++] == '\n') line++; continue; }
            if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '/')
            { while (index < source.Length && source[index] != '\n') index++; continue; }
            if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/'))
                { if (source[index++] == '\n') line++; }
                if (index + 1 >= source.Length) throw new FormatException($"line {line}: unterminated comment");
                index += 2; continue;
            }
            var start = index;
            if (char.IsLetter(source[index]) || source[index] == '_')
                while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_')) index++;
            else if (char.IsDigit(source[index]))
                while (index < source.Length && char.IsDigit(source[index])) index++;
            else
            {
                index++;
                if (index < source.Length && source[start..(index + 1)] is "==" or "!=" or "<=" or ">=") index++;
            }
            tokens.Add(new Token(source[start..index], line));
        }
        return tokens;
    }
}
