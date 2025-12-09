#ifndef _INLINE_CONSOLE_H
#define _INLINE_CONSOLE_H

#ifndef CLIB_CONSOLE_PROTOS_H
#define CLIB_CONSOLE_PROTOS_H
#endif

#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif
#ifndef  DEVICES_INPUTEVENT_H
#include <devices/inputevent.h>
#endif
#ifndef  DEVICES_KEYMAP_H
#include <devices/keymap.h>
#endif

#ifndef CONSOLE_BASE_NAME
#define CONSOLE_BASE_NAME ConsoleDevice
#endif

#define CDInputHandler(events, consoleDevice) ({ \
  CONST struct InputEvent * _CDInputHandler_events = (events); \
  struct Library * _CDInputHandler_consoleDevice = (consoleDevice); \
  ({ \
  register char * _CDInputHandler__bn __asm("a6") = (char *) (CONSOLE_BASE_NAME);\
  ((struct InputEvent * (*)(char * __asm("a6"), CONST struct InputEvent * __asm("a0"), struct Library * __asm("a1"))) \
  (_CDInputHandler__bn - 42))(_CDInputHandler__bn, _CDInputHandler_events, _CDInputHandler_consoleDevice); \
});})

#define RawKeyConvert(events, buffer, length, keyMap) ({ \
  CONST struct InputEvent * _RawKeyConvert_events = (events); \
  STRPTR _RawKeyConvert_buffer = (buffer); \
  LONG _RawKeyConvert_length = (length); \
  CONST struct KeyMap * _RawKeyConvert_keyMap = (keyMap); \
  ({ \
  register char * _RawKeyConvert__bn __asm("a6") = (char *) (CONSOLE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct InputEvent * __asm("a0"), STRPTR __asm("a1"), LONG __asm("d1"), CONST struct KeyMap * __asm("a2"))) \
  (_RawKeyConvert__bn - 48))(_RawKeyConvert__bn, _RawKeyConvert_events, _RawKeyConvert_buffer, _RawKeyConvert_length, _RawKeyConvert_keyMap); \
});})

#endif /*  _INLINE_CONSOLE_H  */
