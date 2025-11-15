grammar Novus;

// Parser Rules

compilationUnit
    : NEWLINE* importDeclaration* reexportDeclaration* (constDeclaration | staticDeclaration | globalVariableDeclaration | structDeclaration | enumDeclaration | traitDeclaration | implDeclaration | functionDeclaration)* EOF
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
    : importWildcard (KW_AS IDENTIFIER)?
    | IDENTIFIER (KW_AS IDENTIFIER)?
    ;

importWildcard
    : IDENTIFIER '*'     // Prefix wildcard: MEMF_*
    | '*' IDENTIFIER     // Suffix wildcard: *Mem
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
    : attribute* KW_EXTERN? (KW_PUB | KW_INTERNAL)? KW_FN IDENTIFIER genericParams? '(' parameterList? ')' ('->' type)? whereClause? block? NEWLINE*
    ;

parameterList
    : selfParameter (',' parameter)* (',' variadicParameter)?
    | parameter (',' parameter)* (',' variadicParameter)?
    | variadicParameter
    ;

parameter
    : KW_CONSUMING? IDENTIFIER ':' type
    ;

variadicParameter
    : '...' IDENTIFIER
    ;

selfParameter
    : '&' KW_MUT? KW_SELF
    | KW_CONSUMING? KW_SELF
    ;

structDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_STRUCT IDENTIFIER genericParams? whereClause? '{' NEWLINE* structField* '}' NEWLINE*
    ;

structField
    : IDENTIFIER ':' type ','? NEWLINE*
    ;

enumDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_ENUM IDENTIFIER genericParams? whereClause? '{' NEWLINE* enumVariant (',' NEWLINE* enumVariant)* ','? NEWLINE* '}' NEWLINE*
    ;

enumVariant
    : IDENTIFIER ('(' typeList ')')?
    ;

traitDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_TRAIT IDENTIFIER genericParams? '{' NEWLINE* traitItem* '}' NEWLINE*
    ;

traitItem
    : functionSignature
    ;

functionSignature
    : KW_FN IDENTIFIER genericParams? '(' parameterList? ')' ('->' type)? NEWLINE*
    ;

implDeclaration
    : attribute* KW_IMPL genericParams? traitTypeName=typeName traitTypeArgs=genericTypeArgs? KW_FOR implTargetType whereClause? '{' NEWLINE* implItem* '}' NEWLINE*
    | attribute* KW_IMPL genericParams? targetTypeName=typeName targetTypeArgs=genericTypeArgs? whereClause? '{' NEWLINE* implItem* '}' NEWLINE*
    ;

implTargetType
    : typeName implTypeArgs=genericTypeArgs?  # NamedImplTarget
    | primitiveTypeName                       # PrimitiveImplTarget
    ;

primitiveTypeName
    : KW_U8
    | KW_U16
    | KW_U32
    | KW_U64
    | KW_I8
    | KW_I16
    | KW_I32
    | KW_I64
    | KW_BOOL
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

whereClause
    : KW_WHERE whereBound (',' whereBound)*
    ;

whereBound
    : IDENTIFIER ':' traitBound
    ;

traitBound
    : typeName genericTypeArgs?  # SingleTraitBound
    | traitBound '+' traitBound  # MultipleTraitBound
    ;

type
    : '&' KW_MUT? type                                        # ReferenceType
    | '*' type                                                # PointerType
    | '[' type ';' expression ']'                            # ArrayTypeWithSize      // [u8; 100] - fixed-size uninitialized array
    | '[' type ']'                                           # ArrayTypeInferred      // [i32] - size inferred from initializer
    | '(' ')'                                                # UnitType               // Unit type ()
    | '(' type (',' type)+ ')'                               # TupleType              // Tuple with 2+ elements
    | KW_FN '(' typeList? ')' ('->' type)?                   # FunctionPointerType
    | KW_SELF_TYPE                                           # SelfType              // Self - refers to implementing type in trait context
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
    | deferStatement
    | assertStatement
    | panicStatement
    | unsafeBlock
    | usingStatement
    | block
    | expressionStatement
    ;

deferStatement
    : KW_DEFER block                 # DeferBlock
    | KW_DEFER expression            # DeferExpression
    ;

assertStatement
    : 'assert' '!' '(' expression (',' STRING_LITERAL)? ')'
    ;

panicStatement
    : 'panic' '!' '(' STRING_LITERAL ')'
    ;

unsafeBlock
    : KW_UNSAFE block
    ;

usingStatement
    : KW_USING expression block
    ;

matchArm
    : pattern (KW_IF expression)? '=>' (expression | block | returnStatement)
    ;

pattern
    : pattern '|' pattern                       # PipePattern
    | '&' pattern                               # ReferencePattern
    | variantName '(' patternList? ')'         # VariantPattern
    | IDENTIFIER '::' IDENTIFIER ('::' IDENTIFIER)*  # SimpleVariantPattern
    | IDENTIFIER                                # IdentifierPattern
    | INTEGER_LITERAL                           # LiteralPattern
    | HEX_LITERAL                               # LiteralPattern
    | BINARY_LITERAL                            # LiteralPattern
    | STRING_LITERAL                            # LiteralPattern
    | KW_TRUE                                   # BoolLiteralPattern
    | KW_FALSE                                  # BoolLiteralPattern
    | KW_NULL                                   # NullLiteralPattern
    | '_'                                       # WildcardPattern
    ;

variantName
    : IDENTIFIER ('::' IDENTIFIER)*
    ;

patternList
    : pattern (',' pattern)*
    ;

postfixCondition
    : KW_IF expression
    | KW_UNLESS expression
    ;

returnStatement
    : KW_RETURN expression? postfixCondition?
    ;

variableDeclaration
    : (KW_LET | KW_VAR) KW_MUT? (IDENTIFIER | '_' | tuplePattern) (':' type)? '=' expression
    ;

tuplePattern
    : '(' (IDENTIFIER | '_') (',' (IDENTIFIER | '_'))+ ')'
    ;

assignmentStatement
    : ('*')* (IDENTIFIER | KW_SELF) (lvalueSuffix)* ('++' | '--') postfixCondition?
    | ('*')* (IDENTIFIER | KW_SELF) (lvalueSuffix)* ('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=') expression postfixCondition?
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
    : KW_FOR (variableDeclaration | assignmentStatement) ';' expression ';' assignmentStatement block  # ForCStyle
    | KW_FOR KW_MUT? IDENTIFIER KW_IN expression block                                                 # ForInLoop
    ;

foreverStatement
    : KW_FOREVER block
    ;

breakStatement
    : KW_BREAK postfixCondition?
    ;

expressionStatement
    : expression postfixCondition?
    ;

expression
    : primaryExpression                                     # PrimaryExpr
    | expression '::' genericTypeArgs                      # TurboFishExpr
    | expression '::' IDENTIFIER                           # PathExpr
    | expression '.' IDENTIFIER                            # MemberAccessExpr
    | expression '(' argumentList? ')'                     # CallExpr
    | expression '[' expression ']'                        # IndexExpr
    | expression '++' # PostIncrementExpr
    | expression '--'                                      # PostDecrementExpr
    | expression '?'                                       # TryExpr               // Result propagation with auto-conversion
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
    | <assoc=right> expression '?' expression ':' expression  # TernaryExpr
    ;

argumentList
    : expression (',' expression)*
    ;

primaryExpression
    : KW_TRUE                                      # BoolLiteral
    | KW_FALSE                                     # BoolLiteral
    | KW_NULL                                      # NullLiteral
    | KW_SELF                                      # SelfExpr
    | '@'? KW_SIZEOF '(' type ')'                  # SizeofExpr
    | F_STRING_LITERAL                             # InterpolatedStringLiteral
    | CHAR_LITERAL                                 # CharLiteral
    | STRING_LITERAL                               # StringLiteral
    | '-'? FLOAT_LITERAL                           # FloatLiteral
    | '-'? INTEGER_LITERAL                         # IntegerLiteral
    | '-'? BINARY_LITERAL                          # BinaryLiteral
    | '-'? HEX_LITERAL                             # HexLiteral
    | typeName '{' NEWLINE* structFieldInit (',' NEWLINE* structFieldInit)* ','? NEWLINE* '}'  # StructLiteral
    | typeName '{' NEWLINE* expression NEWLINE* '}'                                           # StructArrayInit
    | identifier                                   # IdentifierExpr
    | KW_MATCH expression '{' NEWLINE* matchArm (',' NEWLINE* matchArm)* ','? NEWLINE* '}'  # MatchExpr
    | '(' ')'                                      # UnitLiteral
    | '(' expression (',' expression)+ ')'         # TupleLiteral
    | '(' expression ')'                           # ParenExpr
    | '[' NEWLINE* expression NEWLINE* ';' NEWLINE* expression NEWLINE* ']'  # ArrayRepeatLiteral
    | '[' NEWLINE* (expression (',' NEWLINE* expression)*)? NEWLINE* ']'     # ArrayLiteral
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
KW_MATCH    : 'match';
KW_DEFER     : 'defer';
KW_UNLESS    : 'unless';
KW_UNSAFE    : 'unsafe';
KW_USING     : 'using';
KW_SIZEOF    : 'sizeof';
KW_CONSUMING : 'consuming';
KW_TRUE     : 'true';
KW_FALSE    : 'false';
KW_NULL     : 'null';

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
