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
