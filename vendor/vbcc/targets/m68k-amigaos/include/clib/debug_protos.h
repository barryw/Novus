#ifndef  CLIB_DEBUG_PROTOS_H
#define  CLIB_DEBUG_PROTOS_H

/*
**	$VER: debug_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/


#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  EXEC_TYPES_H
#include <exec/types.h>
#endif

/* debug support functions in debug.lib */

LONG KCmpStr( CONST_STRPTR a, CONST_STRPTR b );
LONG cmpstrexec( CONST_STRPTR a, CONST_STRPTR b );
LONG KGetChar( VOID );
LONG kgetc( VOID );
LONG kgetchar( VOID );
LONG kgetch( VOID );
LONG KGetCh( VOID );
LONG KGetNum( VOID );
LONG kgetnum( VOID );
LONG KMayGetChar( VOID );
LONG KMayGetCh( VOID );
VOID KPutFmt( CONST_STRPTR formatString, CONST_APTR values );
VOID KVPrintF( CONST_STRPTR formatString, CONST_APTR values );
VOID KPrintF( CONST_STRPTR formatString, ... );
VOID kprintf( CONST_STRPTR formatString, ... );
VOID KPutChar( LONG ch );
VOID kputc( LONG ch );
VOID kputchar( LONG ch );
VOID kputch( LONG ch );
VOID KPutCh( LONG ch );
VOID KPutStr( CONST_STRPTR string );
VOID kputs( CONST_STRPTR string );
VOID KPutS( CONST_STRPTR string );
VOID KDoFmt( CONST_STRPTR formatString, CONST_APTR dataStream, CONST_APTR putChProc, APTR putChData );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif   /* CLIB_DEBUG_PROTOS_H */
