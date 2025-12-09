#ifndef _INLINE_KEYMAP_H
#define _INLINE_KEYMAP_H

#ifndef CLIB_KEYMAP_PROTOS_H
#define CLIB_KEYMAP_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DEVICES_INPUTEVENT_H
#include <devices/inputevent.h>
#endif
#ifndef  DEVICES_KEYMAP_H
#include <devices/keymap.h>
#endif

#ifndef KEYMAP_BASE_NAME
#define KEYMAP_BASE_NAME KeymapBase
#endif

#define SetKeyMapDefault(keyMap) ({ \
  struct KeyMap * _SetKeyMapDefault_keyMap = (keyMap); \
  ({ \
  register char * _SetKeyMapDefault__bn __asm("a6") = (char *) (KEYMAP_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct KeyMap * __asm("a0"))) \
  (_SetKeyMapDefault__bn - 30))(_SetKeyMapDefault__bn, _SetKeyMapDefault_keyMap); \
});})

#define AskKeyMapDefault() ({ \
  register char * _AskKeyMapDefault__bn __asm("a6") = (char *) (KEYMAP_BASE_NAME);\
  ((struct KeyMap * (*)(char * __asm("a6"))) \
  (_AskKeyMapDefault__bn - 36))(_AskKeyMapDefault__bn); \
})

#define MapRawKey(event, buffer, length, keyMap) ({ \
  CONST struct InputEvent * _MapRawKey_event = (event); \
  STRPTR _MapRawKey_buffer = (buffer); \
  LONG _MapRawKey_length = (length); \
  CONST struct KeyMap * _MapRawKey_keyMap = (keyMap); \
  ({ \
  register char * _MapRawKey__bn __asm("a6") = (char *) (KEYMAP_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), CONST struct InputEvent * __asm("a0"), STRPTR __asm("a1"), LONG __asm("d1"), CONST struct KeyMap * __asm("a2"))) \
  (_MapRawKey__bn - 42))(_MapRawKey__bn, _MapRawKey_event, _MapRawKey_buffer, _MapRawKey_length, _MapRawKey_keyMap); \
});})

#define MapANSI(string, count, buffer, length, keyMap) ({ \
  CONST_STRPTR _MapANSI_string = (string); \
  LONG _MapANSI_count = (count); \
  STRPTR _MapANSI_buffer = (buffer); \
  LONG _MapANSI_length = (length); \
  CONST struct KeyMap * _MapANSI_keyMap = (keyMap); \
  ({ \
  register char * _MapANSI__bn __asm("a6") = (char *) (KEYMAP_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), LONG __asm("d0"), STRPTR __asm("a1"), LONG __asm("d1"), CONST struct KeyMap * __asm("a2"))) \
  (_MapANSI__bn - 48))(_MapANSI__bn, _MapANSI_string, _MapANSI_count, _MapANSI_buffer, _MapANSI_length, _MapANSI_keyMap); \
});})

#endif /*  _INLINE_KEYMAP_H  */
