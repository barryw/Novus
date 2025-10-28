grammar Novus;

// Parser Rules

compilationUnit
    : importDeclaration* reexportDeclaration* (constDeclaration | globalVariableDeclaration | structDeclaration | enumDeclaration | functionDeclaration)* EOF
    ;

attribute
    : '@' IDENTIFIER ('(' attributeArgList? ')')?
    | '#' '[' IDENTIFIER ('(' attributeArgList? ')')? ']'
    ;

attributeArgList
    : attributeArg (',' attributeArg)*
    ;

attributeArg
    : IDENTIFIER '=' expression
    | expression
    ;

importDeclaration
    : KW_FROM modulePath KW_IMPORT importList
    ;

modulePath
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

importList
    : '*'
    | importName (',' importName)*
    ;

importName
    : IDENTIFIER (KW_AS IDENTIFIER)?
    ;

reexportDeclaration
    : KW_PUB KW_USE modulePath '::' ('*' | reexportList)
    ;

reexportList
    : IDENTIFIER (',' IDENTIFIER)*
    ;

constDeclaration
    : attribute* KW_PUB? KW_CONST IDENTIFIER ':' type '=' expression
    ;

globalVariableDeclaration
    : attribute* KW_EXTERN KW_VAR IDENTIFIER ':' type
    ;

functionDeclaration
    : attribute* KW_EXTERN? KW_PUB? KW_FN IDENTIFIER '(' parameterList? ')' ('->' type)? block?
    ;

parameterList
    : parameter (',' parameter)*
    ;

parameter
    : IDENTIFIER ':' type
    ;

structDeclaration
    : attribute* KW_PUB? KW_STRUCT IDENTIFIER genericParams? '{' structField* '}'
    ;

structField
    : IDENTIFIER ':' type ','?
    ;

enumDeclaration
    : attribute* KW_PUB? KW_ENUM IDENTIFIER genericParams? '{' enumVariant (',' enumVariant)* ','? '}'
    ;

enumVariant
    : IDENTIFIER ('(' typeList ')')?
    ;

genericParams
    : '<' IDENTIFIER (',' IDENTIFIER)* '>'
    ;

type
    : '&' KW_MUT? type                                        # ReferenceType
    | '*' type                                                # PointerType
    | '[' INTEGER_LITERAL ']' type                           # ArrayType
    | '[' type ']'                                           # SliceType
    | '(' type (',' type)+ ')'                               # TupleType
    | KW_FN '(' typeList? ')' ('->' type)?                   # FunctionPointerType
    | KW_U8                                                   # PrimitiveType
    | KW_U16                                                  # PrimitiveType
    | KW_U32                                                  # PrimitiveType
    | KW_U64                                                  # PrimitiveType
    | KW_I8                                                   # PrimitiveType
    | KW_I16                                                  # PrimitiveType
    | KW_I32                                                  # PrimitiveType
    | KW_I64                                                  # PrimitiveType
    | KW_BOOL                                                 # PrimitiveType
    | KW_F32                                                  # PrimitiveType
    | KW_F64                                                  # PrimitiveType
    | KW_FIXED16                                              # PrimitiveType
    | KW_FIXED32                                              # PrimitiveType
    | KW_STRING                                               # PrimitiveType
    | typeName ('<' typeList '>')?                           # NamedType
    ;

typeName
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

typeList
    : type (',' type)*
    ;

block
    : '{' statement* '}'
    ;

statement
    : returnStatement
    | variableDeclaration
    | assignmentStatement
    | ifStatement
    | whileStatement
    | forStatement
    | foreverStatement
    | breakStatement
    | matchStatement
    | deferStatement
    | unsafeBlock
    | usingStatement
    | expressionStatement
    ;

deferStatement
    : KW_DEFER block
    ;

unsafeBlock
    : KW_UNSAFE block
    ;

usingStatement
    : KW_USING expression block
    ;

matchStatement
    : KW_MATCH expression '{' matchArm (',' matchArm)* ','? '}'
    ;

matchArm
    : pattern '=>' (expression | block)
    ;

pattern
    : variantName '(' patternList? ')'         # VariantPattern
    | IDENTIFIER '::' IDENTIFIER ('::' IDENTIFIER)*  # SimpleVariantPattern
    | IDENTIFIER                                # IdentifierPattern
    | INTEGER_LITERAL                           # LiteralPattern
    | STRING_LITERAL                            # LiteralPattern
    | KW_TRUE                                   # BoolLiteralPattern
    | KW_FALSE                                  # BoolLiteralPattern
    | '_'                                       # WildcardPattern
    ;

variantName
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

patternList
    : pattern (',' pattern)*
    ;

returnStatement
    : KW_RETURN expression
    ;

variableDeclaration
    : (KW_LET | KW_VAR) (IDENTIFIER | '_') (':' type)? '=' expression
    ;

assignmentStatement
    : ('*')* IDENTIFIER (lvalueSuffix)* '=' expression
    ;

lvalueSuffix
    : '.' IDENTIFIER
    | '[' expression ']'
    ;

ifStatement
    : KW_IF expression block (KW_ELSE (ifStatement | block))?
    ;

whileStatement
    : KW_WHILE expression block
    ;

forStatement
    : KW_FOR '(' (variableDeclaration | assignmentStatement)? ';' expression? ';' assignmentStatement? ')' block
    ;

foreverStatement
    : KW_FOREVER block
    ;

breakStatement
    : KW_BREAK
    ;

expressionStatement
    : expression
    ;

expression
    : primaryExpression                                     # PrimaryExpr
    | expression '::' IDENTIFIER                           # PathExpr
    | expression '.' IDENTIFIER                            # MemberAccessExpr
    | expression '(' argumentList? ')'                     # CallExpr
    | expression '[' expression ']'                        # IndexExpr
    | expression '..' expression                           # RangeExpr
    | expression '..=' expression                          # RangeInclusiveExpr
    | '(' type ')' expression                              # CastExpr
    | '&' KW_MUT? expression                               # BorrowExpr
    | ('!' | '~' | '-') expression                         # UnaryExpr
    | '*' expression                                       # DereferenceExpr
    | expression ('*' | '/' | '%') expression              # MultiplicativeExpr
    | expression ('+' | '-') expression                     # AdditiveExpr
    | expression ('<<' | '>>') expression                  # ShiftExpr
    | expression '&' expression                            # BitwiseAndExpr
    | expression '^' expression                            # BitwiseXorExpr
    | expression '|' expression                            # BitwiseOrExpr
    | expression ('==' | '!=' | '<' | '>' | '<=' | '>=') expression  # ComparisonExpr
    | expression '&&' expression                           # LogicalAndExpr
    | expression '||' expression                           # LogicalOrExpr
    ;

argumentList
    : expression (',' expression)*
    ;

primaryExpression
    : KW_TRUE                                      # BoolLiteral
    | KW_FALSE                                     # BoolLiteral
    | STRING_LITERAL                               # StringLiteral
    | '-'? FLOAT_LITERAL                           # FloatLiteral
    | '-'? INTEGER_LITERAL                         # IntegerLiteral
    | '-'? BINARY_LITERAL                          # BinaryLiteral
    | '-'? HEX_LITERAL                             # HexLiteral
    | typeName '{' structFieldInit (',' structFieldInit)* ','? '}'  # StructLiteral
    | identifier                                   # IdentifierExpr
    | '(' expression ')'                           # ParenExpr
    | '{' (expression (',' expression)*)? '}'     # ArrayLiteral
    ;

identifier
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

structFieldInit
    : IDENTIFIER ':' expression
    ;

// Lexer Rules

// Keywords (must come before IDENTIFIER)
KW_FROM     : 'from';
KW_IMPORT   : 'import';
KW_AS       : 'as';
KW_USE      : 'use';
KW_PUB      : 'pub';
KW_CONST    : 'const';
KW_EXTERN   : 'extern';
KW_VAR      : 'var';
KW_LET      : 'let';
KW_MUT      : 'mut';
KW_FN       : 'fn';
KW_STRUCT   : 'struct';
KW_ENUM     : 'enum';
KW_RETURN   : 'return';
KW_IF       : 'if';
KW_ELSE     : 'else';
KW_WHILE    : 'while';
KW_FOR      : 'for';
KW_FOREVER  : 'forever';
KW_BREAK    : 'break';
KW_MATCH    : 'match';
KW_DEFER    : 'defer';
KW_UNSAFE   : 'unsafe';
KW_USING    : 'using';
KW_TRUE     : 'true';
KW_FALSE    : 'false';

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
KW_STRING   : 'String';

FLOAT_LITERAL
    : [0-9]+ '.' [0-9]* ('f32' | 'f64' | 'fixed16' | 'fixed32')?
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
    ;

STRING_LITERAL
    : '"' ( ESC | ~["\\] )* '"'
    ;

fragment ESC
    : '\\' [btnfr0"'\\]
    | '\\x' [0-9A-Fa-f] [0-9A-Fa-f]
    ;

IDENTIFIER
    : [a-zA-Z_][a-zA-Z0-9_]*
    ;

// Whitespace and Comments
WS
    : [ \t\r\n]+ -> skip
    ;

LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;
