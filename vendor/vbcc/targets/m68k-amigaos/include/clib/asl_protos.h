#ifndef CLIB_ASL_PROTOS_H
#define CLIB_ASL_PROTOS_H

/*
**	$VER: asl_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  LIBRARIES_ASL_H
#include <libraries/asl.h>
#endif
/*--- functions in V36 or higher (Release 2.0) ---*/

/* OBSOLETE -- Please use the generic requester functions instead */

struct FileRequester *AllocFileRequest( VOID );
VOID FreeFileRequest( struct FileRequester *fileReq );
BOOL RequestFile( struct FileRequester *fileReq );
APTR AllocAslRequest( ULONG reqType, CONST struct TagItem *tagList );
APTR AllocAslRequestTags( ULONG reqType, ... );
VOID FreeAslRequest( APTR requester );
BOOL AslRequest( APTR requester, CONST struct TagItem *tagList );
BOOL AslRequestTags( APTR requester, ... );
VOID AbortAslRequest( APTR requester );
VOID ActivateAslRequest( APTR requester );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_ASL_PROTOS_H */
