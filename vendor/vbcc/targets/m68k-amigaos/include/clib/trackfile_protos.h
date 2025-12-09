#ifndef CLIB_TRACKFILE_PROTOS_H
#define CLIB_TRACKFILE_PROTOS_H

/*
**	$VER: trackfile_protos.h 2.3 (10.8.2020)
**
**	'C' prototypes. For use with 32 bit integers only.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  DEVICES_TRACKFILE_H
#include <devices/trackfile.h>
#endif
LONG TFStartUnitTagList( LONG which_unit, CONST struct TagItem *tags );
LONG TFStartUnitTags( LONG which_unit, ... );
LONG TFStopUnitTagList( LONG which_unit, CONST struct TagItem *tags );
LONG TFStopUnitTags( LONG which_unit, ... );
LONG TFInsertMediaTagList( LONG which_unit, CONST struct TagItem *tags );
LONG TFInsertMediaTags( LONG which_unit, ... );
LONG TFEjectMediaTagList( LONG which_unit, CONST struct TagItem *tags );
LONG TFEjectMediaTags( LONG which_unit, ... );
struct TrackFileUnitData *TFGetUnitData( LONG which_unit );
VOID TFFreeUnitData( struct TrackFileUnitData *tfud );

/*--- functions in V2 or higher ---*/

LONG TFChangeUnitTagList( LONG which_unit, CONST struct TagItem *tags );
LONG TFChangeUnitTags( LONG which_unit, ... );
LONG TFExamineFileSize( LONG file_size );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_TRACKFILE_PROTOS_H */
