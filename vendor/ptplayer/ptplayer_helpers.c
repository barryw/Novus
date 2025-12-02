/*
 * ptplayer_helpers.c - Helper functions for Novus ptplayer integration
 *
 * This file provides C-callable access to ptplayer's global variables.
 * The actual ptplayer function wrappers are in ptplayer_stubs.asm because
 * ptplayer uses a register-based calling convention (a6=CUSTOM, a0/a1/d0/d1 for args)
 * which is difficult to call correctly from C without assembly stubs.
 *
 * Architecture:
 *   Novus code -> C declarations (ptplayer.novus)
 *              -> C helper funcs (this file) for variables
 *              -> ASM stubs (ptplayer_stubs.asm) for function calls
 *              -> ptplayer (ptplayer.asm)
 *
 * Note: __novus_get_vbr() is implemented in ptplayer_stubs.asm because
 * it requires supervisor mode to read the VBR register using movec.
 */

#include <stdint.h>

/* External variables from ptplayer.asm
 * ptplayer exports these with double underscore prefix in assembly (__mt_Enable)
 * C code must reference with single underscore (_mt_Enable) so VBCC's name
 * mangling adds one more to make it __mt_Enable in the object file
 */
extern unsigned char _mt_Enable;
extern unsigned char _mt_E8Trigger;
extern unsigned char _mt_MusicChannels;
extern unsigned char _mt_SongEnd;
extern unsigned char _mt_VUMeter;

/* Variable access functions */

void __novus_ptplayer_set_enable(unsigned char value) {
    _mt_Enable = value;
}

unsigned char __novus_ptplayer_get_enable(void) {
    return _mt_Enable;
}

unsigned char __novus_ptplayer_get_e8_trigger(void) {
    return _mt_E8Trigger;
}

unsigned char __novus_ptplayer_get_music_channels(void) {
    return _mt_MusicChannels;
}

void __novus_ptplayer_set_music_channels(unsigned char value) {
    _mt_MusicChannels = value;
}

unsigned char __novus_ptplayer_get_song_end(void) {
    return _mt_SongEnd;
}

void __novus_ptplayer_set_song_end(unsigned char value) {
    _mt_SongEnd = value;
}

unsigned char __novus_ptplayer_get_vumeter(void) {
    unsigned char val = _mt_VUMeter;
    _mt_VUMeter = 0;  /* Clear after reading (as per ptplayer docs) */
    return val;
}

unsigned char __novus_ptplayer_peek_vumeter(void) {
    return _mt_VUMeter;  /* Read without clearing */
}

/* Callback invoker - calls a function pointer with a u8 argument
 * This allows Novus code to call function pointers stored as u32 addresses
 */
void __novus_call_u8_callback(uint32_t func_addr, unsigned char arg) {
    if (func_addr != 0) {
        /* Cast address to function pointer and call */
        typedef void (*callback_fn)(unsigned char);
        callback_fn fn = (callback_fn)func_addr;
        fn(arg);
    }
}

/* Song position and length are accessed via assembly stubs in ptplayer_stubs.asm
 * because calculating offsets from C is error-prone with all the conditional assembly.
 *
 * These are declared in ptplayer_stubs.asm and access ptplayer's internal state directly.
 */
