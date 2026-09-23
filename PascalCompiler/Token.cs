namespace PascalCompiler;

public enum TokenType
{
    ProgramKw, VarKw, Begin, End, If, Then, Else, While, Do, WriteLn, Write,
    IntegerKw, BooleanKw, StringKw, RealKw, TrueKw, FalseKw,
    Div, Mod, And, Or, Not,
    ForKw, ToKw, DownToKw, FunctionKw, ProcedureKw, CaseKw, OfKw, ConstKw, ReadLnKw, ArrayKw,
    TypeKw, RecordKw, ClassKw,
    PrivateKw, PublicKw, ConstructorKw, VirtualKw, OverrideKw, InheritedKw,

    Semi, Colon, Comma, Dot, DotDot, Assign,
    Plus, Minus, Star, Slash, LParen, RParen, LBracket, RBracket,
    Eq, Neq, Lt, Gt, Le, Ge,

    IntLiteral, RealLiteral, StringLiteral, Identifier,

    Eof
}

public readonly record struct Token(TokenType Type, string Text, int Line, int Col);
