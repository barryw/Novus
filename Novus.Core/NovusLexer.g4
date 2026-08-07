lexer grammar NovusLexer;

// Custom lexer base class for mode management (inline assembly)
options {
    superClass = NovusLexerBase;
}

@header {
using Novus.Frontend;
}

// Keywords (must come before IDENTIFIER)
KW_FROM     : 'from';
KW_IMPORT   : 'import';
KW_AS       : 'as';
KW_USE      : 'use';
KW_PUB      : 'pub';
KW_INTERNAL : 'internal';
KW_CONST    : 'const';
KW_TYPE     : 'type';
KW_STATIC   : 'static';
KW_EXTERN   : 'extern';
KW_VAR      : 'var';
KW_AT       : 'at';
KW_LET      : 'let';
// KW_MUT removed - use KW_VAR for mutability everywhere
KW_FN         : 'fn';
KW_IMPL       : 'impl';
KW_SELF       : 'self';
KW_SELF_TYPE  : 'Self';
KW_STRUCT     : 'struct';
KW_ENUM     : 'enum';
KW_TRAIT    : 'trait';
KW_WHERE    : 'where';
KW_RETURN   : 'return';
KW_IF       : 'if';
KW_ELSE     : 'else';
KW_WHILE    : 'while';
KW_FOR      : 'for';
KW_IN       : 'in';
KW_FOREVER  : 'forever';
KW_BREAK    : 'break';
KW_CONTINUE : 'continue';
KW_MATCH    : 'match';
KW_DEFER     : 'defer';
KW_UNLESS    : 'unless';
KW_UNSAFE    : 'unsafe';
KW_USING     : 'using';
KW_SIZEOF        : 'sizeof';
KW_OFFSETOF      : 'offsetof';
KW_ALIGNOF       : 'alignof';
KW_ZEROED        : 'zeroed';
KW_DROP_IN_PLACE : 'drop_in_place';
KW_CONSUMING : 'consuming';
KW_COPPER    : 'copper';
KW_BLITTER   : 'blitter';
// KW_ASM triggers ASM_HEADER_MODE to parse the header, then ASM_MODE for the body
KW_ASM       : 'asm' -> pushMode(ASM_HEADER_MODE);
KW_VOLATILE  : 'volatile';
KW_CLOBBERS  : 'clobbers';
KW_TRUE     : 'true';
KW_FALSE    : 'false';
KW_NULL     : 'null';
KW_PANIC    : 'panic';
KW_ASSERT   : 'assert';
KW_CLOSURE  : 'closure';

// Macro-like expressions (compile-time transformed)
KW_MATCHES  : 'matches!';    // matches!(expr, pattern) -> bool
KW_DBG      : 'dbg!';        // dbg!(expr) -> expr, prints to debug output
KW_UNREACHABLE : 'unreachable!';  // unreachable!() -> diverges, signals unreachable code

// Primitive type keywords
KW_U8       : 'u8';
KW_U16      : 'u16';
KW_U32      : 'u32';
KW_U64      : 'u64';
KW_I8       : 'i8';
KW_I16      : 'i16';
KW_I32      : 'i32';
KW_I64      : 'i64';
KW_BOOL     : 'bool';
KW_F32      : 'f32';
KW_F64      : 'f64';
KW_FIXED16  : 'fixed16';
KW_FIXED32  : 'fixed32';

// Range operators - must come before FLOAT_LITERAL
DOTDOTEQ    : '..=';
DOTDOT      : '..';

// Shift operators
LSHIFT_ASSIGN  : '<<=';
RSHIFT_ASSIGN  : '>>=';
LSHIFT         : '<<';
RSHIFT         : '>>';
LESS           : '<';
GREATER        : '>';

FLOAT_LITERAL
    : [0-9]+ '.' [0-9]+ ('f32' | 'f64' | 'fixed16' | 'fixed32')?
    | [0-9]* '.' [0-9]+ ('f32' | 'f64' | 'fixed16' | 'fixed32')?
    ;

INTEGER_LITERAL
    : [0-9]+ ('_' [0-9]+)* ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

BINARY_LITERAL
    : '%' [01]+ ('_' [01]+)* ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

HEX_LITERAL
    : '$' [0-9A-Fa-f]+ ('_' [0-9A-Fa-f]+)* ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    | '0' [xX] [0-9A-Fa-f]+ ('_' [0-9A-Fa-f]+)* ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

F_STRING_LITERAL
    : 'f"' ( F_ESC | F_INTERP | ~["\\{] )* '"'
    ;

fragment F_ESC
    : '\\' ('b' | 't' | 'n' | 'f' | 'r' | '0' | '"' | '\'' | '\\' | '{' | '}')
    | '\\x' HEX_DIGIT HEX_DIGIT
    ;

fragment F_INTERP
    : '{' F_INTERP_CONTENT '}'
    ;

fragment F_INTERP_CONTENT
    : ( ~[{}] | '{' F_INTERP_CONTENT '}' )*
    ;

CHAR_LITERAL
    : '\'' ( ESC | ~['\\] ) '\''
    ;

STRING_LITERAL
    : '"' ( ESC | ~["\\] )* '"'
    ;

fragment ESC
    : '\\' ('b' | 't' | 'n' | 'f' | 'r' | '0' | '"' | '\'' | '\\')
    | '\\x' HEX_DIGIT HEX_DIGIT
    ;

fragment HEX_DIGIT
    : [0-9A-Fa-f]
    ;

// UNDERSCORE must come before IDENTIFIER since both can match '_'
// ANTLR uses first-match wins for lexer rules
UNDERSCORE  : '_';

IDENTIFIER
    : [a-zA-Z_][a-zA-Z0-9_]*
    ;

// Whitespace and Comments
WS
    : [ \t]+ -> skip
    ;

NEWLINE
    : '\r'? '\n'
    ;

LINE_COMMENT
    : '//' ~[\r\n]* -> channel(HIDDEN)
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> channel(HIDDEN)
    ;

// Single-character tokens (used in both default and ASM modes)
LPAREN      : '(';
RPAREN      : ')';
LBRACE      : '{';
RBRACE      : '}';
LBRACKET    : '[';
RBRACKET    : ']';
SEMI        : ';';
COMMA       : ',';
DOT         : '.';
COLON       : ':';
COLONCOLON  : '::';
ARROW       : '->';
FAT_ARROW   : '=>';
HASH        : '#';
AT_SIGN     : '@';
// UNDERSCORE is defined earlier (before IDENTIFIER) - don't duplicate
QUESTION    : '?';
PLUS        : '+';
MINUS       : '-';
STAR        : '*';
SLASH       : '/';
PERCENT     : '%';
AMPERSAND   : '&';
PIPE        : '|';
CARET       : '^';
TILDE       : '~';
BANG        : '!';
EQ          : '=';
PLUSPLUS    : '++';
MINUSMINUS  : '--';
ANDAND      : '&&';
OROR        : '||';
EQEQ        : '==';
NE          : '!=';
LE          : '<=';
GE          : '>=';
PLUSEQ      : '+=';
MINUSEQ     : '-=';
STAREQ      : '*=';
SLASHEQ     : '/=';
PERCENTEQ   : '%=';
AMPEQ       : '&=';
PIPEEQ      : '|=';
CARETEQ     : '^=';

// ============================================================================
// ASM_HEADER_MODE - Parses asm header (inputs, return type, clobbers)
// ============================================================================
// Entered when KW_ASM is matched. Emits same tokens as default mode for the
// header portion. Transitions to ASM_MODE when '{' is seen.

mode ASM_HEADER_MODE;

// Re-emit header tokens with same types as default mode
ASM_HDR_LPAREN      : '(' -> type(LPAREN);
ASM_HDR_RPAREN      : ')' -> type(RPAREN);
ASM_HDR_LBRACE      : '{' -> type(LBRACE), mode(ASM_MODE);  // Transition to ASM_MODE
ASM_HDR_COMMA       : ',' -> type(COMMA);
ASM_HDR_ARROW       : '->' -> type(ARROW);
ASM_HDR_COLON       : ':' -> type(COLON);

// Keywords needed in asm header
ASM_HDR_IN          : 'in' -> type(KW_IN);
ASM_HDR_OUT         : 'out';  // 'out' is only a keyword in ASM_HEADER_MODE
ASM_HDR_VAR         : 'var' -> type(KW_VAR);
ASM_HDR_USE         : 'use' -> type(KW_USE);
ASM_HDR_SIZEOF      : 'sizeof' -> type(KW_SIZEOF);
ASM_HDR_OFFSETOF    : 'offsetof' -> type(KW_OFFSETOF);
ASM_HDR_ALIGNOF     : 'alignof' -> type(KW_ALIGNOF);
ASM_HDR_CONST       : 'const' -> type(KW_CONST);
ASM_HDR_VOLATILE    : 'volatile' -> type(KW_VOLATILE);
ASM_HDR_CLOBBERS    : 'clobbers' -> type(KW_CLOBBERS);
ASM_HDR_MEMORY      : 'memory' -> type(IDENTIFIER);
ASM_HDR_AMPERSAND   : '&' -> type(AMPERSAND);

// Type keywords for return type
ASM_HDR_U8          : 'u8' -> type(KW_U8);
ASM_HDR_U16         : 'u16' -> type(KW_U16);
ASM_HDR_U32         : 'u32' -> type(KW_U32);
ASM_HDR_U64         : 'u64' -> type(KW_U64);
ASM_HDR_I8          : 'i8' -> type(KW_I8);
ASM_HDR_I16         : 'i16' -> type(KW_I16);
ASM_HDR_I32         : 'i32' -> type(KW_I32);
ASM_HDR_I64         : 'i64' -> type(KW_I64);
ASM_HDR_BOOL        : 'bool' -> type(KW_BOOL);
ASM_HDR_STAR        : '*' -> type(STAR);

// General identifier for register names, variable names
ASM_HDR_IDENTIFIER  : [a-zA-Z_][a-zA-Z0-9_]* -> type(IDENTIFIER);

// Skip whitespace and newlines in header
ASM_HDR_WS          : [ \t]+ -> skip;
ASM_HDR_NEWLINE     : '\r'? '\n' -> type(NEWLINE);

// ============================================================================
// ASM_MODE - Assembly Lexer Mode (body of asm block)
// ============================================================================
// Entered when '{' is seen in ASM_HEADER_MODE. Lexes raw assembly syntax.

mode ASM_MODE;

// ASM_LBRACE for nested braces in assembly (e.g., bit field syntax {0:32})
ASM_LBRACE
    : '{'
    ;

// ASM_RBRACE closes the asm block and returns to default mode
ASM_RBRACE
    : '}' -> type(RBRACE), popMode
    ;

ASM_LABEL
    : '.' [a-zA-Z_][a-zA-Z0-9_]* ':'
    | [a-zA-Z_][a-zA-Z0-9_]* ':'
    ;

ASM_MNEMONIC
    : ASM_INSTRUCTION_NAME ASM_SIZE_SUFFIX?
    ;

fragment ASM_INSTRUCTION_NAME
    : [mM][oO][vV][eE]
    | [mM][oO][vV][eE][aA]
    | [mM][oO][vV][eE][qQ]
    | [mM][oO][vV][eE][mM]
    | [mM][oO][vV][eE][pP]
    | [eE][xX][gG]
    | [sS][wW][aA][pP]
    | [pP][eE][aA]
    | [lL][eE][aA]
    | [aA][dD][dD]
    | [aA][dD][dD][aA]
    | [aA][dD][dD][iI]
    | [aA][dD][dD][qQ]
    | [aA][dD][dD][xX]
    | [sS][uU][bB]
    | [sS][uU][bB][aA]
    | [sS][uU][bB][iI]
    | [sS][uU][bB][qQ]
    | [sS][uU][bB][xX]
    | [mM][uU][lL][sS]
    | [mM][uU][lL][uU]
    | [dD][iI][vV][sS]
    | [dD][iI][vV][uU]
    | [dD][iI][vV][sS][lL]
    | [dD][iI][vV][uU][lL]
    | [nN][eE][gG]
    | [nN][eE][gG][xX]
    | [cC][lL][rR]
    | [eE][xX][tT]
    | [eE][xX][tT][bB]
    | [aA][bB][cC][dD]
    | [sS][bB][cC][dD]
    | [nN][bB][cC][dD]
    | [aA][nN][dD]
    | [aA][nN][dD][iI]
    | [oO][rR]
    | [oO][rR][iI]
    | [eE][oO][rR]
    | [eE][oO][rR][iI]
    | [nN][oO][tT]
    | [aA][sS][lL]
    | [aA][sS][rR]
    | [lL][sS][lL]
    | [lL][sS][rR]
    | [rR][oO][lL]
    | [rR][oO][rR]
    | [rR][oO][xX][lL]
    | [rR][oO][xX][rR]
    | [bB][tT][sS][tT]
    | [bB][sS][eE][tT]
    | [bB][cC][lL][rR]
    | [bB][cC][hH][gG]
    | [bB][fF][cC][hH][gG]
    | [bB][fF][cC][lL][rR]
    | [bB][fF][eE][xX][tT][sS]
    | [bB][fF][eE][xX][tT][uU]
    | [bB][fF][fF][fF][oO]
    | [bB][fF][iI][nN][sS]
    | [bB][fF][sS][eE][tT]
    | [bB][fF][tT][sS][tT]
    | [tT][sS][tT]
    | [cC][mM][pP]
    | [cC][mM][pP][aA]
    | [cC][mM][pP][iI]
    | [cC][mM][pP][mM]
    | [cC][hH][kK]
    | [tT][aA][sS]
    | [bB][rR][aA]
    | [bB][sS][rR]
    | [bB][cC][cC]
    | [bB][cC][sS]
    | [bB][eE][qQ]
    | [bB][nN][eE]
    | [bB][gG][eE]
    | [bB][gG][tT]
    | [bB][lL][eE]
    | [bB][lL][tT]
    | [bB][hH][iI]
    | [bB][lL][sS]
    | [bB][mM][iI]
    | [bB][pP][lL]
    | [bB][vV][cC]
    | [bB][vV][sS]
    | [jJ][mM][pP]
    | [jJ][sS][rR]
    | [rR][tT][sS]
    | [rR][tT][eE]
    | [rR][tT][dD]
    | [rR][tT][rR]
    | [dD][bB][rR][aA]
    | [dD][bB][fF]
    | [dD][bB][tT]
    | [dD][bB][cC][cC]
    | [dD][bB][cC][sS]
    | [dD][bB][eE][qQ]
    | [dD][bB][nN][eE]
    | [dD][bB][gG][eE]
    | [dD][bB][gG][tT]
    | [dD][bB][lL][eE]
    | [dD][bB][lL][tT]
    | [dD][bB][hH][iI]
    | [dD][bB][lL][sS]
    | [dD][bB][mM][iI]
    | [dD][bB][pP][lL]
    | [dD][bB][vV][cC]
    | [dD][bB][vV][sS]
    | [sS][cC][cC]
    | [sS][cC][sS]
    | [sS][eE][qQ]
    | [sS][nN][eE]
    | [sS][gG][eE]
    | [sS][gG][tT]
    | [sS][lL][eE]
    | [sS][lL][tT]
    | [sS][hH][iI]
    | [sS][lL][sS]
    | [sS][mM][iI]
    | [sS][pP][lL]
    | [sS][vV][cC]
    | [sS][vV][sS]
    | [sS][tT]
    | [sS][fF]
    | [lL][iI][nN][kK]
    | [uU][nN][lL][kK]
    | [nN][oO][pP]
    | [rR][eE][sS][eE][tT]
    | [sS][tT][oO][pP]
    | [tT][rR][aA][pP]
    | [tT][rR][aA][pP][vV]
    | [iI][lL][lL][eE][gG][aA][lL]
    | [cC][aA][sS]
    | [pP][aA][cC][kK]
    | [uU][nN][pP][kK]
    | [cC][iI][nN][vV][aA]
    | [cC][iI][nN][vV][lL]
    | [cC][iI][nN][vV][pP]
    | [cC][pP][uU][sS][hH][aA]
    | [cC][pP][uU][sS][hH][lL]
    | [cC][pP][uU][sS][hH][pP]
    | [fF][mM][oO][vV][eE]
    | [fF][mM][oO][vV][eE][mM]
    | [fF][mM][oO][vV][eE][cC][rR]
    | [fF][aA][dD][dD]
    | [fF][sS][uU][bB]
    | [fF][mM][uU][lL]
    | [fF][dD][iI][vV]
    | [fF][cC][mM][pP]
    | [fF][tT][sS][tT]
    | [fF][aA][bB][sS]
    | [fF][nN][eE][gG]
    | [fF][sS][qQ][rR][tT]
    | [fF][iI][nN][tT]
    | [fF][iI][nN][tT][rR][zZ]
    | [fF][sS][iI][nN]
    | [fF][cC][oO][sS]
    | [fF][tT][aA][nN]
    | [fF][aA][tT][aA][nN]
    | [fF][eE][tT][oO][xX]
    | [fF][tT][wW][oO][tT][oO][xX]
    | [fF][tT][eE][nN][tT][oO][xX]
    | [fF][lL][oO][gG][nN]
    | [fF][lL][oO][gG][2]
    | [fF][lL][oO][gG][1][0]
    | [fF][gG][eE][tT][eE][xX][pP]
    | [fF][gG][eE][tT][mM][aA][nN]
    | [fF][sS][cC][aA][lL][eE]
    | [fF][mM][oO][dD]
    | [fF][rR][eE][mM]
    | [fF][sS][gG][lL][mM][uU][lL]
    | [fF][sS][gG][lL][dD][iI][vV]
    ;

fragment ASM_SIZE_SUFFIX
    : '.' [bBwWlLsSqQxXpPdD]
    ;

// Register list for movem instruction (e.g., d0-d7/a2-a5)
// Must come before ASM_REGISTER so longer matches take priority
ASM_REGLIST
    : ASM_REGLIST_PART ('/' ASM_REGLIST_PART)+
    ;

fragment ASM_REGLIST_PART
    : [dDaA] [0-7] '-' [dDaA] [0-7]     // d0-d7, a2-a5
    | [dDaA] [0-7]                       // single register d0, a5
    ;

ASM_REGISTER
    : [dD] [0-7]
    | [aA] [0-7]
    | [sS] [pP]
    | [pP] [cC]
    | [sS] [rR]
    | [cC] [cC] [rR]
    | [uU] [sS] [pP]
    | [vV] [bB] [rR]
    | [fF] [pP] [0-7]
    | [fF] [pP] [cC] [rR]
    | [fF] [pP] [sS] [rR]
    ;

ASM_IMMEDIATE
    : '#' '-'? [0-9]+
    | '#' '$' [0-9A-Fa-f]+
    | '#' '0' [xX] [0-9A-Fa-f]+
    | '#' '%' [01]+
    | '#' [a-zA-Z_][a-zA-Z0-9_]*  // Symbolic immediate (e.g., #sizeof_Player, #MY_CONST)
    ;

ASM_HEX
    : '$' [0-9A-Fa-f]+
    | '0' [xX] [0-9A-Fa-f]+
    ;

ASM_BINARY_NUM
    : '%' [01]+
    ;

ASM_NUMBER
    : '-'? [0-9]+
    ;

// Bit field specification: {offset:width} where offset/width can be numbers or registers
// Examples: {0:32}, {d1:d2}, {0:d2}
ASM_BITFIELD
    : '{' ASM_BF_VALUE ':' ASM_BF_VALUE '}'
    ;

fragment ASM_BF_VALUE
    : [0-9]+
    | [dD] [0-7]
    ;

ASM_ADDRESS_MODE
    : '-'? '(' ASM_ADDR_INNER ')'  '+'?
    ;

fragment ASM_ADDR_INNER
    : ( ~[()\r\n] | '(' ASM_ADDR_INNER ')' )+
    ;

ASM_LABEL_REF
    : '.' [a-zA-Z_][a-zA-Z0-9_]*
    | [a-zA-Z_][a-zA-Z0-9_]*
    ;

ASM_COMMA
    : ','
    ;

// Support legacy quoted string syntax in asm blocks for backward compatibility
ASM_STRING_LITERAL
    : '"' (~["\r\n] | '\\' .)* '"' -> type(STRING_LITERAL)
    ;

ASM_WS
    : [ \t]+ -> skip
    ;

ASM_NEWLINE
    : '\r'? '\n' -> type(NEWLINE)
    ;

ASM_COMMENT
    : ';' ~[\r\n]* -> channel(HIDDEN)
    ;

ASM_LINE_COMMENT
    : '//' ~[\r\n]* -> channel(HIDDEN)
    ;

ASM_BLOCK_COMMENT
    : '/*' .*? '*/' -> channel(HIDDEN)
    ;
