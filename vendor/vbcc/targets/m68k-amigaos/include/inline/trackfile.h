#ifndef _INLINE_TRACKFILE_H
#define _INLINE_TRACKFILE_H

#ifndef CLIB_TRACKFILE_PROTOS_H
#define CLIB_TRACKFILE_PROTOS_H
#endif

#ifndef  DEVICES_TRACKFILE_H
#include <devices/trackfile.h>
#endif

#ifndef TRACKFILE_BASE_NAME
#define TRACKFILE_BASE_NAME TrackFileBase
#endif

#define TFStartUnitTagList(which_unit, tags) ({ \
  LONG _TFStartUnitTagList_which_unit = (which_unit); \
  CONST struct TagItem * _TFStartUnitTagList_tags = (tags); \
  ({ \
  register char * _TFStartUnitTagList__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_TFStartUnitTagList__bn - 42))(_TFStartUnitTagList__bn, _TFStartUnitTagList_which_unit, _TFStartUnitTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___TFStartUnitTags(struct Device * TrackFileBase, LONG which_unit, ...)
{
  return TFStartUnitTagList(which_unit, (CONST struct TagItem *) ((ULONG) &which_unit + sizeof(LONG)));
}

#define TFStartUnitTags(which_unit...) ___TFStartUnitTags(TRACKFILE_BASE_NAME, which_unit)
#endif

#define TFStopUnitTagList(which_unit, tags) ({ \
  LONG _TFStopUnitTagList_which_unit = (which_unit); \
  CONST struct TagItem * _TFStopUnitTagList_tags = (tags); \
  ({ \
  register char * _TFStopUnitTagList__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_TFStopUnitTagList__bn - 48))(_TFStopUnitTagList__bn, _TFStopUnitTagList_which_unit, _TFStopUnitTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___TFStopUnitTags(struct Device * TrackFileBase, LONG which_unit, ...)
{
  return TFStopUnitTagList(which_unit, (CONST struct TagItem *) ((ULONG) &which_unit + sizeof(LONG)));
}

#define TFStopUnitTags(which_unit...) ___TFStopUnitTags(TRACKFILE_BASE_NAME, which_unit)
#endif

#define TFInsertMediaTagList(which_unit, tags) ({ \
  LONG _TFInsertMediaTagList_which_unit = (which_unit); \
  CONST struct TagItem * _TFInsertMediaTagList_tags = (tags); \
  ({ \
  register char * _TFInsertMediaTagList__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_TFInsertMediaTagList__bn - 54))(_TFInsertMediaTagList__bn, _TFInsertMediaTagList_which_unit, _TFInsertMediaTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___TFInsertMediaTags(struct Device * TrackFileBase, LONG which_unit, ...)
{
  return TFInsertMediaTagList(which_unit, (CONST struct TagItem *) ((ULONG) &which_unit + sizeof(LONG)));
}

#define TFInsertMediaTags(which_unit...) ___TFInsertMediaTags(TRACKFILE_BASE_NAME, which_unit)
#endif

#define TFEjectMediaTagList(which_unit, tags) ({ \
  LONG _TFEjectMediaTagList_which_unit = (which_unit); \
  CONST struct TagItem * _TFEjectMediaTagList_tags = (tags); \
  ({ \
  register char * _TFEjectMediaTagList__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_TFEjectMediaTagList__bn - 60))(_TFEjectMediaTagList__bn, _TFEjectMediaTagList_which_unit, _TFEjectMediaTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___TFEjectMediaTags(struct Device * TrackFileBase, LONG which_unit, ...)
{
  return TFEjectMediaTagList(which_unit, (CONST struct TagItem *) ((ULONG) &which_unit + sizeof(LONG)));
}

#define TFEjectMediaTags(which_unit...) ___TFEjectMediaTags(TRACKFILE_BASE_NAME, which_unit)
#endif

#define TFGetUnitData(which_unit) ({ \
  LONG _TFGetUnitData_which_unit = (which_unit); \
  ({ \
  register char * _TFGetUnitData__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((struct TrackFileUnitData * (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_TFGetUnitData__bn - 66))(_TFGetUnitData__bn, _TFGetUnitData_which_unit); \
});})

#define TFFreeUnitData(tfud) ({ \
  struct TrackFileUnitData * _TFFreeUnitData_tfud = (tfud); \
  ({ \
  register char * _TFFreeUnitData__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct TrackFileUnitData * __asm("a0"))) \
  (_TFFreeUnitData__bn - 72))(_TFFreeUnitData__bn, _TFFreeUnitData_tfud); \
});})

#define TFChangeUnitTagList(which_unit, tags) ({ \
  LONG _TFChangeUnitTagList_which_unit = (which_unit); \
  CONST struct TagItem * _TFChangeUnitTagList_tags = (tags); \
  ({ \
  register char * _TFChangeUnitTagList__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_TFChangeUnitTagList__bn - 78))(_TFChangeUnitTagList__bn, _TFChangeUnitTagList_which_unit, _TFChangeUnitTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___TFChangeUnitTags(struct Device * TrackFileBase, LONG which_unit, ...)
{
  return TFChangeUnitTagList(which_unit, (CONST struct TagItem *) ((ULONG) &which_unit + sizeof(LONG)));
}

#define TFChangeUnitTags(which_unit...) ___TFChangeUnitTags(TRACKFILE_BASE_NAME, which_unit)
#endif

#define TFExamineFileSize(file_size) ({ \
  LONG _TFExamineFileSize_file_size = (file_size); \
  ({ \
  register char * _TFExamineFileSize__bn __asm("a6") = (char *) (TRACKFILE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_TFExamineFileSize__bn - 84))(_TFExamineFileSize__bn, _TFExamineFileSize_file_size); \
});})

#endif /*  _INLINE_TRACKFILE_H  */
