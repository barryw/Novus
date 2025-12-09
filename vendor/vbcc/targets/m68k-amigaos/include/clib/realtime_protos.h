#ifndef CLIB_REALTIME_PROTOS_H
#define CLIB_REALTIME_PROTOS_H

/*
**	$VER: realtime_protos.h 47.1 (30.11.2021)
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
#ifndef  LIBRARIES_REALTIME_H
#include <libraries/realtime.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
/*--- functions in V37 or higher (Release 2.04) ---*/

/* Locks */

APTR LockRealTime( ULONG lockType );
VOID UnlockRealTime( APTR lock );

/* Conductor */

struct Player *CreatePlayerA( CONST struct TagItem *tagList );
struct Player *CreatePlayer( Tag tag1, ... );
VOID DeletePlayer( struct Player *player );
BOOL SetPlayerAttrsA( struct Player *player, CONST struct TagItem *tagList );
BOOL SetPlayerAttrs( struct Player *player, ... );
LONG SetConductorState( struct Player *player, ULONG state, LONG time );
BOOL ExternalSync( struct Player *player, LONG minTime, LONG maxTime );
struct Conductor *NextConductor( CONST struct Conductor *previousConductor );
struct Conductor *FindConductor( CONST_STRPTR name );
ULONG GetPlayerAttrsA( struct Player *player, CONST struct TagItem *tagList );
ULONG GetPlayerAttrs( struct Player *player, ... );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_REALTIME_PROTOS_H */
