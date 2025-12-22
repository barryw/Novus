parser grammar NovusParser;

options {
    tokenVocab = NovusLexer;
}

@header {
using Novus.Frontend;
}

@members {
    /// <summary>
    /// Get the lexer instance for mode management (inline assembly).
    /// </summary>
    private NovusLexer GetLexer()
    {
        return (NovusLexer)((Antlr4.Runtime.CommonTokenStream)InputStream).TokenSource;
    }
}

// Parser Rules

compilationUnit
    : NEWLINE* importDeclaration* reexportDeclaration* (functionDeclaration | constDeclaration | staticDeclaration | globalVariableDeclaration | structDeclaration | enumDeclaration | traitDeclaration | implDeclaration)* EOF
    ;

attribute
    : AT_SIGN IDENTIFIER (LPAREN attributeArgList? RPAREN)? NEWLINE*
    | HASH LBRACKET IDENTIFIER (LPAREN attributeArgList? RPAREN)? RBRACKET NEWLINE*
    ;

attributeArgList
    : attributeArg (COMMA attributeArg)*
    ;

attributeArg
    : IDENTIFIER EQ expression
    | expression
    ;

importDeclaration
    : KW_FROM modulePath KW_IMPORT importList NEWLINE*
    ;

modulePath
    : modulePathComponent (COLONCOLON modulePathComponent)*
    ;

// Module path components can be identifiers or certain keywords that are also valid module names
modulePathComponent
    : IDENTIFIER
    | KW_COPPER      // std::graphics::copper
    | KW_BLITTER     // std::graphics::blitter
    ;

importList
    : STAR
    | importName (COMMA importName)*
    ;

importName
    : importWildcard (KW_AS IDENTIFIER)?
    | IDENTIFIER (KW_AS IDENTIFIER)?
    ;

importWildcard
    : IDENTIFIER STAR
    | STAR IDENTIFIER
    ;

reexportDeclaration
    : KW_PUB KW_USE modulePath COLONCOLON (STAR | reexportList) NEWLINE*
    ;

reexportList
    : IDENTIFIER (COMMA IDENTIFIER)*
    ;

constDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_CONST IDENTIFIER (COLON type)? EQ expression NEWLINE*
    ;

staticDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_STATIC KW_VAR? IDENTIFIER (COLON type)? EQ expression NEWLINE*
    ;

globalVariableDeclaration
    : attribute* KW_EXTERN KW_VAR IDENTIFIER COLON type (KW_AT expression)? NEWLINE*
    ;

functionDeclaration
    : attribute* KW_EXTERN? (KW_PUB | KW_INTERNAL)? KW_CONST? KW_FN IDENTIFIER genericParams? LPAREN parameterList? RPAREN (ARROW type)? whereClause? (block | SEMI)? NEWLINE*
    ;

parameterList
    : selfParameter (COMMA NEWLINE* parameter)* (COMMA NEWLINE* variadicParameter)?
    | parameter (COMMA NEWLINE* parameter)* (COMMA NEWLINE* variadicParameter)?
    | variadicParameter
    ;

parameter
    : NEWLINE* KW_CONSUMING? IDENTIFIER COLON type NEWLINE*
    | NEWLINE* KW_CONSUMING? IDENTIFIER NEWLINE*
    ;

variadicParameter
    : DOTDOT DOT IDENTIFIER
    ;

selfParameter
    : AMPERSAND KW_VAR? KW_SELF
    | KW_CONSUMING? KW_SELF
    ;

structDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_STRUCT IDENTIFIER genericParams? whereClause? LBRACE NEWLINE* structField* RBRACE NEWLINE*
    ;

structField
    : IDENTIFIER COLON type COMMA? NEWLINE*
    | IDENTIFIER COMMA? NEWLINE*
    ;

enumDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_ENUM IDENTIFIER genericParams? whereClause? LBRACE NEWLINE* (enumVariant (COMMA NEWLINE* enumVariant)* COMMA?)? NEWLINE* RBRACE NEWLINE*
    ;

enumVariant
    : IDENTIFIER (LPAREN typeList RPAREN)?
    | IDENTIFIER LPAREN RPAREN
    ;

traitDeclaration
    : attribute* (KW_PUB | KW_INTERNAL)? KW_TRAIT IDENTIFIER genericParams? LBRACE NEWLINE* traitItem* RBRACE NEWLINE*
    ;

traitItem
    : traitMethodDeclaration
    ;

traitMethodDeclaration
    : KW_FN IDENTIFIER genericParams? LPAREN parameterList? RPAREN (ARROW type)? (block | SEMI)? NEWLINE*
    ;

functionSignature
    : KW_FN IDENTIFIER genericParams? LPAREN parameterList? RPAREN (ARROW type)? NEWLINE*
    ;

implDeclaration
    : attribute* KW_IMPL genericParams? traitTypeName=typeName traitTypeArgs=genericTypeArgs? KW_FOR implTargetType whereClause? LBRACE NEWLINE* implItem* RBRACE NEWLINE*
    | attribute* KW_IMPL genericParams? targetTypeName=typeName targetTypeArgs=genericTypeArgs? whereClause? LBRACE NEWLINE* implItem* RBRACE NEWLINE*
    ;

implTargetType
    : typeName implTypeArgs=genericTypeArgs?  # NamedImplTarget
    | primitiveTypeName                       # PrimitiveImplTarget
    ;

primitiveTypeName
    : KW_U8 | KW_U16 | KW_U32 | KW_U64 | KW_I8 | KW_I16 | KW_I32 | KW_I64 | KW_BOOL
    ;

implItem
    : functionDeclaration
    ;

genericParams
    : LESS IDENTIFIER (COMMA IDENTIFIER)* GREATER
    ;

genericTypeArgs
    : LESS typeList GREATER
    ;

whereClause
    : KW_WHERE whereBound (COMMA whereBound)*
    ;

whereBound
    : IDENTIFIER COLON traitBound
    ;

traitBound
    : typeName genericTypeArgs?  # SingleTraitBound
    | traitBound PLUS traitBound # MultipleTraitBound
    ;

type
    : AMPERSAND KW_VAR? type                                        # ReferenceType
    | STAR type                                                     # PointerType
    | LBRACKET NEWLINE* type NEWLINE* SEMI NEWLINE* expression NEWLINE* RBRACKET  # ArrayTypeWithSize
    | LBRACKET NEWLINE* type NEWLINE* RBRACKET                      # ArrayTypeInferred
    | LPAREN RPAREN                                                 # UnitType
    | LPAREN NEWLINE* type (COMMA NEWLINE* type)+ NEWLINE* RPAREN   # TupleType
    | KW_FN LPAREN NEWLINE* typeList? NEWLINE* RPAREN (ARROW type)? # FunctionPointerType
    | KW_CLOSURE LPAREN NEWLINE* typeList? NEWLINE* RPAREN (ARROW type)?  # ClosureType
    | KW_SELF_TYPE                                                  # SelfType
    | KW_U8                                                         # PrimitiveType
    | KW_U16                                                        # PrimitiveType
    | KW_U32                                                        # PrimitiveType
    | KW_U64                                                        # PrimitiveType
    | KW_I8                                                         # PrimitiveType
    | KW_I16                                                        # PrimitiveType
    | KW_I32                                                        # PrimitiveType
    | KW_I64                                                        # PrimitiveType
    | KW_BOOL                                                       # PrimitiveType
    | KW_F32                                                        # PrimitiveType
    | KW_F64                                                        # PrimitiveType
    | KW_FIXED16                                                    # PrimitiveType
    | KW_FIXED32                                                    # PrimitiveType
    | typeName genericTypeArgs?                                     # NamedType
    ;

typeName
    : IDENTIFIER (COLONCOLON IDENTIFIER)*
    ;

typeList
    : NEWLINE* type (COMMA NEWLINE* type)* NEWLINE*
    ;

block
    : LBRACE NEWLINE* (statement (NEWLINE+ statement)* NEWLINE*)? RBRACE
    ;

statement
    : returnStatement
    | letElseStatement
    | variableDeclaration
    | assignmentStatement
    | ifStatement
    | labeledLoop
    | whileStatement
    | forStatement
    | foreverStatement
    | breakStatement
    | continueStatement
    | deferStatement
    | panicStatement
    | assertStatement
    | asmStatement
    | unsafeBlock
    | usingStatement
    | block
    | expressionStatement
    ;

letElseStatement
    : KW_LET pattern EQ expression KW_ELSE block
    ;

labeledLoop
    : IDENTIFIER COLON whileStatement
    | IDENTIFIER COLON forStatement
    | IDENTIFIER COLON foreverStatement
    ;

deferStatement
    : KW_DEFER block                 # DeferBlock
    | KW_DEFER expression            # DeferExpression
    ;

assertStatement
    : KW_ASSERT BANG LPAREN expression (COMMA STRING_LITERAL)? RPAREN
    ;

panicStatement
    : KW_PANIC BANG LPAREN STRING_LITERAL RPAREN
    ;

unsafeBlock
    : KW_UNSAFE block
    ;

usingStatement
    : KW_USING expression block
    ;

matchArm
    : pattern (KW_IF expression)? FAT_ARROW (expression | block | returnStatement)
    ;

pattern
    : pattern PIPE pattern                       # PipePattern
    | AMPERSAND pattern                          # ReferencePattern
    | variantName LPAREN patternList? RPAREN    # VariantPattern
    | IDENTIFIER COLONCOLON IDENTIFIER (COLONCOLON IDENTIFIER)*  # SimpleVariantPattern
    | KW_VAR IDENTIFIER                          # VarIdentifierPattern
    | IDENTIFIER                                 # IdentifierPattern
    | INTEGER_LITERAL                            # LiteralPattern
    | HEX_LITERAL                                # LiteralPattern
    | BINARY_LITERAL                             # LiteralPattern
    | STRING_LITERAL                             # LiteralPattern
    | KW_TRUE                                    # BoolLiteralPattern
    | KW_FALSE                                   # BoolLiteralPattern
    | KW_NULL                                    # NullLiteralPattern
    | UNDERSCORE                                 # WildcardPattern
    ;

variantName
    : IDENTIFIER (COLONCOLON IDENTIFIER)*
    ;

patternList
    : pattern (COMMA pattern)*
    ;

postfixCondition
    : KW_IF expression
    | KW_UNLESS expression
    ;

returnStatement
    : KW_RETURN expression? postfixCondition?
    ;

variableDeclaration
    : KW_LET (IDENTIFIER | UNDERSCORE | tuplePattern) (COLON type)? EQ expression    // Immutable binding
    | KW_LET (IDENTIFIER | UNDERSCORE | tuplePattern) COLON type                      // Immutable, uninitialized
    | KW_LET (IDENTIFIER | UNDERSCORE | tuplePattern)                                 // Immutable, uninitialized
    | KW_VAR (IDENTIFIER | UNDERSCORE | tuplePattern) (COLON type)? EQ expression    // Mutable binding
    | KW_VAR (IDENTIFIER | UNDERSCORE | tuplePattern) COLON type                      // Mutable, uninitialized
    | KW_VAR (IDENTIFIER | UNDERSCORE | tuplePattern)                                 // Mutable, uninitialized
    ;

tuplePattern
    : LPAREN (IDENTIFIER | UNDERSCORE) (COMMA (IDENTIFIER | UNDERSCORE))+ RPAREN
    ;

assignmentStatement
    : (STAR)* (IDENTIFIER | KW_SELF) (lvalueSuffix)* (PLUSPLUS | MINUSMINUS) postfixCondition?
    | (STAR)* (IDENTIFIER | KW_SELF) (lvalueSuffix)* (EQ | PLUSEQ | MINUSEQ | STAREQ | SLASHEQ | PERCENTEQ | AMPEQ | PIPEEQ | CARETEQ | LSHIFT_ASSIGN | RSHIFT_ASSIGN) expression postfixCondition?
    ;

lvalueSuffix
    : DOT IDENTIFIER
    | LBRACKET expression RBRACKET
    ;

ifStatement
    : KW_IF ifCondition block (KW_ELSE (ifStatement | block))?
    ;

ifElseChain
    : KW_IF expression block KW_ELSE (ifElseChain | block)
    ;

ifCondition
    : expression                                           # IfConditionExpression
    | KW_LET IDENTIFIER (COLON type)? EQ expression       # IfConditionLet
    | KW_VAR IDENTIFIER (COLON type)? EQ expression       # IfConditionVar
    ;

whileStatement
    : KW_WHILE expression block                                                              # WhileExpr
    | KW_WHILE KW_VAR IDENTIFIER (COLON type)? comparisonOp expression block                 # WhileVar
    ;

// Comparison operators for while var syntax
comparisonOp
    : LESS | GREATER | LE | GE | EQEQ | NE
    ;

forStatement
    : KW_FOR (variableDeclaration | assignmentStatement) SEMI expression SEMI assignmentStatement block  # ForCStyle
    | KW_FOR KW_VAR? IDENTIFIER KW_IN expression block                                                   # ForInLoop
    ;

foreverStatement
    : KW_FOREVER block
    ;

breakStatement
    : KW_BREAK IDENTIFIER? postfixCondition?
    ;

continueStatement
    : KW_CONTINUE IDENTIFIER? postfixCondition?
    ;

expressionStatement
    : expression postfixCondition?
    ;

expression
    : primaryExpression                                     # PrimaryExpr
    | expression COLONCOLON genericTypeArgs                # TurboFishExpr
    | expression COLONCOLON IDENTIFIER                     # PathExpr
    | expression NEWLINE* DOT IDENTIFIER                   # MemberAccessExpr
    | expression LPAREN argumentList? RPAREN               # CallExpr
    | expression LBRACKET expression RBRACKET              # IndexExpr
    | expression PLUSPLUS                                  # PostIncrementExpr
    | expression MINUSMINUS                                # PostDecrementExpr
    | expression QUESTION                                  # TryExpr
    | expression KW_AS type                                # AsCastExpr
    | AMPERSAND KW_VAR? expression                         # BorrowExpr
    | (BANG | TILDE | MINUS) expression                    # UnaryExpr
    | PLUSPLUS expression                                  # PreIncrementExpr
    | MINUSMINUS expression                                # PreDecrementExpr
    | STAR expression                                      # DereferenceExpr
    | expression (STAR | SLASH | PERCENT) expression       # MultiplicativeExpr
    | expression (PLUS | MINUS) expression                 # AdditiveExpr
    | expression LSHIFT expression                         # ShiftExpr
    | expression RSHIFT expression                         # ShiftExpr
    | expression AMPERSAND expression                      # BitwiseAndExpr
    | expression CARET expression                          # BitwiseXorExpr
    | expression PIPE expression                           # BitwiseOrExpr
    | expression DOTDOT expression                         # RangeExpr
    | expression DOTDOTEQ expression                       # RangeInclusiveExpr
    | expression (EQEQ | NE | LESS | GREATER | LE | GE) expression  # ComparisonExpr
    | expression ANDAND expression                         # LogicalAndExpr
    | expression OROR expression                           # LogicalOrExpr
    ;

argumentList
    : NEWLINE* expression (COMMA NEWLINE* expression)* (COMMA NEWLINE*)? NEWLINE*
    ;

primaryExpression
    : KW_TRUE                                      # BoolLiteral
    | KW_FALSE                                     # BoolLiteral
    | KW_NULL                                      # NullLiteral
    | KW_SELF                                      # SelfExpr
    | AT_SIGN? KW_SIZEOF LPAREN type RPAREN       # SizeofExpr
    | F_STRING_LITERAL                             # InterpolatedStringLiteral
    | CHAR_LITERAL                                 # CharLiteral
    | STRING_LITERAL                               # StringLiteral
    | MINUS? FLOAT_LITERAL                         # FloatLiteral
    | MINUS? INTEGER_LITERAL                       # IntegerLiteral
    | MINUS? BINARY_LITERAL                        # BinaryLiteral
    | MINUS? HEX_LITERAL                           # HexLiteral
    | typeName LBRACE NEWLINE* arrayLiteralInit NEWLINE* RBRACE  # StructArrayInit
    | typeName LBRACE NEWLINE* structFieldInit (COMMA NEWLINE* structFieldInit)* (COMMA NEWLINE* DOTDOT expression)? COMMA? NEWLINE* RBRACE  # StructLiteral
    | identifier                                   # IdentifierExpr
    | KW_MATCH expression LBRACE NEWLINE* matchArm (COMMA NEWLINE* matchArm)* COMMA? NEWLINE* RBRACE  # MatchExpr
    | KW_IF expression block KW_ELSE (ifElseChain | block)  # IfExpr
    | KW_UNSAFE block                              # UnsafeExpr
    | asmExpression                                # AsmExpr
    | copperList                                   # CopperExpr
    | blitterJob                                   # BlitterExpr
    | LPAREN RPAREN                                # UnitLiteral
    | LPAREN type RPAREN expression                # CastExpr
    | LPAREN NEWLINE* expression (COMMA NEWLINE* expression)+ NEWLINE* RPAREN  # TupleLiteral
    | LPAREN NEWLINE* expression NEWLINE* RPAREN  # ParenExpr
    | LBRACKET NEWLINE* expression NEWLINE* SEMI NEWLINE* expression NEWLINE* RBRACKET  # ArrayRepeatLiteral
    | LBRACKET NEWLINE* (expression (COMMA NEWLINE* expression)* (COMMA NEWLINE*)?)? NEWLINE* RBRACKET  # ArrayLiteral
    | KW_MATCHES LPAREN expression COMMA NEWLINE* pattern NEWLINE* RPAREN  # MatchesExpr
    | KW_DBG LPAREN expression RPAREN                                       # DbgExpr
    | KW_UNREACHABLE LPAREN RPAREN                                          # UnreachableExpr
    | closureExpression                                                     # ClosureExpr
    ;

identifier
    : IDENTIFIER (COLONCOLON IDENTIFIER)*
    ;

// Closure expressions: |x: i32, y: i32| -> i32 { x + y }
// Supports:
//   - |x: i32| -> i32 { x + 1 }          - with return type
//   - |x: i32| { x + 1 }                 - without return type (inferred)
//   - || { 42 }                          - no parameters
//   - |mut x| { x += 1; x }              - mutable capture (captures by reference)
closureExpression
    : PIPE closureParameterList? PIPE (ARROW type)? block
    ;

closureParameterList
    : closureParameter (COMMA NEWLINE* closureParameter)*
    ;

// Closure parameter can be:
//   - Regular parameter: x: i32
//   - Mutable capture: var x (captures existing variable by reference)
//   - Reference capture: &x (captures by reference for large types)
closureParameter
    : KW_VAR IDENTIFIER                    # MutableCaptureParam
    | AMPERSAND IDENTIFIER                 # ReferenceCaptureParam
    | IDENTIFIER COLON type                # TypedClosureParam
    ;

arrayLiteralInit
    : LBRACKET NEWLINE* (expression (COMMA NEWLINE* expression)* (COMMA NEWLINE*)?)? NEWLINE* RBRACKET
    ;

structFieldInit
    : IDENTIFIER COLON NEWLINE* expression
    ;

copperList
    : KW_COPPER LBRACE NEWLINE* copperOperation* NEWLINE* RBRACE
    ;

copperOperation
    : copperOpName LPAREN expression COMMA expression RPAREN SEMI? NEWLINE*
    ;

copperOpName
    : IDENTIFIER
    ;

blitterJob
    : KW_BLITTER LBRACE NEWLINE* blitterField (COMMA NEWLINE* blitterField)* COMMA? NEWLINE* RBRACE
    ;

blitterField
    : IDENTIFIER COLON expression
    ;

// Inline Assembly
asmStatement
    : KW_UNSAFE KW_ASM LPAREN asmInputList? RPAREN NEWLINE* asmReturnSpec? NEWLINE* asmUseClause? NEWLINE* asmVolatile? NEWLINE* asmClobbers? NEWLINE* asmBlock
    ;

asmExpression
    : KW_UNSAFE KW_ASM LPAREN asmInputList? RPAREN NEWLINE* asmReturnSpec? NEWLINE* asmUseClause? NEWLINE* asmVolatile? NEWLINE* asmClobbers? NEWLINE* asmBlock
    ;

asmInputList
    : asmInput (COMMA NEWLINE* asmInput)*
    ;

// Input binding: supports &var (address-of), mut var (mutable), out reg (output binding)
// Examples:
//   x in d0              - simple input
//   &buffer in a0        - address of variable
//   var counter in d0 out d0  - mutable with writeback
asmInput
    : AMPERSAND? KW_VAR? IDENTIFIER (EQ NEWLINE* expression)? asmRegisterBinding? asmOutputBinding?
    ;

asmRegisterBinding
    : KW_IN asmRegister
    ;

// Output binding for writeback (used with mut)
asmOutputBinding
    : ASM_HDR_OUT asmRegister
    ;

// Use clause for compile-time constants
// Examples:
//   use(sizeof(Player), offsetof(Player, health))
asmUseClause
    : KW_USE LPAREN asmUseItem (COMMA NEWLINE* asmUseItem)* RPAREN
    ;

asmUseItem
    : KW_SIZEOF LPAREN type RPAREN                      // sizeof(Type)
    | KW_OFFSETOF LPAREN type COMMA IDENTIFIER RPAREN   // offsetof(Type, field)
    | KW_ALIGNOF LPAREN type RPAREN                     // alignof(Type)
    | KW_CONST IDENTIFIER                               // const CONSTANT_NAME
    ;

asmReturnSpec
    : ARROW type asmRegisterBinding?
    | ARROW LPAREN asmMultiReturn RPAREN
    ;

asmMultiReturn
    : type KW_IN asmRegister (COMMA NEWLINE* type KW_IN asmRegister)+
    ;

asmVolatile
    : KW_VOLATILE
    ;

asmClobbers
    : KW_CLOBBERS LPAREN asmClobberList RPAREN
    ;

asmClobberList
    : asmClobberItem (COMMA NEWLINE* asmClobberItem)*
    ;

asmClobberItem
    : IDENTIFIER
    ;

asmRegister
    : IDENTIFIER
    ;

// Assembly block - accepts raw assembly syntax (lexed via ASM_MODE)
// The LBRACE triggers ASM_MODE in the lexer, which produces ASM_* tokens.
// RBRACE is emitted with type RBRACE by ASM_RBRACE rule.
asmBlock
    : LBRACE NEWLINE* asmInstruction* RBRACE
    ;

// Assembly instruction - can be a label, mnemonic with operands, or legacy quoted string
asmInstruction
    : asmLabel asmMnemonic? NEWLINE*                    // label: [instruction]
    | asmMnemonic asmOperandList? NEWLINE*              // instruction [operands]
    | STRING_LITERAL NEWLINE*                           // legacy quoted string syntax
    | NEWLINE                                           // empty line
    ;

asmLabel
    : ASM_LABEL
    ;

asmMnemonic
    : ASM_MNEMONIC
    ;

asmOperandList
    : asmOperand (ASM_COMMA asmOperand)*
    ;

asmOperand
    : ASM_IMMEDIATE                                     // #42, #$FF, #%1010
    | ASM_REGLIST ASM_BITFIELD?                         // d0-d7/a2-a5 (for movem), with optional bitfield
    | ASM_REGISTER ASM_BITFIELD?                        // d0, a0, sp, pc (with optional bitfield for bfxxx)
    | ASM_HEX                                           // $DFF006
    | ASM_BINARY_NUM                                    // %10101010
    | ASM_NUMBER                                        // 42, -5
    | ASM_LABEL_REF ASM_BITFIELD?                       // label reference with optional bitfield
    | ASM_ADDRESS_MODE ASM_BITFIELD?                    // (a0), (a0)+, -(a0), with optional bitfield
    | ASM_BITFIELD                                      // {0:32} standalone
    ;
