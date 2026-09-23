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
        var classTypes = new List<ClassTypeDecl>();
        var methodImpls = new List<MethodImplDecl>();
        var ctorImpls = new List<ClassCtorImplDecl>();

        while (true)
        {
            if (Current.Type == TokenType.VarKw)
            {
                vars.AddRange(ParseVarSection());
            }
            else if (Current.Type == TokenType.TypeKw)
            {
                ParseTypeSection(recordTypes, classTypes);
            }
            else if (Current.Type == TokenType.FunctionKw || Current.Type == TokenType.ProcedureKw)
            {
                ParseFunctionOrMethod(subs, methodImpls);
            }
            else if (Current.Type == TokenType.ConstructorKw)
            {
                ctorImpls.Add(ParseCtorImpl());
            }
            else
            {
                break;
            }
        }

        var body = ParseCompoundStatement();
        Expect(TokenType.Dot, "'.' al final del programa");
        Expect(TokenType.Eof, "fin de archivo");

        return new PascalProgram(name, vars, subs, recordTypes, classTypes, methodImpls, ctorImpls, body);
    }

    private void ParseTypeSection(List<RecordTypeDecl> recordTypes, List<ClassTypeDecl> classTypes)
    {
        Expect(TokenType.TypeKw, "'type'");
        while (Current.Type == TokenType.Identifier)
        {
            var nameTok = Expect(TokenType.Identifier, "un identificador");
            Expect(TokenType.Eq, "'='");

            if (Match(TokenType.RecordKw))
            {
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
                recordTypes.Add(new RecordTypeDecl(nameTok.Text, fields, nameTok.Line, nameTok.Col));
            }
            else if (Match(TokenType.ClassKw))
            {
                string? parentName = null;
                if (Match(TokenType.LParen))
                {
                    parentName = Expect(TokenType.Identifier, "el nombre de la clase base").Text;
                    Expect(TokenType.RParen, "')'");
                }

                var fields = new List<ClassField>();
                var methods = new List<ClassMethodSig>();
                ClassCtorSig? ctorSig = null;
                bool isPrivate = false;

                while (Current.Type == TokenType.Identifier
                    || Current.Type == TokenType.FunctionKw
                    || Current.Type == TokenType.ProcedureKw
                    || Current.Type == TokenType.ConstructorKw
                    || Current.Type == TokenType.PrivateKw
                    || Current.Type == TokenType.PublicKw)
                {
                    if (Current.Type == TokenType.PrivateKw) { _pos++; isPrivate = true; continue; }
                    if (Current.Type == TokenType.PublicKw) { _pos++; isPrivate = false; continue; }

                    if (Current.Type == TokenType.ConstructorKw)
                    {
                        var ckw = Current;
                        _pos++;
                        var cname = Expect(TokenType.Identifier, "'Create'").Text;
                        if (!string.Equals(cname, "Create", StringComparison.OrdinalIgnoreCase))
                            throw new ParseError("el único constructor soportado se llama 'Create'", ckw.Line, ckw.Col);
                        if (ctorSig is not null)
                            throw new ParseError("ya se declaró un constructor 'Create' en esta clase", ckw.Line, ckw.Col);
                        var cparams = ParseParamList();
                        Expect(TokenType.Semi, "';'");
                        ctorSig = new ClassCtorSig(cparams, ckw.Line, ckw.Col);
                        continue;
                    }

                    if (Current.Type == TokenType.FunctionKw || Current.Type == TokenType.ProcedureKw)
                    {
                        bool isFunc = Current.Type == TokenType.FunctionKw;
                        var mkw = Current;
                        _pos++;
                        var mname = Expect(TokenType.Identifier, "un identificador").Text;
                        var mparams = ParseParamList();
                        PascalType? mret = null;
                        if (isFunc)
                        {
                            Expect(TokenType.Colon, "':' con el tipo de retorno");
                            mret = ParseType();
                        }
                        Expect(TokenType.Semi, "';'");

                        bool isVirtual = false, isOverride = false;
                        if (Match(TokenType.VirtualKw)) { isVirtual = true; Expect(TokenType.Semi, "';'"); }
                        else if (Match(TokenType.OverrideKw)) { isOverride = true; Expect(TokenType.Semi, "';'"); }

                        methods.Add(new ClassMethodSig(mname, mparams, mret, mkw.Line, mkw.Col, isPrivate, isVirtual, isOverride));
                    }
                    else
                    {
                        var names = new List<Token> { Expect(TokenType.Identifier, "un identificador") };
                        while (Match(TokenType.Comma))
                            names.Add(Expect(TokenType.Identifier, "un identificador"));
                        Expect(TokenType.Colon, "':'");
                        var (fieldType, fieldRecordType) = ParseClassFieldType();
                        Expect(TokenType.Semi, "';'");
                        foreach (var n in names)
                            fields.Add(new ClassField(n.Text, fieldType, n.Line, n.Col, isPrivate, fieldRecordType));
                    }
                }
                Expect(TokenType.End, "'end'");
                Expect(TokenType.Semi, "';'");
                classTypes.Add(new ClassTypeDecl(nameTok.Text, parentName, fields, methods, ctorSig, nameTok.Line, nameTok.Col));
            }
            else
            {
                throw new ParseError("se esperaba 'record' o 'class'", Current.Line, Current.Col);
            }
        }
    }

    // A class field's type: either a simple scalar type, or a bare identifier naming
    // another record/class type (composition) — no arrays as class fields.
    private (PascalType Type, string? RecordType) ParseClassFieldType()
    {
        if (Current.Type == TokenType.Identifier)
        {
            var name = Current.Text;
            _pos++;
            return (PascalType.Void, name);
        }
        return (ParseType(), null);
    }

    private List<ParamDecl> ParseParamList()
    {
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
        return parameters;
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

    // Parses either a free function/procedure, or a class method implementation
    // ((function|procedure) ClassName.MethodName(...)), appending to the matching list.
    private void ParseFunctionOrMethod(List<SubDecl> subs, List<MethodImplDecl> methodImpls)
    {
        bool isFunction = Current.Type == TokenType.FunctionKw;
        var kw = Expect(isFunction ? TokenType.FunctionKw : TokenType.ProcedureKw, isFunction ? "'function'" : "'procedure'");
        var firstName = Expect(TokenType.Identifier, "un identificador").Text;

        if (Match(TokenType.Dot))
        {
            var methodName = Expect(TokenType.Identifier, "un nombre de método").Text;
            var mParams = ParseParamList();

            PascalType? mReturnType = null;
            if (isFunction)
            {
                Expect(TokenType.Colon, "':' con el tipo de retorno");
                mReturnType = ParseType();
            }
            Expect(TokenType.Semi, "';'");

            var mLocals = new List<VarDecl>();
            if (Current.Type == TokenType.VarKw)
                mLocals = ParseVarSection();

            var mBody = ParseCompoundStatement();
            Expect(TokenType.Semi, "';' después de la declaración");

            methodImpls.Add(new MethodImplDecl(firstName, methodName, mParams, mReturnType, mLocals, mBody, kw.Line, kw.Col));
            return;
        }

        var parameters = ParseParamList();

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

        subs.Add(new SubDecl(firstName, parameters, returnType, returnRecordType, locals, body, kw.Line, kw.Col));
    }

    // constructor ClassName.Create(...); [var ...] begin ... end;
    private ClassCtorImplDecl ParseCtorImpl()
    {
        var kw = Expect(TokenType.ConstructorKw, "'constructor'");
        var className = Expect(TokenType.Identifier, "un identificador").Text;
        Expect(TokenType.Dot, "'.'");
        var ctorName = Expect(TokenType.Identifier, "'Create'").Text;
        if (!string.Equals(ctorName, "Create", StringComparison.OrdinalIgnoreCase))
            throw new ParseError("el único constructor soportado se llama 'Create'", kw.Line, kw.Col);

        var cParams = ParseParamList();
        Expect(TokenType.Semi, "';'");

        var cLocals = new List<VarDecl>();
        if (Current.Type == TokenType.VarKw)
            cLocals = ParseVarSection();

        var cBody = ParseCompoundStatement();
        Expect(TokenType.Semi, "';' después de la declaración");

        return new ClassCtorImplDecl(className, cParams, cLocals, cBody, kw.Line, kw.Col);
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
            case TokenType.InheritedKw:
                return ParseInheritedStmt();
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
            var memberTok = Expect(TokenType.Identifier, "un nombre de campo o método");
            if (Match(TokenType.LParen))
            {
                var qArgs = new List<Expr>();
                if (Current.Type != TokenType.RParen)
                {
                    qArgs.Add(ParseExpr());
                    while (Match(TokenType.Comma))
                        qArgs.Add(ParseExpr());
                }
                Expect(TokenType.RParen, "')'");
                return new QualifiedCallStmt(id.Text, memberTok.Text, qArgs, id.Line, id.Col);
            }
            Expect(TokenType.Assign, "':=' o '('");
            var value = ParseExpr();
            return new FieldAssignStmt(id.Text, memberTok.Text, value, id.Line, id.Col);
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

    private Stmt ParseInheritedStmt()
    {
        var kw = Expect(TokenType.InheritedKw, "'inherited'");
        var member = Expect(TokenType.Identifier, "un nombre de método").Text;
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
        return new InheritedCallStmt(member, args, kw.Line, kw.Col);
    }

    private Expr ParseInheritedExpr()
    {
        var kw = Expect(TokenType.InheritedKw, "'inherited'");
        var member = Expect(TokenType.Identifier, "un nombre de método").Text;
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
        return new InheritedCallExpr(member, args, kw.Line, kw.Col);
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
            case TokenType.InheritedKw:
                return ParseInheritedExpr();
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
                    var memberTok = Expect(TokenType.Identifier, "un nombre de campo o método");
                    if (Match(TokenType.LParen))
                    {
                        var qArgs = new List<Expr>();
                        if (Current.Type != TokenType.RParen)
                        {
                            qArgs.Add(ParseExpr());
                            while (Match(TokenType.Comma))
                                qArgs.Add(ParseExpr());
                        }
                        Expect(TokenType.RParen, "')'");
                        return new QualifiedCallExpr(t.Text, memberTok.Text, qArgs, t.Line, t.Col);
                    }
                    return new FieldAccessExpr(t.Text, memberTok.Text, t.Line, t.Col);
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
