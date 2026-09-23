namespace PascalCompiler;

public enum PascalType { Integer, Real, Boolean, StringT, Void }

public sealed record ArrayInfo(PascalType ElementType, int Low, int High, int? Low2 = null, int? High2 = null)
{
    public bool Is2D => Low2.HasValue;
}

public sealed record RecordField(string Name, PascalType Type, int Line, int Col);
public sealed record RecordTypeDecl(string Name, List<RecordField> Fields, int Line, int Col);

// ClassField.RecordType: like VarDecl.RecordType, an optional named-type reference
// (record or class) for composition fields — e.g. "logger: TLogger" inside a class.
public sealed record ClassField(string Name, PascalType Type, int Line, int Col, bool IsPrivate = false, string? RecordType = null);
public sealed record ClassMethodSig(
    string Name,
    List<ParamDecl> Params,
    PascalType? ReturnType,
    int Line,
    int Col,
    bool IsPrivate = false,
    bool IsVirtual = false,
    bool IsOverride = false);
public sealed record ClassCtorSig(List<ParamDecl> Params, int Line, int Col);

public sealed record ClassTypeDecl(
    string Name,
    string? ParentName,
    List<ClassField> Fields,
    List<ClassMethodSig> Methods,
    ClassCtorSig? Ctor,
    int Line,
    int Col);

public sealed record MethodImplDecl(
    string ClassName,
    string MethodName,
    List<ParamDecl> Params,
    PascalType? ReturnType,
    List<VarDecl> Locals,
    CompoundStmt Body,
    int Line,
    int Col);

public sealed record ClassCtorImplDecl(
    string ClassName,
    List<ParamDecl> Params,
    List<VarDecl> Locals,
    CompoundStmt Body,
    int Line,
    int Col);

public abstract record Expr;

public sealed record IntLiteralExpr(int Value) : Expr;
public sealed record RealLiteralExpr(double Value) : Expr;
public sealed record StringLiteralExpr(string Value) : Expr;
public sealed record BoolLiteralExpr(bool Value) : Expr;
public sealed record VarExpr(string Name, int Line, int Col) : Expr;
public sealed record IndexExpr(string ArrayName, List<Expr> Indices, int Line, int Col) : Expr;
public sealed record FieldAccessExpr(string RecordVarName, string FieldName, int Line, int Col) : Expr;
public sealed record UnaryExpr(TokenType Op, Expr Operand, int Line, int Col) : Expr;
public sealed record BinaryExpr(TokenType Op, Expr Left, Expr Right, int Line, int Col) : Expr;
public sealed record FuncCallExpr(string Name, List<Expr> Args, int Line, int Col) : Expr;

// obj.Metodo(args) as a value, or TClase.Create() to construct an instance.
// Which one it is (constructor vs. instance method call) is resolved semantically,
// by checking whether Target names a declared class type or a variable.
public sealed record QualifiedCallExpr(string Target, string Member, List<Expr> Args, int Line, int Col) : Expr;

// inherited MethodName(args) — calls the base class's implementation directly
// (non-virtual dispatch), from within an overriding method.
public sealed record InheritedCallExpr(string Member, List<Expr> Args, int Line, int Col) : Expr;

public abstract record Stmt;

public sealed record AssignStmt(string Name, Expr Value, int Line, int Col) : Stmt;
public sealed record IndexedAssignStmt(string ArrayName, List<Expr> Indices, Expr Value, int Line, int Col) : Stmt;
public sealed record FieldAssignStmt(string RecordVarName, string FieldName, Expr Value, int Line, int Col) : Stmt;
public sealed record IfStmt(Expr Condition, Stmt Then, Stmt? Else, int Line, int Col) : Stmt;
public sealed record WhileStmt(Expr Condition, Stmt Body, int Line, int Col) : Stmt;
public sealed record ForStmt(string VarName, Expr Start, Expr End, bool Down, Stmt Body, int Line, int Col) : Stmt;
public sealed record CompoundStmt(List<Stmt> Statements) : Stmt;
public sealed record WriteLnStmt(List<Expr> Args, bool Newline) : Stmt;
public sealed record ReadLnStmt : Stmt;
public sealed record ProcCallStmt(string Name, List<Expr> Args, int Line, int Col) : Stmt;
public sealed record QualifiedCallStmt(string Target, string Member, List<Expr> Args, int Line, int Col) : Stmt;
public sealed record InheritedCallStmt(string Member, List<Expr> Args, int Line, int Col) : Stmt;
public sealed record EmptyStmt : Stmt;

public sealed record CaseBranch(List<Expr> Labels, Stmt Body);
public sealed record CaseStmt(Expr Selector, List<CaseBranch> Branches, Stmt? ElseBranch, int Line, int Col) : Stmt;

// RecordType is a generic "named type" reference at the AST level: it holds an
// identifier that names either a record or a class type. Which one it is gets
// resolved later, in CodeGen, where declared types are known.
public sealed record VarDecl(string Name, PascalType Type, int Line, int Col, ArrayInfo? Array = null, string? RecordType = null);
public sealed record ParamDecl(string Name, PascalType Type, bool ByRef, int Line, int Col, ArrayInfo? Array = null, string? RecordType = null);

public sealed record SubDecl(
    string Name,
    List<ParamDecl> Params,
    PascalType? ReturnType,
    string? ReturnRecordType,
    List<VarDecl> Locals,
    CompoundStmt Body,
    int Line,
    int Col);

public sealed record PascalProgram(
    string Name,
    List<VarDecl> Vars,
    List<SubDecl> Subs,
    List<RecordTypeDecl> RecordTypes,
    List<ClassTypeDecl> ClassTypes,
    List<MethodImplDecl> MethodImpls,
    List<ClassCtorImplDecl> CtorImpls,
    CompoundStmt Body);
