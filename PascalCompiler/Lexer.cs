using System.Text;

namespace PascalCompiler;

public sealed class LexError(string message, int line, int col) : Exception($"Error léxico ({line}:{col}): {message}");

public sealed class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["program"] = TokenType.ProgramKw,
        ["var"] = TokenType.VarKw,
        ["begin"] = TokenType.Begin,
        ["end"] = TokenType.End,
        ["if"] = TokenType.If,
        ["then"] = TokenType.Then,
        ["else"] = TokenType.Else,
        ["while"] = TokenType.While,
        ["do"] = TokenType.Do,
        ["writeln"] = TokenType.WriteLn,
        ["write"] = TokenType.Write,
        ["integer"] = TokenType.IntegerKw,
        ["boolean"] = TokenType.BooleanKw,
        ["string"] = TokenType.StringKw,
        ["real"] = TokenType.RealKw,
        ["true"] = TokenType.TrueKw,
        ["false"] = TokenType.FalseKw,
        ["div"] = TokenType.Div,
        ["mod"] = TokenType.Mod,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["not"] = TokenType.Not,
        ["for"] = TokenType.ForKw,
        ["to"] = TokenType.ToKw,
        ["downto"] = TokenType.DownToKw,
        ["function"] = TokenType.FunctionKw,
        ["procedure"] = TokenType.ProcedureKw,
        ["case"] = TokenType.CaseKw,
        ["of"] = TokenType.OfKw,
        ["const"] = TokenType.ConstKw,
        ["readln"] = TokenType.ReadLnKw,
        ["array"] = TokenType.ArrayKw,
        ["type"] = TokenType.TypeKw,
        ["record"] = TokenType.RecordKw,
        ["class"] = TokenType.ClassKw,
    };

    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public Lexer(string source) => _src = source;

    private char Current => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek(int offset = 1) => _pos + offset < _src.Length ? _src[_pos + offset] : '\0';

    private void Advance()
    {
        if (Current == '\n') { _line++; _col = 1; } else { _col++; }
        _pos++;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipTrivia();
            if (_pos >= _src.Length)
            {
                tokens.Add(new Token(TokenType.Eof, "", _line, _col));
                break;
            }

            int line = _line, col = _col;
            char c = Current;

            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifierOrKeyword(line, col));
            }
            else if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(line, col));
            }
            else if (c == '\'')
            {
                tokens.Add(ReadString(line, col));
            }
            else
            {
                tokens.Add(ReadSymbol(line, col));
            }
        }
        return tokens;
    }

    private void SkipTrivia()
    {
        while (true)
        {
            while (char.IsWhiteSpace(Current)) Advance();

            if (Current == '{')
            {
                while (Current != '}' && Current != '\0') Advance();
                if (Current == '}') Advance();
                continue;
            }

            if (Current == '/' && Peek() == '/')
            {
                while (Current != '\n' && Current != '\0') Advance();
                continue;
            }

            break;
        }
    }

    private Token ReadIdentifierOrKeyword(int line, int col)
    {
        var sb = new StringBuilder();
        while (char.IsLetterOrDigit(Current) || Current == '_')
        {
            sb.Append(Current);
            Advance();
        }
        var text = sb.ToString();
        var type = Keywords.TryGetValue(text, out var kw) ? kw : TokenType.Identifier;
        return new Token(type, text, line, col);
    }

    private Token ReadNumber(int line, int col)
    {
        var sb = new StringBuilder();
        bool isReal = false;
        while (char.IsDigit(Current))
        {
            sb.Append(Current);
            Advance();
        }
        if (Current == '.' && char.IsDigit(Peek()))
        {
            isReal = true;
            sb.Append(Current);
            Advance();
            while (char.IsDigit(Current))
            {
                sb.Append(Current);
                Advance();
            }
        }
        return new Token(isReal ? TokenType.RealLiteral : TokenType.IntLiteral, sb.ToString(), line, col);
    }

    private Token ReadString(int line, int col)
    {
        Advance(); // opening quote
        var sb = new StringBuilder();
        while (true)
        {
            if (Current == '\0') throw new LexError("cadena sin cerrar", line, col);
            if (Current == '\'' && Peek() == '\'')
            {
                sb.Append('\'');
                Advance();
                Advance();
                continue;
            }
            if (Current == '\'')
            {
                Advance();
                break;
            }
            sb.Append(Current);
            Advance();
        }
        return new Token(TokenType.StringLiteral, sb.ToString(), line, col);
    }

    private Token ReadSymbol(int line, int col)
    {
        char c = Current;
        switch (c)
        {
            case ';': Advance(); return new Token(TokenType.Semi, ";", line, col);
            case ',': Advance(); return new Token(TokenType.Comma, ",", line, col);
            case '.':
                Advance();
                if (Current == '.') { Advance(); return new Token(TokenType.DotDot, "..", line, col); }
                return new Token(TokenType.Dot, ".", line, col);
            case '+': Advance(); return new Token(TokenType.Plus, "+", line, col);
            case '-': Advance(); return new Token(TokenType.Minus, "-", line, col);
            case '*': Advance(); return new Token(TokenType.Star, "*", line, col);
            case '/': Advance(); return new Token(TokenType.Slash, "/", line, col);
            case '(': Advance(); return new Token(TokenType.LParen, "(", line, col);
            case ')': Advance(); return new Token(TokenType.RParen, ")", line, col);
            case '[': Advance(); return new Token(TokenType.LBracket, "[", line, col);
            case ']': Advance(); return new Token(TokenType.RBracket, "]", line, col);
            case '=': Advance(); return new Token(TokenType.Eq, "=", line, col);
            case ':':
                Advance();
                if (Current == '=') { Advance(); return new Token(TokenType.Assign, ":=", line, col); }
                return new Token(TokenType.Colon, ":", line, col);
            case '<':
                Advance();
                if (Current == '>') { Advance(); return new Token(TokenType.Neq, "<>", line, col); }
                if (Current == '=') { Advance(); return new Token(TokenType.Le, "<=", line, col); }
                return new Token(TokenType.Lt, "<", line, col);
            case '>':
                Advance();
                if (Current == '=') { Advance(); return new Token(TokenType.Ge, ">=", line, col); }
                return new Token(TokenType.Gt, ">", line, col);
            default:
                throw new LexError($"carácter inesperado '{c}'", line, col);
        }
    }
}
