#ifndef _INLINE_TRANSLATOR_H
#define _INLINE_TRANSLATOR_H

#ifndef CLIB_TRANSLATOR_PROTOS_H
#define CLIB_TRANSLATOR_PROTOS_H
#endif

#ifndef  EXEC_TYPES_H
#include <exec/types.h>
#endif

#ifndef TRANSLATOR_BASE_NAME
#define TRANSLATOR_BASE_NAME TranslatorBase
#endif

#define Translate(inputString, inputLength, outputBuffer, bufferSize) ({ \
  CONST_STRPTR _Translate_inputString = (inputString); \
  LONG _Translate_inputLength = (inputLength); \
  STRPTR _Translate_outputBuffer = (outputBuffer); \
  LONG _Translate_bufferSize = (bufferSize); \
  ({ \
  register char * _Translate__bn __asm("a6") = (char *) (TRANSLATOR_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), LONG __asm("d0"), STRPTR __asm("a1"), LONG __asm("d1"))) \
  (_Translate__bn - 30))(_Translate__bn, _Translate_inputString, _Translate_inputLength, _Translate_outputBuffer, _Translate_bufferSize); \
});})

#endif /*  _INLINE_TRANSLATOR_H  */
