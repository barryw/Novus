#ifndef _INLINE_BULLET_H
#define _INLINE_BULLET_H

#ifndef CLIB_BULLET_PROTOS_H
#define CLIB_BULLET_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  DISKFONT_GLYPH_H
#include <diskfont/glyph.h>
#endif

#ifndef BULLET_BASE_NAME
#define BULLET_BASE_NAME BulletBase
#endif

#define OpenEngine() ({ \
  register char * _OpenEngine__bn __asm("a6") = (char *) (BULLET_BASE_NAME);\
  ((struct GlyphEngine * (*)(char * __asm("a6"))) \
  (_OpenEngine__bn - 30))(_OpenEngine__bn); \
})

#define CloseEngine(glyphEngine) ({ \
  struct GlyphEngine * _CloseEngine_glyphEngine = (glyphEngine); \
  ({ \
  register char * _CloseEngine__bn __asm("a6") = (char *) (BULLET_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct GlyphEngine * __asm("a0"))) \
  (_CloseEngine__bn - 36))(_CloseEngine__bn, _CloseEngine_glyphEngine); \
});})

#define SetInfoA(glyphEngine, tagList) ({ \
  struct GlyphEngine * _SetInfoA_glyphEngine = (glyphEngine); \
  CONST struct TagItem * _SetInfoA_tagList = (tagList); \
  ({ \
  register char * _SetInfoA__bn __asm("a6") = (char *) (BULLET_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct GlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetInfoA__bn - 42))(_SetInfoA__bn, _SetInfoA_glyphEngine, _SetInfoA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SetInfo(struct Library * BulletBase, struct GlyphEngine * glyphEngine, ...)
{
  return SetInfoA(glyphEngine, (CONST struct TagItem *) ((ULONG) &glyphEngine + sizeof(struct GlyphEngine *)));
}

#define SetInfo(glyphEngine...) ___SetInfo(BULLET_BASE_NAME, glyphEngine)
#endif

#define ObtainInfoA(glyphEngine, tagList) ({ \
  struct GlyphEngine * _ObtainInfoA_glyphEngine = (glyphEngine); \
  CONST struct TagItem * _ObtainInfoA_tagList = (tagList); \
  ({ \
  register char * _ObtainInfoA__bn __asm("a6") = (char *) (BULLET_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct GlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ObtainInfoA__bn - 48))(_ObtainInfoA__bn, _ObtainInfoA_glyphEngine, _ObtainInfoA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___ObtainInfo(struct Library * BulletBase, struct GlyphEngine * glyphEngine, ...)
{
  return ObtainInfoA(glyphEngine, (CONST struct TagItem *) ((ULONG) &glyphEngine + sizeof(struct GlyphEngine *)));
}

#define ObtainInfo(glyphEngine...) ___ObtainInfo(BULLET_BASE_NAME, glyphEngine)
#endif

#define ReleaseInfoA(glyphEngine, tagList) ({ \
  struct GlyphEngine * _ReleaseInfoA_glyphEngine = (glyphEngine); \
  CONST struct TagItem * _ReleaseInfoA_tagList = (tagList); \
  ({ \
  register char * _ReleaseInfoA__bn __asm("a6") = (char *) (BULLET_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct GlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ReleaseInfoA__bn - 54))(_ReleaseInfoA__bn, _ReleaseInfoA_glyphEngine, _ReleaseInfoA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___ReleaseInfo(struct Library * BulletBase, struct GlyphEngine * glyphEngine, ...)
{
  return ReleaseInfoA(glyphEngine, (CONST struct TagItem *) ((ULONG) &glyphEngine + sizeof(struct GlyphEngine *)));
}

#define ReleaseInfo(glyphEngine...) ___ReleaseInfo(BULLET_BASE_NAME, glyphEngine)
#endif

#endif /*  _INLINE_BULLET_H  */
