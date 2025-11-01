grammar Novus;

// Parser Rules

compilationUnit
    : NEWLINE* importDeclaration* reexportDeclaration* (constDeclaration | staticDeclaration | globalVariableDeclaration | structDeclaration | enumDeclaration | implDeclaration | functionDeclaration)* EOF
    ;

attribute
    : '@' IDENTIFIER ('(' attributeArgList? ')')? NEWLINE*
    | '#' '[' IDENTIFIER ('(' attributeArgList? ')')? ']' NEWLINE*
    ;

attributeArgList
    : attributeArg (',' attributeArg)*
    ;

attributeArg
    : IDENTIFIER '=' expression
    | expression
    ;

importDeclaration
    : KW_FROM modulePath KW_IMPORT importList NEWLINE*
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
    : KW_PUB KW_USE modulePath '::' ('*' | reexportList) NEWLINE*
    ;

reexportList
    : IDENTIFIER (',' IDENTIFIER)*
    ;

constDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_CONST IDENTIFIER (':' type)? '=' expression NEWLINE*
    ;

staticDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_STATIC KW_MUT? IDENTIFIER ':' type '=' expression NEWLINE*
    ;

globalVariableDeclaration
    : attribute* KW_EXTERN KW_VAR IDENTIFIER ':' type (KW_AT expression)? NEWLINE*
    ;

functionDeclaration
    : attribute* KW_EXTERN? (KW_PUB | KW_INTERNAL)? KW_FN IDENTIFIER '(' parameterList? ')' ('->' type)? block? NEWLINE*
    ;

parameterList
    : selfParameter (',' parameter)*
    | parameter (',' parameter)*
    ;

parameter
    : IDENTIFIER ':' type
    ;

selfParameter
    : '&' KW_MUT? KW_SELF
    | KW_SELF
    ;

structDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_STRUCT IDENTIFIER genericParams? '{' NEWLINE* structField* '}' NEWLINE*
    ;

structField
    : IDENTIFIER ':' type ','? NEWLINE*
    ;

enumDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_ENUM IDENTIFIER genericParams? '{' NEWLINE* enumVariant (',' NEWLINE* enumVariant)* ','? NEWLINE* '}' NEWLINE*
    ;

enumVariant
    : IDENTIFIER ('(' typeList ')')?
    ;

implDeclaration
    : attribute* KW_IMPL genericParams? typeName genericTypeArgs? '{' NEWLINE* implItem* '}' NEWLINE*
    ;

implItem
    : functionDeclaration
    ;

genericParams
    : '<' IDENTIFIER (',' IDENTIFIER)* '>'
    ;

genericTypeArgs
    : '<' typeList '>'
    ;

type
    : '&' KW_MUT? type                                        # ReferenceType
    | '*' type                                                # PointerType
    | '[' expression ']' type                                 # ArrayType
    | '[' type ']'                                           # SliceType       // TODO: Not yet implemented in IrBuilder/codegen
    | '(' type (',' type)+ ')'                               # TupleType       // TODO: Not yet implemented in IrBuilder/codegen
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
    | typeName ('<' typeList '>')?                          # NamedType
    ;

typeName
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

typeList
    : type (',' type)*
    ;

block
    : '{' NEWLINE* (statement (NEWLINE+ statement)* NEWLINE*)? '}'
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
    | block
    | expressionStatement
    ;

deferStatement
    : KW_DEFER block                 # DeferBlock
    | KW_DEFER '=>' expression       # DeferExpression
    ;

unsafeBlock
    : KW_UNSAFE block
    ;

usingStatement
    : KW_USING expression block
    ;

matchStatement
    : KW_MATCH expression '{' NEWLINE* matchArm (',' NEWLINE* matchArm)* ','? NEWLINE* '}'
    ;

matchArm
    : pattern '=>' (expression | block | returnStatement)
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
    : KW_RETURN expression?
    ;

variableDeclaration
    : (KW_LET | KW_VAR) KW_MUT? (IDENTIFIER | '_') (':' type)? '=' expression
    ;

assignmentStatement
    : ('*')* (IDENTIFIER | KW_SELF) (lvalueSuffix)* ('++' | '--')
    | ('*')* (IDENTIFIER | KW_SELF) (lvalueSuffix)* ('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=') expression
    ;

lvalueSuffix
    : '.' IDENTIFIER
    | '[' expression ']'
    ;

ifStatement
    : KW_IF ifCondition block (KW_ELSE (ifStatement | block))?
    ;

ifCondition
    : expression                                           # IfConditionExpression
    | KW_LET IDENTIFIER (':' type)? '=' expression        # IfConditionLet
    | KW_VAR IDENTIFIER (':' type)? '=' expression        # IfConditionVar
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
    | expression '++' # PostIncrementExpr
    | expression '--'                                      # PostDecrementExpr
    | expression '..' expression                           # RangeExpr            // TODO: Not yet implemented in IrBuilder/codegen
    | expression '..=' expression                          # RangeInclusiveExpr   // TODO: Not yet implemented in IrBuilder/codegen
    | '(' type ')' expression                              # CastExpr
    | '&' KW_MUT? expression                               # BorrowExpr
    | ('!' | '~' | '-') expression                         # UnaryExpr
    | '++' expression                                      # PreIncrementExpr
    | '--' expression                                      # PreDecrementExpr
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
    | KW_SELF                                      # SelfExpr
    | '@' KW_SIZEOF '(' type ')'                   # SizeofExpr
    | STRING_LITERAL                               # StringLiteral
    | '-'? FLOAT_LITERAL                           # FloatLiteral
    | '-'? INTEGER_LITERAL                         # IntegerLiteral
    | '-'? BINARY_LITERAL                          # BinaryLiteral
    | '-'? HEX_LITERAL                             # HexLiteral
    | typeName '{' NEWLINE* structFieldInit (',' NEWLINE* structFieldInit)* ','? NEWLINE* '}'  # StructLiteral
    | identifier                                   # IdentifierExpr
    | '(' expression ')'                           # ParenExpr
    | '{' NEWLINE* (expression (',' NEWLINE* expression)*)? NEWLINE* '}'     # ArrayLiteral
    ;

identifier
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

structFieldInit
    : IDENTIFIER ':' NEWLINE* expression
    ;

// Lexer Rules

// Keywords (must come before IDENTIFIER)
KW_FROM     : 'from';
KW_IMPORT   : 'import';
KW_AS       : 'as';
KW_USE      : 'use';
KW_PUB      : 'pub';
KW_INTERNAL : 'internal';
KW_CONST    : 'const';
KW_STATIC   : 'static';
KW_EXTERN   : 'extern';
KW_VAR      : 'var';
KW_AT       : 'at';
KW_LET      : 'let';
KW_MUT      : 'mut';
KW_FN       : 'fn';
KW_IMPL     : 'impl';
KW_SELF     : 'self';
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
KW_SIZEOF   : 'sizeof';
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
    : '\\' ('b' | 't' | 'n' | 'f' | 'r' | '0' | '"' | '\'' | '\\')
    | '\\x' HEX_DIGIT HEX_DIGIT
    ;

fragment HEX_DIGIT
    : [0-9A-Fa-f]
    ;

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
    : '//' ~[\r\n]* -> skip
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;
