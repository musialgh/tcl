namespace TqlInterpreter;

public enum Tk
{
    // literals
    Number, StrPat, StrLit, BracketIdent,

    // bare identifier
    Ident,

    // keywords (alphabetical within groups)
    KwAbort, KwAdd, KwAnd, KwAs, KwAsc,
    KwBreak, KwBy,
    KwCall, KwColumn, KwConcat, KwContinue, KwCreate,
    KwDecimal, KwDefault, KwDesc, KwDistinct,
    KwElse,
    KwFalse, KwFor, KwFrom, KwFunction,
    KwGlobal, KwGroup, KwGuid,
    KwIf, KwImport, KwIn, KwInsert, KwInto, KwIs,
    KwLet, KwLimit, KwLoad,
    KwNot, KwNull,
    KwOn, KwOr, KwOrder,
    KwReturn, KwRow,
    KwSave, KwSelect, KwSet,
    KwTable, KwText, KwTo, KwTrue,
    KwUpdate, KwUsing,
    KwValues,
    KwWhere, KwWhile,
    KwXor,

    // operators
    Eq, NotEq, Lt, LtEq, Gt, GtEq,
    Plus, Minus, Star, Slash, Percent,
    DotDot,

    // punctuation
    LParen, RParen, Comma, Colon, Dot, Semi,

    // end
    Eof
}

public record Token(Tk Kind, string Text, int Line, int Col);
