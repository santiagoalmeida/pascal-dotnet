namespace PascalCompiler;

public sealed class ParseError(string message, int line, int col) : Exception($"Error de sintaxis ({line}:{col}): {message}");

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens) => _tokens = tokens;

    private Token Current => _tokens[_pos];

    private Token Expect(TokenType type, string what)
    {
        if (Current.Type != type)
            throw new ParseError($"se esperaba {what}, se encontró '{Current.Text}'", Current.Line, Current.Col);
        var t = Current;
        _pos++;
        return t;
    }

    private bool Match(TokenType type)
    {
        if (Current.Type != type) return false;
        _pos++;
        return true;
    }

    public PascalProgram ParseProgram()
    {
        Expect(TokenType.ProgramKw, "'program'");
        var name = Expect(TokenType.Identifier, "un identificador").Text;
        Expect(TokenType.Semi, "';'");

        var vars = new List<VarDecl>();
        var subs = new List<SubDecl>();
        var recordTypes = new List<RecordTypeDecl>();

        while (true)
        {
            if (Current.Type == TokenType.VarKw)
            {
                vars.AddRange(ParseVarSection());
            }
            else if (Current.Type == TokenType.TypeKw)
            {
                recordTypes.AddRange(ParseTypeSection());
            }
            else if (Current.Type == TokenType.FunctionKw || Current.Type == TokenType.ProcedureKw)
            {
                subs.Add(ParseSubDecl());
            }
            else
            {
                break;
            }
        }

        var body = ParseCompoundStatement();
        Expect(TokenType.Dot, "'.' al final del programa");
        Expect(TokenType.Eof, "fin de archivo");

        return new PascalProgram(name, vars, subs, recordTypes, body);
    }

    private List<RecordTypeDecl> ParseTypeSection()
    {
        Expect(TokenType.TypeKw, "'type'");
        var decls = new List<RecordTypeDecl>();
        while (Current.Type == TokenType.Identifier)
        {
            var nameTok = Expect(TokenType.Identifier, "un identificador");
            Expect(TokenType.Eq, "'='");
            Expect(TokenType.RecordKw, "'record'");

            var fields = new List<RecordField>();
            while (Current.Type == TokenType.Identifier)
            {
                var names = new List<Token> { Expect(TokenType.Identifier, "un identificador") };
                while (Match(TokenType.Comma))
                    names.Add(Expect(TokenType.Identifier, "un identificador"));
                Expect(TokenType.Colon, "':'");
                var fieldType = ParseType();
                Expect(TokenType.Semi, "';'");
                foreach (var n in names)
                    fields.Add(new RecordField(n.Text, fieldType, n.Line, n.Col));
            }

            Expect(TokenType.End, "'end'");
            Expect(TokenType.Semi, "';'");
            decls.Add(new RecordTypeDecl(nameTok.Text, fields, nameTok.Line, nameTok.Col));
        }
        return decls;
    }

    private List<VarDecl> ParseVarSection()
    {
        Expect(TokenType.VarKw, "'var'");
        var decls = new List<VarDecl>();
        while (Current.Type == TokenType.Identifier)
        {
            var names = new List<Token> { Expect(TokenType.Identifier, "un identificador") };
            while (Match(TokenType.Comma))
                names.Add(Expect(TokenType.Identifier, "un identificador"));

            Expect(TokenType.Colon, "':'");
            var (type, array, recordType) = ParseTypeSpec();
            Expect(TokenType.Semi, "';'");

            foreach (var n in names)
                decls.Add(new VarDecl(n.Text, type, n.Line, n.Col, array, recordType));
        }
        return decls;
    }

    private PascalType ParseType()
    {
        var t = Current;
        switch (t.Type)
        {
            case TokenType.IntegerKw: _pos++; return PascalType.Integer;
            case TokenType.RealKw: _pos++; return PascalType.Real;
            case TokenType.BooleanKw: _pos++; return PascalType.Boolean;
            case TokenType.StringKw: _pos++; return PascalType.StringT;
            default:
                throw new ParseError("tipo esperado (integer, real, boolean, string)", t.Line, t.Col);
        }
    }

    private (PascalType Type, ArrayInfo? Array, string? RecordType) ParseTypeSpec()
    {
        if (Current.Type == TokenType.ArrayKw)
        {
            _pos++;
            Expect(TokenType.LBracket, "'['");
            var low = ParseIntConst();
            Expect(TokenType.DotDot, "'..'");
            var high = ParseIntConst();

            int? low2 = null, high2 = null;
            if (Match(TokenType.Comma))
            {
                low2 = ParseIntConst();
                Expect(TokenType.DotDot, "'..'");
                high2 = ParseIntConst();
            }

            Expect(TokenType.RBracket, "']'");
            Expect(TokenType.OfKw, "'of'");
            var elemType = ParseType();
            return (elemType, new ArrayInfo(elemType, low, high, low2, high2), null);
        }
        if (Current.Type == TokenType.Identifier)
        {
            // A bare identifier in type position names a previously declared record type;
            // validity is checked later, in CodeGen, where declared types are known.
            var name = Current.Text;
            _pos++;
            return (PascalType.Void, null, name);
        }
        return (ParseType(), null, null);
    }

    private int ParseIntConst()
    {
        bool neg = Match(TokenType.Minus);
        var tok = Expect(TokenType.IntLiteral, "un entero");
        var v = int.Parse(tok.Text);
        return neg ? -v : v;
    }

    private SubDecl ParseSubDecl()
    {
        bool isFunction = Current.Type == TokenType.FunctionKw;
        var kw = Expect(isFunction ? TokenType.FunctionKw : TokenType.ProcedureKw, isFunction ? "'function'" : "'procedure'");
        var name = Expect(TokenType.Identifier, "un identificador").Text;

        var parameters = new List<ParamDecl>();
        if (Match(TokenType.LParen))
        {
            if (Current.Type != TokenType.RParen)
            {
                parameters.AddRange(ParseParam());
                while (Match(TokenType.Semi))
                    parameters.AddRange(ParseParam());
            }
            Expect(TokenType.RParen, "')'");
        }

        PascalType? returnType = null;
        string? returnRecordType = null;
        if (isFunction)
        {
            Expect(TokenType.Colon, "':' con el tipo de retorno");
            if (Current.Type == TokenType.Identifier)
            {
                returnRecordType = Current.Text;
                _pos++;
            }
            else
            {
                returnType = ParseType();
            }
        }
        Expect(TokenType.Semi, "';'");

        var locals = new List<VarDecl>();
        if (Current.Type == TokenType.VarKw)
            locals = ParseVarSection();

        var body = ParseCompoundStatement();
        Expect(TokenType.Semi, "';' después de la declaración");

        return new SubDecl(name, parameters, returnType, returnRecordType, locals, body, kw.Line, kw.Col);
    }

    private List<ParamDecl> ParseParam()
    {
        bool byRef = false;
        if (Current.Type == TokenType.VarKw) { _pos++; byRef = true; }
        else if (Current.Type == TokenType.ConstKw) { _pos++; }

        var names = new List<Token> { Expect(TokenType.Identifier, "un identificador") };
        while (Match(TokenType.Comma))
            names.Add(Expect(TokenType.Identifier, "un identificador"));

        Expect(TokenType.Colon, "':'");
        var (type, array, recordType) = ParseTypeSpec();

        return names.Select(n => new ParamDecl(n.Text, type, byRef, n.Line, n.Col, array, recordType)).ToList();
    }

    private CompoundStmt ParseCompoundStatement()
    {
        Expect(TokenType.Begin, "'begin'");
        var stmts = new List<Stmt>();
        stmts.Add(ParseStatement());
        while (Match(TokenType.Semi))
        {
            if (Current.Type == TokenType.End) break;
            stmts.Add(ParseStatement());
        }
        Expect(TokenType.End, "'end'");
        return new CompoundStmt(stmts);
    }

    private Stmt ParseStatement()
    {
        switch (Current.Type)
        {
            case TokenType.Begin:
                return ParseCompoundStatement();
            case TokenType.If:
                return ParseIf();
            case TokenType.While:
                return ParseWhile();
            case TokenType.ForKw:
                return ParseFor();
            case TokenType.CaseKw:
                return ParseCase();
            case TokenType.WriteLn:
                return ParseWrite(TokenType.WriteLn, newline: true);
            case TokenType.Write:
                return ParseWrite(TokenType.Write, newline: false);
            case TokenType.ReadLnKw:
                return ParseReadLn();
            case TokenType.Identifier:
                return ParseIdentifierStatement();
            case TokenType.End:
            case TokenType.Semi:
                return new EmptyStmt();
            default:
                throw new ParseError($"instrucción inesperada '{Current.Text}'", Current.Line, Current.Col);
        }
    }

    private Stmt ParseIdentifierStatement()
    {
        var id = Expect(TokenType.Identifier, "un identificador");

        if (Current.Type == TokenType.LBracket)
        {
            _pos++;
            var indices = new List<Expr> { ParseExpr() };
            while (Match(TokenType.Comma))
                indices.Add(ParseExpr());
            Expect(TokenType.RBracket, "']'");
            Expect(TokenType.Assign, "':='");
            var value = ParseExpr();
            return new IndexedAssignStmt(id.Text, indices, value, id.Line, id.Col);
        }

        if (Current.Type == TokenType.Dot)
        {
            _pos++;
            var fieldTok = Expect(TokenType.Identifier, "un nombre de campo");
            Expect(TokenType.Assign, "':='");
            var value = ParseExpr();
            return new FieldAssignStmt(id.Text, fieldTok.Text, value, id.Line, id.Col);
        }

        if (Current.Type == TokenType.Assign)
        {
            _pos++;
            var value = ParseExpr();
            return new AssignStmt(id.Text, value, id.Line, id.Col);
        }

        var args = new List<Expr>();
        if (Match(TokenType.LParen))
        {
            if (Current.Type != TokenType.RParen)
            {
                args.Add(ParseExpr());
                while (Match(TokenType.Comma))
                    args.Add(ParseExpr());
            }
            Expect(TokenType.RParen, "')'");
        }
        return new ProcCallStmt(id.Text, args, id.Line, id.Col);
    }

    private Stmt ParseIf()
    {
        var kw = Expect(TokenType.If, "'if'");
        var cond = ParseExpr();
        Expect(TokenType.Then, "'then'");
        var thenStmt = ParseStatement();
        Stmt? elseStmt = null;
        if (Match(TokenType.Else))
            elseStmt = ParseStatement();
        return new IfStmt(cond, thenStmt, elseStmt, kw.Line, kw.Col);
    }

    private Stmt ParseWhile()
    {
        var kw = Expect(TokenType.While, "'while'");
        var cond = ParseExpr();
        Expect(TokenType.Do, "'do'");
        var body = ParseStatement();
        return new WhileStmt(cond, body, kw.Line, kw.Col);
    }

    private Stmt ParseFor()
    {
        var kw = Expect(TokenType.ForKw, "'for'");
        var varName = Expect(TokenType.Identifier, "un identificador").Text;
        Expect(TokenType.Assign, "':='");
        var start = ParseExpr();
        bool down;
        if (Match(TokenType.ToKw)) down = false;
        else { Expect(TokenType.DownToKw, "'to' o 'downto'"); down = true; }
        var end = ParseExpr();
        Expect(TokenType.Do, "'do'");
        var body = ParseStatement();
        return new ForStmt(varName, start, end, down, body, kw.Line, kw.Col);
    }

    private Stmt ParseCase()
    {
        var kw = Expect(TokenType.CaseKw, "'case'");
        var selector = ParseExpr();
        Expect(TokenType.OfKw, "'of'");

        var branches = new List<CaseBranch>();
        while (Current.Type != TokenType.End && Current.Type != TokenType.Else)
        {
            var labels = ParseCaseLabelList();
            Expect(TokenType.Colon, "':'");
            var stmt = ParseStatement();
            branches.Add(new CaseBranch(labels, stmt));
            if (!Match(TokenType.Semi)) break;
        }

        Stmt? elseStmt = null;
        if (Match(TokenType.Else))
        {
            var stmts = new List<Stmt> { ParseStatement() };
            while (Match(TokenType.Semi))
            {
                if (Current.Type == TokenType.End) break;
                stmts.Add(ParseStatement());
            }
            elseStmt = new CompoundStmt(stmts);
        }

        Expect(TokenType.End, "'end'");
        return new CaseStmt(selector, branches, elseStmt, kw.Line, kw.Col);
    }

    private List<Expr> ParseCaseLabelList()
    {
        var labels = new List<Expr> { ParseConstLiteral() };
        while (Match(TokenType.Comma))
            labels.Add(ParseConstLiteral());
        return labels;
    }

    private Expr ParseConstLiteral()
    {
        var t = Current;
        if (t.Type == TokenType.Minus)
        {
            _pos++;
            return new UnaryExpr(TokenType.Minus, ParseConstLiteral(), t.Line, t.Col);
        }
        switch (t.Type)
        {
            case TokenType.IntLiteral:
                _pos++;
                return new IntLiteralExpr(int.Parse(t.Text));
            case TokenType.StringLiteral:
                _pos++;
                return new StringLiteralExpr(t.Text);
            case TokenType.TrueKw:
                _pos++;
                return new BoolLiteralExpr(true);
            case TokenType.FalseKw:
                _pos++;
                return new BoolLiteralExpr(false);
            default:
                throw new ParseError("se esperaba una constante (entero, cadena o booleano)", t.Line, t.Col);
        }
    }

    private Stmt ParseWrite(TokenType kw, bool newline)
    {
        Expect(kw, newline ? "'writeln'" : "'write'");
        var args = new List<Expr>();
        if (Match(TokenType.LParen))
        {
            if (Current.Type != TokenType.RParen)
            {
                args.Add(ParseExpr());
                while (Match(TokenType.Comma))
                    args.Add(ParseExpr());
            }
            Expect(TokenType.RParen, "')'");
        }
        return new WriteLnStmt(args, newline);
    }

    private Stmt ParseReadLn()
    {
        Expect(TokenType.ReadLnKw, "'readln'");
        if (Match(TokenType.LParen))
            Expect(TokenType.RParen, "')'");
        return new ReadLnStmt();
    }

    // Expr -> SimpleExpr (RelOp SimpleExpr)?
    private Expr ParseExpr()
    {
        var left = ParseSimpleExpr();
        if (IsRelOp(Current.Type))
        {
            var opTok = Current;
            _pos++;
            var right = ParseSimpleExpr();
            return new BinaryExpr(opTok.Type, left, right, opTok.Line, opTok.Col);
        }
        return left;
    }

    private static bool IsRelOp(TokenType t) =>
        t is TokenType.Eq or TokenType.Neq or TokenType.Lt or TokenType.Gt or TokenType.Le or TokenType.Ge;

    // SimpleExpr -> Term (('+'|'-'|'or') Term)*
    private Expr ParseSimpleExpr()
    {
        var left = ParseTerm();
        while (Current.Type is TokenType.Plus or TokenType.Minus or TokenType.Or)
        {
            var opTok = Current;
            _pos++;
            var right = ParseTerm();
            left = new BinaryExpr(opTok.Type, left, right, opTok.Line, opTok.Col);
        }
        return left;
    }

    // Term -> Factor (('*'|'/'|'div'|'mod'|'and') Factor)*
    private Expr ParseTerm()
    {
        var left = ParseFactor();
        while (Current.Type is TokenType.Star or TokenType.Slash or TokenType.Div or TokenType.Mod or TokenType.And)
        {
            var opTok = Current;
            _pos++;
            var right = ParseFactor();
            left = new BinaryExpr(opTok.Type, left, right, opTok.Line, opTok.Col);
        }
        return left;
    }

    private Expr ParseFactor()
    {
        var t = Current;
        switch (t.Type)
        {
            case TokenType.IntLiteral:
                _pos++;
                return new IntLiteralExpr(int.Parse(t.Text));
            case TokenType.RealLiteral:
                _pos++;
                return new RealLiteralExpr(double.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture));
            case TokenType.StringLiteral:
                _pos++;
                return new StringLiteralExpr(t.Text);
            case TokenType.TrueKw:
                _pos++;
                return new BoolLiteralExpr(true);
            case TokenType.FalseKw:
                _pos++;
                return new BoolLiteralExpr(false);
            case TokenType.Identifier:
            {
                _pos++;
                if (Match(TokenType.LParen))
                {
                    var args = new List<Expr>();
                    if (Current.Type != TokenType.RParen)
                    {
                        args.Add(ParseExpr());
                        while (Match(TokenType.Comma))
                            args.Add(ParseExpr());
                    }
                    Expect(TokenType.RParen, "')'");
                    return new FuncCallExpr(t.Text, args, t.Line, t.Col);
                }
                if (Match(TokenType.LBracket))
                {
                    var indices = new List<Expr> { ParseExpr() };
                    while (Match(TokenType.Comma))
                        indices.Add(ParseExpr());
                    Expect(TokenType.RBracket, "']'");
                    return new IndexExpr(t.Text, indices, t.Line, t.Col);
                }
                if (Match(TokenType.Dot))
                {
                    var fieldTok = Expect(TokenType.Identifier, "un nombre de campo");
                    return new FieldAccessExpr(t.Text, fieldTok.Text, t.Line, t.Col);
                }
                return new VarExpr(t.Text, t.Line, t.Col);
            }
            case TokenType.LParen:
                _pos++;
                var inner = ParseExpr();
                Expect(TokenType.RParen, "')'");
                return inner;
            case TokenType.Not:
                _pos++;
                return new UnaryExpr(TokenType.Not, ParseFactor(), t.Line, t.Col);
            case TokenType.Minus:
                _pos++;
                return new UnaryExpr(TokenType.Minus, ParseFactor(), t.Line, t.Col);
            default:
                throw new ParseError($"se esperaba una expresión, se encontró '{t.Text}'", t.Line, t.Col);
        }
    }
}
