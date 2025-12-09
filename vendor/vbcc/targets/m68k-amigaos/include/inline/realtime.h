#ifndef _INLINE_REALTIME_H
#define _INLINE_REALTIME_H

#ifndef CLIB_REALTIME_PROTOS_H
#define CLIB_REALTIME_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  LIBRARIES_REALTIME_H
#include <libraries/realtime.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif

#ifndef REALTIME_BASE_NAME
#define REALTIME_BASE_NAME RealTimeBase
#endif

#define LockRealTime(lockType) ({ \
  ULONG _LockRealTime_lockType = (lockType); \
  ({ \
  register char * _LockRealTime__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_LockRealTime__bn - 30))(_LockRealTime__bn, _LockRealTime_lockType); \
});})

#define UnlockRealTime(lock) ({ \
  APTR _UnlockRealTime_lock = (lock); \
  ({ \
  register char * _UnlockRealTime__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_UnlockRealTime__bn - 36))(_UnlockRealTime__bn, _UnlockRealTime_lock); \
});})

#define CreatePlayerA(tagList) ({ \
  CONST struct TagItem * _CreatePlayerA_tagList = (tagList); \
  ({ \
  register char * _CreatePlayerA__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((struct Player * (*)(char * __asm("a6"), CONST struct TagItem * __asm("a0"))) \
  (_CreatePlayerA__bn - 42))(_CreatePlayerA__bn, _CreatePlayerA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Player * ___CreatePlayer(struct RealTimeBase * RealTimeBase, Tag tagList, ...)
{
  return CreatePlayerA((CONST struct TagItem *) &tagList);
}

#define CreatePlayer(tags...) ___CreatePlayer(REALTIME_BASE_NAME, tags)
#endif

#define DeletePlayer(player) ({ \
  struct Player * _DeletePlayer_player = (player); \
  ({ \
  register char * _DeletePlayer__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Player * __asm("a0"))) \
  (_DeletePlayer__bn - 48))(_DeletePlayer__bn, _DeletePlayer_player); \
});})

#define SetPlayerAttrsA(player, tagList) ({ \
  struct Player * _SetPlayerAttrsA_player = (player); \
  CONST struct TagItem * _SetPlayerAttrsA_tagList = (tagList); \
  ({ \
  register char * _SetPlayerAttrsA__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Player * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetPlayerAttrsA__bn - 54))(_SetPlayerAttrsA__bn, _SetPlayerAttrsA_player, _SetPlayerAttrsA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___SetPlayerAttrs(struct RealTimeBase * RealTimeBase, struct Player * player, ...)
{
  return SetPlayerAttrsA(player, (CONST struct TagItem *) ((ULONG) &player + sizeof(struct Player *)));
}

#define SetPlayerAttrs(player...) ___SetPlayerAttrs(REALTIME_BASE_NAME, player)
#endif

#define SetConductorState(player, state, time) ({ \
  struct Player * _SetConductorState_player = (player); \
  ULONG _SetConductorState_state = (state); \
  LONG _SetConductorState_time = (time); \
  ({ \
  register char * _SetConductorState__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Player * __asm("a0"), ULONG __asm("d0"), LONG __asm("d1"))) \
  (_SetConductorState__bn - 60))(_SetConductorState__bn, _SetConductorState_player, _SetConductorState_state, _SetConductorState_time); \
});})

#define ExternalSync(player, minTime, maxTime) ({ \
  struct Player * _ExternalSync_player = (player); \
  LONG _ExternalSync_minTime = (minTime); \
  LONG _ExternalSync_maxTime = (maxTime); \
  ({ \
  register char * _ExternalSync__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Player * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_ExternalSync__bn - 66))(_ExternalSync__bn, _ExternalSync_player, _ExternalSync_minTime, _ExternalSync_maxTime); \
});})

#define NextConductor(previousConductor) ({ \
  CONST struct Conductor * _NextConductor_previousConductor = (previousConductor); \
  ({ \
  register char * _NextConductor__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((struct Conductor * (*)(char * __asm("a6"), CONST struct Conductor * __asm("a0"))) \
  (_NextConductor__bn - 72))(_NextConductor__bn, _NextConductor_previousConductor); \
});})

#define FindConductor(name) ({ \
  CONST_STRPTR _FindConductor_name = (name); \
  ({ \
  register char * _FindConductor__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((struct Conductor * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_FindConductor__bn - 78))(_FindConductor__bn, _FindConductor_name); \
});})

#define GetPlayerAttrsA(player, tagList) ({ \
  struct Player * _GetPlayerAttrsA_player = (player); \
  CONST struct TagItem * _GetPlayerAttrsA_tagList = (tagList); \
  ({ \
  register char * _GetPlayerAttrsA__bn __asm("a6") = (char *) (REALTIME_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Player * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_GetPlayerAttrsA__bn - 84))(_GetPlayerAttrsA__bn, _GetPlayerAttrsA_player, _GetPlayerAttrsA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___GetPlayerAttrs(struct RealTimeBase * RealTimeBase, struct Player * player, ...)
{
  return GetPlayerAttrsA(player, (CONST struct TagItem *) ((ULONG) &player + sizeof(struct Player *)));
}

#define GetPlayerAttrs(player...) ___GetPlayerAttrs(REALTIME_BASE_NAME, player)
#endif

#endif /*  _INLINE_REALTIME_H  */
