grammar Novus;

// Parser Rules

compilationUnit
    : importDeclaration* (constDeclaration | structDeclaration | enumDeclaration | functionDeclaration)* EOF
    ;

importDeclaration
    : 'from' IDENTIFIER 'import' importList
    ;

importList
    : '*'
    | importName (',' importName)*
    ;

importName
    : IDENTIFIER ('as' IDENTIFIER)?
    ;

constDeclaration
    : 'pub'? 'const' IDENTIFIER ':' type '=' expression
    ;

functionDeclaration
    : 'extern'? 'pub'? 'fn' IDENTIFIER '(' parameterList? ')' ('->' type)? block?
    ;

parameterList
    : parameter (',' parameter)*
    ;

parameter
    : IDENTIFIER ':' type
    ;

structDeclaration
    : 'pub'? 'struct' IDENTIFIER genericParams? '{' structField* '}'
    ;

structField
    : IDENTIFIER ':' type ','?
    ;

enumDeclaration
    : 'pub'? 'enum' IDENTIFIER genericParams? '{' enumVariant (',' enumVariant)* ','? '}'
    ;

enumVariant
    : IDENTIFIER ('(' typeList ')')?
    ;

genericParams
    : '<' IDENTIFIER (',' IDENTIFIER)* '>'
    ;

type
    : '*' type                                                # PointerType
    | '[' INTEGER_LITERAL ']' type                           # ArrayType
    | 'fn' '(' typeList? ')' ('->' type)?                    # FunctionPointerType
    | 'u8'                                                    # PrimitiveType
    | 'u16'                                                   # PrimitiveType
    | 'u32'                                                   # PrimitiveType
    | 'u64'                                                   # PrimitiveType
    | 'i8'                                                    # PrimitiveType
    | 'i16'                                                   # PrimitiveType
    | 'i32'                                                   # PrimitiveType
    | 'i64'                                                   # PrimitiveType
    | 'bool'                                                  # PrimitiveType
    | 'f32'                                                   # PrimitiveType
    | 'f64'                                                   # PrimitiveType
    | 'fixed16'                                               # PrimitiveType
    | 'fixed32'                                               # PrimitiveType
    | IDENTIFIER ('<' typeList '>')?                         # NamedType
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
    | expressionStatement
    ;

matchStatement
    : 'match' expression '{' matchArm (',' matchArm)* ','? '}'
    ;

matchArm
    : pattern '=>' (expression | block)
    ;

pattern
    : IDENTIFIER '(' patternList? ')'  # VariantPattern
    | IDENTIFIER                        # IdentifierPattern
    | INTEGER_LITERAL                   # LiteralPattern
    | STRING_LITERAL                    # LiteralPattern
    | '_'                               # WildcardPattern
    ;

patternList
    : pattern (',' pattern)*
    ;

returnStatement
    : 'return' expression
    ;

variableDeclaration
    : ('let' | 'var') IDENTIFIER (':' type)? '=' expression
    ;

assignmentStatement
    : IDENTIFIER ('[' expression ']' | ('.' IDENTIFIER)+)? '=' expression
    ;

ifStatement
    : 'if' expression block ('else' (ifStatement | block))?
    ;

whileStatement
    : 'while' expression block
    ;

forStatement
    : 'for' '(' (variableDeclaration | assignmentStatement)? ';' expression? ';' assignmentStatement? ')' block
    ;

foreverStatement
    : 'forever' block
    ;

breakStatement
    : 'break'
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
    | '(' type ')' expression                              # CastExpr
    | '&' IDENTIFIER                                       # AddressOfExpr
    | ('!' | '~' | '-') expression                         # UnaryExpr
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
    : 'true'                                      # BoolLiteral
    | 'false'                                     # BoolLiteral
    | STRING_LITERAL                              # StringLiteral
    | '-'? FLOAT_LITERAL                          # FloatLiteral
    | '-'? INTEGER_LITERAL                        # IntegerLiteral
    | '-'? BINARY_LITERAL                         # BinaryLiteral
    | '-'? HEX_LITERAL                            # HexLiteral
    | IDENTIFIER '{' structFieldInit (',' structFieldInit)* ','? '}'  # StructLiteral
    | IDENTIFIER                                  # IdentifierExpr
    | '(' expression ')'                          # ParenExpr
    | '{' (expression (',' expression)*)? '}'    # ArrayLiteral
    ;

structFieldInit
    : IDENTIFIER ':' expression
    ;

// Lexer Rules

FLOAT_LITERAL
    : [0-9]+ '.' [0-9]* ('f32' | 'f64' | 'fixed16' | 'fixed32')?
    | [0-9]* '.' [0-9]+ ('f32' | 'f64' | 'fixed16' | 'fixed32')?
    ;

INTEGER_LITERAL
    : [0-9] ([0-9_]* [0-9])? ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

BINARY_LITERAL
    : '%' [01] ([01_]* [01])? ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

HEX_LITERAL
    : '$' [0-9A-Fa-f] ([0-9A-Fa-f_]* [0-9A-Fa-f])? ('u8' | 'u16' | 'u32' | 'u64' | 'i8' | 'i16' | 'i32' | 'i64')?
    ;

STRING_LITERAL
    : '"' ( ESC | ~["\\] )* '"'
    ;

fragment ESC
    : '\\' [btnfr"'\\]
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
