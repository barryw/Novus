# Pinned NDK bugs

This file records verified defects in the pinned classic 68k AmigaOS NDK 3.9
surface. Raw bindings preserve the header behavior; corrected APIs live above
Tier 3 and explicitly supersede the affected raw declarations.

## TextEditor base redefinition

`gadgets/texteditor.h` defines `TEXTEDITOR_Dummy` as
`REACTION_Dummy + 0x26000`, defines 34 `GA_TEXTEDITOR_*` macros in terms of it,
then undefines and replaces it with `0x45000` for `GM_TEXTEDITOR_*` methods.
C expands referenced macros when they are used, not when they are defined, so
the second definition changes the earlier gadget-tag values as well.

The raw `TEXTEDITOR_Dummy` binding therefore preserves the header's final
`0x45000` value. The affected raw `GA_TEXTEDITOR_*` constants are documented as
buggy and are superseded by documented `TAG_*` constants under
`amiga::ui::text_editor`, which retain the intended `0x85026000` tag base.
`GM_TEXTEDITOR_*` uses the final method base as the header intended.

## Signed left shifts into the sign bit

`libraries/lowlevel.h` defines `JP_TYPE_MASK` as `(15<<28)`, and
`libraries/nonvolatile.h` defines `NVEF_APPNAME` as `(1<<NVEB_APPNAME)` where
`NVEB_APPNAME` is 31. Both expressions shift a signed C `int` beyond its
representable range, which is undefined behavior in C even though classic
Amiga compilers produce the intended `0xF0000000` and `0x80000000` bit masks.

The raw Novus constants retain the intended values with their declared `u32`
type, where the shifts are defined. The constant verifier records these two
symbols as `unsigned_normalized`: it recompiles the authoritative expression
with unsigned literals and still requires an exact match, so the NDK defect is
documented without weakening value verification.

## amigaguide.library 45.7 AGA_Path failure

The pinned autodoc says `GetAmigaGuideAttr(AGA_Path, ...)` returns the current
AmigaGuide path as a `BPTR`. Live A4000 testing against amigaguide.library 45.7
(25 February 2025) opened a real guide relative to a retained, valid `RAM:` DOS
lock, waited for successful asynchronous startup, and held the documented
AmigaGuide client lock. The call still returned zero and left storage at zero.
The same locked client returned a valid initialized `AGA_XRefList`, so the
failure is specific to `AGA_Path`, not the client or raw register binding.

Tier 3 preserves and documents the clean failure. The A4000 test accepts it
only for version 45.7; another version must either return a nameable DOS lock or
fail verification. A safe path query above Tier 3 should retain the caller's
original directory lock as its fallback rather than inventing a pointer.

## amigaguide.library 45.7 invalid string-ID hang

The `GetAmigaGuideString` autodoc promises NULL for an invalid ID. Live A4000
tests confirmed that public `HTERR_NOT_ENOUGH_MEMORY` returns stable non-empty
localized text, but tested invalid IDs `-1` and `9999` never returned from
amigaguide.library 45.7. Both processes remained alive without a Guru until the
external 45-second guest-command timeout; this is therefore a blocking library
defect, not a Novus exception or crash.

Tier 3 documents the precondition and tests only a published ID. Callers must
not probe the ID space. A safe wrapper above Tier 3 should accept a closed enum
of published IDs and reject unknown integers without calling the library.

## amigaguide.library 45.7 duplicate XRef result

The Commodore V34 autodoc defines `LoadXRef` results as `-1` for Ctrl-C, `0`
for failure, `1` for a newly loaded table, and `2` when that table is already
loaded. Live A4000 testing against amigaguide.library 45.7 wrote a valid XREF
file, loaded it successfully, then loaded the identical path again. Both calls
returned `1`; the second should have returned `2`. `ExpungeXRef` remained
functional: loading the same file after expunging again returned `1`.

Tier 3 preserves the documented numeric contract and records the installed
library defect. The runtime test accepts the wrong duplicate result only for
version 45.7; other versions must return `2`.

## intuition.library 47 RemoveGList stale link

The pinned autodoc says that, since V36, `RemoveGList` clears the
`NextGadget` field of the last gadget removed. Live testing attached three
linked gadgets and removed the first two. The function returned ordinal zero
and correctly made the third gadget the window-list head, but the second
removed gadget remained linked to that retained third gadget.

Measured on both verified machines: intuition.library 47.53 on the A4000 and
47.51 on the A1200 behave identically, so the defect is not confined to one
revision. An earlier version of this note claimed 47.53 only, and the runtime
test pinned that exact revision - which failed as soon as a second machine ran
it.

Tier 3 preserves the ABI and documents the installed-library defect. The
runtime test accepts the stale link across Intuition 47 while still rejecting a
link that is neither terminated nor the retained head. Callers should explicitly
clear the removed tail's `NextGadget` before treating the detached chain as an
independently terminated list.

## utility.library wide math prototypes

The NDK 3.9 SFD and `clib/utility_protos.h` declare `SDivMod32`,
`UDivMod32`, `SMult64`, and `UMult64` as returning only `LONG` or `ULONG`,
although the autodocs define two-register results. The division calls return
quotient in D0 and remainder in D1. Live A4000 verification also confirms that
the multiply calls return their 64-bit product low word in D0 and high word in
D1, opposite the Novus/VBCC native wide-result order.

The generated raw declarations therefore use `i64`/`u64`. Division exposes
the documented quotient:remainder pair as high:low bits, while the generated
multiply stubs exchange D0 and D1 so callers receive an ordinary 64-bit
product. This supersedes the former generated scalar declarations, which lost
or misordered half of the documented result.

## graphics.library InitMasks incomplete VSprite BorderLine

The NDK says `InitMasks` creates `CollMask` by ORing both hardware-sprite
bitplanes per row, then creates the one-dimensional `BorderLine` by ORing every
collision row. Live AmigaOS 3.2 verification used three rows with deliberately
disjoint bits. It returned the exact expected collision rows `$8F01`, `$00FF`,
and `$6000`, but wrote `$8FF1` to `BorderLine` instead of their full `$EFFF`
union. The source image remained unchanged.

Tier 3 preserves and documents the operating-system behavior. Code that needs
reliable border collision must recompute `BorderLine` from the completed
`CollMask`; a corrected application API belongs above the raw binding.

## graphics.library FreeGBuffers leaves dangling pointers

Live AmigaOS 3.2 verification confirms that `FreeGBuffers` returns every
allocation made by `GetGBuffers`, including the double-buffer packet and its
buffer, but leaves `Bob.ImageShadow`, `Bob.SaveBuffer`, `Bob.DBuffer`,
`VSprite.CollMask`, and `VSprite.BorderLine` unchanged. All five values point
to released memory after the call. A second raw release or later use can
therefore double-free or corrupt memory.

Tier 3 preserves and documents that operating-system behavior. The raw
`AnimOb` parameter is non-consuming because the function does not free the
object or its component graph. Safe GELS APIs must clear the five fields after
calling the raw function and prevent a second release.

## graphics.library SetChipRev omits its internal revision bit

The pinned autodoc says `SetChipRev` returns the actual bits in
`GfxBase->ChipRevBits0`. On the live A4000, that field is `31`: it includes the
documented internal-only `GFXF_AA_MLISA` bit. An idempotent request for the
already-enabled public mask returns `15`, while the complete field remains
unchanged at `31`.

Tier 3 preserves the operating-system result and documents the distinction.
Applications should compare the return value only with the public
`SETCHIPREV_*` masks. Normal Workbench applications must not call this
startup-only function to reconfigure the graphics database; the live test
uses only the already-enabled public mask on a disposable A4000 run.

## diskfont.library NewScaledDiskFont result type

NDK 3.9's SFD and `clib/diskfont_protos.h` declare `NewScaledDiskFont` as
returning `struct DiskFont *`, but the public headers never define that
structure. The function's own autodoc instead specifies `struct
DiskFontHeader *` and documents cleanup through its `dfh_TF` and `dfh_Segment`
members. Live A4000 verification confirms that the returned object has the
published `DiskFontHeader` layout and contains the requested scaled `TextFont`.

Tier 3 retains the SFD's opaque `*DiskFont` signature for exact inventory
accounting. Callers must cast a successful result to `*DiskFontHeader`, call
`StripFont` on `dfh_TF`, then pass `dfh_Segment` to `UnLoadSeg`. The live test
performs that full lifecycle and leak-checks it.

## datatypes.library GetDTTriggerMethods result type

NDK 3.9's SFD and clib prototype declare `GetDTTriggerMethods` as returning
`struct DTMethods *`, but no public header defines that plural structure. The
DataTypes class header defines the actual table element as singular
`struct DTMethod`, and the autodoc says the function returns a null-terminated
`DTMethod` list. Live A4000 verification confirms that layout: the first entry
contains a valid label pointer and nonzero method ID.

Tier 3 preserves the SFD's opaque plural return type for exact inventory
accounting and documents the required cast to `*DTMethod`. A corrected typed
return belongs in the safe wrapper rather than silently changing the raw ABI
surface.

## datatypes.library PrintDTObject null printer request Guru

The pinned `PrintDTObjectA` autodoc says the caller must not manipulate its
`printerIO` until printing completes, but it does not explicitly require an
opened request or describe null handling. Live AmigaOS 3.2 verification with a
null `dtp_PIO` was accepted asynchronously and then raised Guru Meditation
`0x81000005 0x04819b80 0x00000000 0x0000021c` instead of returning failure.

The raw functions therefore document the dangerous precondition. Their A4000
tests create a full `printerIO`, open `printer.device`, submit the job, wait for
`DTA_Busy` to clear, close the device, drain its message port, and delete the
request. A safe Tier 2 printing API must own that lifecycle so application code
cannot supply a null or unopened request.

## dos.library SetArgStr SFD return type

NDK 3.9's `Include/sfd/dos_lib.sfd`, `clib/dos_protos.h`, and inline prototype
declare `SetArgStr` as returning `BOOL`, but the autodoc specifies `STRPTR`.
Live AmigaOS 3.2 verification confirms that D0 returns the previous argument-
string pointer so it can be restored before process exit. Treating the result
as 16-bit `BOOL` truncates that pointer and makes the documented lifecycle
unsafe.

The raw binding generator therefore overrides this one defective SFD return
type with `STRPTR`. `amiga::raw::dos::SetArgStr` now returns `*u8`, and its live
A4000 test replaces, reads back, and restores the exact original pointer.

## dos.library Fault SFD return type

NDK 3.9's `Include/sfd/dos_lib.sfd` and clib prototype declare `Fault` as
returning `BOOL`, preserving an error in older documentation. The function's
autodoc explicitly corrects that declaration: D0 has always returned the
`LONG` number of characters written to the caller's buffer.

The raw binding generator therefore overrides the defective return type with
`LONG`. `amiga::raw::dos::Fault` returns `i32`, and its A4000 test verifies the
returned length and caller-supplied prefix.

AmigaOS 3.2's dos.library 47.4 also leaves `IoErr` unchanged, despite the
autodoc saying that `Fault` sets it to the supplied code. The raw call preserves
that operating-system behavior; its live test pins the discrepancy so callers
do not accidentally rely on the missing side effect.

## dos.library FindArg parameter names

NDK 3.9's clib and inline prototypes call FindArg's D1 parameter `keyword` and
D2 parameter `arg_template`. The corrected autodoc and live AmigaOS 3.2
behavior agree that the actual order is `template` in D1, then `keyword` in D2.
Because both parameters are string pointers, ordinary signature verification
cannot detect the reversal.

The generator renames the raw parameters to `template, keyword` without
changing their registers. The A4000 test verifies exact keywords, explicit
`KEYWORD=ALIAS` abbreviations, and missing-keyword rejection.

## dos.library GetDeviceProc exhaustion ownership

The NDK autodoc explains how to pass a prior `DevProc` back to
`GetDeviceProc` to iterate a multi-directory assign, but does not explicitly
say that the exhaustion path frees that prior structure before returning
NULL. Calling `FreeDeviceProc` on the retained pointer then double-frees DOS
memory; live A4000 testing caught the resulting `0x01000009` Guru Meditation.

The raw `dp` parameter is therefore `consuming`. A non-null result returns
ownership of the advanced structure; a null result leaves nothing to free.
The live test traverses both assign entries through exhaustion and relies on
the suite memory check to verify the balanced lifecycle.

The same AmigaOS 3.2 implementation also leaves `IoErr` at the caller's
`ERROR_OBJECT_NOT_FOUND` value when iteration is exhausted instead of setting
the autodoc's `ERROR_NO_MORE_ENTRIES`. The live test records this discrepancy;
portable code must treat the null result as authoritative.

## dos.library resident-segment use-count semantics

The pinned NDK 3.9 autodocs describe `AddSegment`'s third argument as an
initial use count normally set to zero, describe an unused normal resident as
`seg_UC == 0`, and say `RemSegment` only accepts that value. Live AmigaOS 3.2
dos.library 47.4 behaves differently: passing zero creates a permanent system
entry with `seg_UC == -1`; passing one creates a removable idle normal entry
with `seg_UC == 1`; `FindSegment` preserves one; and `RemSegment` accepts and
unloads it. The raw declarations retain the ABI, while their documentation and
A4000 tests record the values applications must actually use.

## dos.library AbortPkt is a no-op

The pinned NDK documents that `AbortPkt` has done nothing since V37. Live
AmigaOS 3.2 verification sends a valid asynchronous packet to a deliberately
delayed child handler, calls `AbortPkt` before the handler receives it, and
confirms that the handler still processes and replies with both exact result
values. Callers must always wait for the normal packet reply before reusing or
freeing it; `AbortPkt` provides no cancellation guarantee on the target OS.

## dos.library AddBuffers retains the legacy direct count

The pinned NDK says V37-and-earlier filesystems may return their current buffer
count directly instead of returning `DOSTRUE` and placing the count in `IoErr`.
Live AmigaOS 3.2 verification shows that the DOS3 FastFileSystem still uses the
legacy direct-count convention. Tests and higher tiers must accept `-1` plus
`IoErr` or a positive direct count, while treating zero as failure.

## dos.library Format retains mounted-volume state

Live AmigaOS 3.2 formatting of a disposable DOS3 partition succeeds and the
resulting filesystem passes exact create/write/read/delete verification.
However, the test framework's repeated leak confirmation shows another format
retaining 49,776 bytes of AmigaOS-owned mounted-volume state. That state is not
returned while the partition remains attached, so `Format` has behavior, size,
and timing evidence but intentionally does not receive leak-verification credit.
This remains an explicit runtime-coverage blocker rather than a waived leak.

## utility.library 47.3 FreeNamedObject register defect

The `FreeNamedObject` implementation in utility.library 47.3 from Kickstart
47.115 subtracts its private 16-byte header from the public object pointer in
A0, then calls Exec `FreeVec` at LVO -690. The Exec ABI requires the allocation
pointer in A1, so the calculated pointer is ignored. Depending on stale caller
state, this leaks the complete named-object allocation or frees an unrelated
address and can corrupt memory or trigger a Guru Meditation.

Live A4000 testing with both VBCC C and Novus measured the same persistent
56-byte loss for an object with eight bytes of user storage. Releasing the
initial use and closing utility.library did not return the allocation. Live ROM
disassembly showed the defective sequence at `$00FD82EA`:

```asm
lea     -16(a0),a0
jsr     -690(a6)       ; FreeVec expects a1
```

Tier 3 retains and documents the raw NDK call. Application code should use
`amiga::sys::utility::free_named_object`, which detects utility.library 47.3
and calls `FreeVec` with the affected implementation's actual allocation base.
Other utility versions continue through the operating-system function.

## AmigaOS 3.2 removed Exec AVL trees

The pinned NDK 3.9 surface contains ten V45 `AVL_*` functions, but AmigaOS 3.2
deliberately removed their implementation. Its Exec release notes state that
every attempt to call one raises recoverable alert `AN_AVLNotImpl`
(`0x01000011`) and returns zero. Live A4000 execution confirmed that exact
alert for `AVL_AddNode`; this is not a Novus ABI or memory-corruption failure.

Tier 3 retains the raw declarations because they are part of the pinned NDK
and may exist on other classic systems or be restored by a patch. Applications
targeting AmigaOS 3.2 must not call them. The ten functions cannot receive
successful A4000 behavior, leak, or speed evidence on the project's current
3.2 runtime, and remain an explicit runtime-coverage blocker rather than being
counted as passing tests.

## FS-UAE A4000 battery-clock writes are not observable

On the current A4000/040 FS-UAE configuration, `ReadBattClock` returns a valid,
monotonic host-synchronized Amiga timestamp. `WriteBattClock` called with a
value exactly 86,400 seconds earlier returns normally, but the next read still
returns the original value. `ResetBattClock` likewise returns normally without
changing the next read to the documented 01-Jan-1978 epoch. The test restores
the saved value after each mutation attempt and triggers no alert or Guru.

This is recorded as an emulator/configuration limitation rather than an
AmigaOS hardware claim. `ReadBattClock` can receive complete A4000 evidence;
`WriteBattClock` and `ResetBattClock` remain explicit runtime blockers until a
machine with writable battery-clock emulation or real hardware verifies their
documented side effects.

## datebrowser.library century leap years

Live A4000 verification shows that the installed classic ReAction
`JulianLeapYear` implementation applies only the divisible-by-four rule: it
reports 2100 as a leap year. `JulianMonthDays(2, 2100)` consequently returns
29 rather than the Gregorian value 28. The same behavior was observed for
1900, so this is not a one-date conversion error.

Tier 3 preserves the operating-system behavior. Applications needing a
Gregorian calendar must apply the century rule above the raw binding: century
years are leap years only when divisible by 400.

## speedbar.library getters require 32-bit output storage

The pinned NDK describes `SBNA_Spacing` and several other SpeedBar node tags as
`WORD` values, but `GetSpeedButtonNodeAttrsA` writes a full 32-bit result to
every destination. Passing a `WORD *` therefore returns the apparent value zero
on big-endian 68k and overwrites the following two bytes. A canonical VBCC C
probe reproduced that corruption; changing only the destination to `ULONG`
returned the exact requested spacing values `4`, `9`, and `11` after allocation
and both setter forms. Novus produced the same results with `u32` storage.

Tier 3 preserves the operating-system ABI and explicitly documents the required
destination width. Callers must use writable 32-bit storage for every output
tag, even where the logical attribute is documented as `WORD` or `BOOL`.

## chooser.library node boolean attributes are ignored

The pinned Chooser autodoc documents `CNA_Disabled` and `CNA_ReadOnly` as
settable and readable private-node attributes. On the installed classic
Chooser class, nodes allocated or updated with either attribute set to true
still return zero from `GetChooserNodeAttrsA`. A direct VBCC C probe and the
Novus A4000 suite produce the same result; text and user-data attributes round
trip correctly in the same calls.

Tier 3 preserves this behavior. Applications must not rely on these two node
flags on the tested class version until an updated Chooser implementation is
available; the raw tests pin the defect so a future working implementation is
an intentional compatibility change rather than a silent one.

## clicktab.library node text retrieval is empty

The pinned ClickTab autodoc says `GetClickTabNodeAttrsA` returns private node
attributes through caller-supplied destination pointers. On the installed
classic class, `TNA_Number` round-trips correctly through a 32-bit destination,
but `TNA_Text` returns a non-null empty string instead of the label supplied to
`AllocClickTabNodeA` or `SetClickTabNodeAttrsA`. A direct VBCC C probe and the
Novus A4000 tests produce the same result.

Tier 3 preserves and tests this behavior. Applications must not use the raw
text getter to recover a ClickTab label on the tested class version. The test
still verifies every allocation, mutation, numeric retrieval, and free side
effect rather than treating the broken text result as successful retrieval.

## FS-UAE A4000 GetKey misses injected non-qualifier keys

With the MCP input path holding the Amiga A raw key (`0x20`),
`lowlevel.library/QueryKeys` reports that exact key pressed, but a same-moment
`GetKey` call returns low word `0xFFFF`, its documented no-key sentinel. The
same mismatch was observed when holding left Shift: `QueryKeys` reported the
qualifier pressed while `GetKey` omitted its qualifier bit.

This is recorded as a limitation of the tested A4000/040 FS-UAE input path,
not a claim about physical keyboards. The raw test pins both sides of the
discrepancy so it cannot be mistaken for successful `GetKey` behavior. Code
that needs reliable key snapshots in this environment must use `QueryKeys`.

## expansion.library AllocBoardMem hides its result

The pinned SFD and `clib/expansion_protos.h` declare `AllocBoardMem` as
returning `VOID`, and the function is omitted from the expansion autodoc. Live
disassembly of expansion.library 47.4 on the A4000 shows that it decodes the
low three `slotSpec` bits through a size/alignment table and tail-calls
`AllocExpansionMem`, leaving the allocated start slot in D0. Its paired
`FreeBoardMem` requires that exact start slot, so the public prototype provides
no safe way to use the pair.

Tier 3 preserves the pinned prototype. New code must use
`AllocExpansionMem`/`FreeExpansionMem`, which exposes the start slot. The live
tests prove the obsolete pair's side effects without inventing a return value:
`AllocBoardMem(1)` makes the deterministic first one-slot position unavailable,
and `FreeBoardMem` makes that exact position first-fit again.

## expansion.library MakeDosNode omits its destructor contract

The pinned autodoc says `MakeDosNode` allocates a `DeviceNode`,
`FileSysStartupMsg`, environment vector, and two BSTRs, but never says how to
release an unattached or removed graph. Live disassembly of expansion.library
47.4 proves that it uses `AllocEntry` to create five separate allocations and
then frees only the temporary `MemList` header. The exec-device BSTR count is
also incremented to include its trailing NUL, unlike the DOS-name BSTR count.

`FreeDosEntry`, `FreeVec`, and a single combined `FreeMem` each produced live
Exec free-list corruption and Guru `0x81000005`. Tier 3 now documents the five
exact `FreeMem` sizes. The A4000 test constructs, validates, frees, and
immediately reconstructs the full graph, then exercises reversible
`AddDosNode` and live-DOS `AddBootNode` ownership transfers without a leak.

## locale.library IsPrint classifies linefeed as printable

On the current AmigaOS 3.2 A4000/040 configuration, `IsPrint` reports true for
ASCII letters and space as expected, but also reports true for linefeed
(`0x0A`). It reports false for NUL and DEL. A canonical VBCC C probe returns
the same values, so this is not a Novus ABI or stub defect.

Tier 3 preserves and tests the installed operating-system behavior. Code that
must reject control characters cannot rely on raw `IsPrint` alone and should
also reject `IsCntrl` results.

## commodities.library InputXpression mask comments are inverted

The pinned `libraries/commodities.h` header says set bits in `ix_CodeMask` and
`ix_QualMask` are ignored. The tested AmigaOS 3.2 implementation does the
opposite: set mask bits select bits that must compare equal, while clear mask
bits are ignored. Exact positive and negative `MatchIX` tests and real
Commodities filter routing on the A4000 confirm this behavior. The equivalent
AROS implementation uses the same `(actual ^ expected) & mask` comparison.

Tier 3 preserves the installed operating-system behavior. The raw API
documentation corrects the header comment rather than teaching callers the
inverted rule.

## icon.library BumpRevision capitalization differs from its autodoc

The pinned autodoc examples say `BumpRevision` produces `copy_of_foo` and
`copy_2_of_foo`. The tested AmigaOS 3.2 implementation produces
`Copy_of_foo` and `Copy_2_of_foo` with an uppercase `C`. The A4000 test checks
both first-copy and numbered-copy behavior exactly.

Tier 3 preserves the installed operating-system behavior. Code comparing
generated names should not assume the lowercase spelling shown by the
autodoc.

## workbench.library WBInfo has conflicting return documentation

The pinned `wb.doc` autodoc describes `WBInfo` as returning an `ULONG` result
in D0, but the authoritative V44.5 `wb_lib.sfd` and generated
`clib/wb_protos.h` both declare `VOID WBInfo(BPTR, STRPTR, struct Screen *)`.
The raw Novus binding follows the SFD/header ABI and therefore returns no value.

The A4000 test validates the observable contract instead: a valid SYS: lock and
Prefs object open the real Information requester, a short-lived Exec task sends
Escape down/up through `input.device`, the requester returns, and every task,
device, lock, and message resource is released.

## MUI PatchASL breaks native ASL abort and leaks requester memory

The tested A4000 starts third-party `MUI:PatchASL` 21.19 from User-Startup.
With that patch active, both legacy `RequestFile` and modern `AslRequest`
retain exactly 1,576 AmigaOS bytes on every open/cancel lifecycle, including
after five warmed retries. `AbortAslRequest` called repeatedly from a separate
Exec task also fails to close the patched requester.

This is not a native `asl.library` defect. Sending Ctrl-C to the per-boot
PatchASL process removes the patch without changing the saved system. Against
native `asl.library` 47.11, all ten ASL entry points pass on the A4000:
requesters cancel correctly, `AbortAslRequest` closes the live modal requester,
`ActivateAslRequest` restores activation, every per-test allocation returns,
and a warm whole-process confirmation ends 24 bytes above its baseline. The
A4000 harness therefore verifies PatchASL has exited before awarding native ASL
runtime or leak evidence.
