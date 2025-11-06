
*------ Rstartup.asm  v 34.9   Copyright 1988 Commodore-Amiga, Inc.
*------
*------ Conditional assembly flags
*------ ASTART:   1=Standard Globals Defined    0=Reentrant Only
*------ WINDOW:   1=AppWindow for WB startup    0=No AppWindow code
*------ XNIL:     1=Remove Rstartup NIL: init   0=Default Nil: WB Output
*------ DEBUG:    1=Set up old statics for Wack 0=No extra statics

* Flags for  [A]start  AWstart  Rstart  RWstart  RXstart
* ASTART         1        1       0        0        0
* WINDOW         0        1       0        1        0
* XNIL           0        0       0        0        1
* DEBUG          0        0       0        0        0

ASTART    SET   1
WINDOW    SET   0
XNIL      SET   0
DEBUG     SET   0

;------   Flag WB output initialization
WBOUT     SET   (ASTART!WINDOW!(1-XNIL))

************************************************************************
*
*   Rstartup.asm --- Reentrant C Program Startup/Exit (CLI and WB)
*                    by C. Scheppner, based on Amiga startup.asm
*                    v34.9  05/25/88
*
*   Copyright (c) 1988 Commodore-Amiga, Inc.
*
*   Title to this software and all copies thereof remain vested in the
*   authors indicated in the above copyright notice.  The object version
*   of this code may be used in software for Commodore Amiga computers.
*   All other rights are reserved.
*
*   NO REPRESENTATIONS OR WARRANTIES ARE MADE WITH RESPECT TO THE
*   ACCURACY, RELIABILITY, PERFORMANCE OR OPERATION OF THIS SOFTWARE,
*   AND ALL SUCH USE IS AT YOUR OWN RISK.  NEITHER COMMODORE NOR THE
*   AUTHORS ASSUME ANY RESPONSIBILITY OR LIABILITY WHATSOEVER WITH
*   RESPECT TO YOUR USE OF THIS SOFTWARE.
*
*
*   RSTARTUP.ASM
*
*      This startup dynamically allocates a structure which includes
*   the argv buffers.  If you use this startup, your code must return
*   to this startup when it exits.  Use exit(n) or final curly brace
*   (rts) to return here.  Do not use AmigaDOS Exit() function.
*   Due to this dynamic allocation and some code consolidation, this
*   startup can make executables several hundred bytes smaller.
*
*       Because a static initialSP variable can not be used, this
*   code depends on the fact that AmigaDOS places the address of
*   the top of our stack in SP and proc->pr_ReturnAddr right before
*   JSR'ing to us.  This code uses pr_ReturnAddr when restoring SP.
*
*       Most versions of Rstartup will initialize a Workbench process's
*   input and output streams (and stdio globals if present) to NIL:
*   if no other form of Workbench output (like WINDOW) is provided.
*   This should help prevent crashes if a user puts an icon on a CLI
*   program, and will also protect against careless stdio debugging
*   or error messages left in a Workbench program.  The code for
*   initializing Workbench IO streams only be removed by assembling
*   Rstartup with ASTART and WINDOW set to 0, and XNIL set to 1.
*
*
*   Some Rstartups which can be conditionally assembled:
*
*      1. Standard Astartup for non-reentrant code
*            Astartup.obj     ASTART=1    WINDOW=0    XNIL=0
*      2. Reentrant startup (no unshareable globals)
*            Rstartup.obj     ASTART=0    WINDOW=0    XNIL=0
*      3. Smaller reentrant-only startup (no NIL: WB init code)
*            RXstartup.obj    ASTART=0    WINDOW=0    XNIL=1
*      4. Standard AWstartup (WB output window) for non-reentrant code
*            AWstartup.obj    ASTART=1    WINDOW=1    XNIL=0
*      5. Reentrant AWstartup (no unshareable globals)
*            RWstartup.obj    ASTART=0    WINDOW=1    XNIL=0
*
*
*   Explanation of conditional assembly flags:
*
*      ASTART (ASTART SET 1) startups will set up and XDEF the
*   global variables _stdin, _stdout, _stderr, _errno and  _WBenchMsg.
*   These startups can be used as smaller replacements for startups
*   like (A)startup.obj and TWstartup.obj.  Startups with ASTART
*   would generally be used for non-reentrant programs, although the
*   startup code itself is still reentrant if the globals are not
*   referenced.
*      Reentrant (ASTART SET 0) startups will NOT set up or
*   XDEF the stdio and WBenchMsg globals.  This not only makes the
*   startup slightly smaller, but also lets you know if your code
*   is referencing these non-reentrant globals (you will get an
*   unresolved external reference when you link).
*
*      WINDOW (WINDOW SET 1) startups use an XREF'd CON: string
*   named AppWindow, defined in your application, to open a stdio
*   console window when your application is started from Workbench.
*   For non-reentrant programs, this window can be used for normal
*   stdio (printf, getchar, etc).  For reentrant programs the window
*   is Input() and Output().  WINDOW is useful when adding Workbench
*   capability to a stdio application, and also for debugging other
*   Workbench applications.  To insure that applications requiring
*   a window startup are linked with a window startup, the label
*   _NeedWStartup can be externed and referenced in the application
*   so that a linker error will occur if linked with a standard
*   startup.
*
*       example:   /* Optional safety reference to NeedWStartup */
*                    extern UBYTE  NeedWStartup;
*                    UBYTE  *HaveWStartup = &NeedWStartup;
*                  /* Required window specification */
*                    char AppWindow[] = "CON:30/30/200/150/MyProgram";
*                    ( OR  char AppWindow[] = "\0";  for no window )
*
*
*      XNIL (XNIL SET 1) allows the creation of a smaller Rstartup
*   by removing the code that initializes a Workbench process's
*   output streams to NIL:.  This flag can only remove the code
*   if it is not required for ASTART or WINDOW.
*
*      DEBUG (DEBUG SET 1) will cause the old startup.asm statics
*   initialSP, dosCmdLen and dosCmdBuf to be defined and initialized
*   by the startup code, for use as debugging symbols when using Wack.
*
*
*   RULES FOR REENTRANT CODE
*
*      - Make no direct or indirect (printf, etc) references to the
*        globals _stdin, _stdout, _stderr, _errno, or _WBenchMsg.
*
*      - For stdio use either special versions of printf and getchar
*        that use Input() and Output() rather than _stdin and _stdout,
*        or use fprintf and fgetc with Input() and Output() file handles.
*
*      - Workbench applications must get the pointer to the WBenchMsg
*        from argv rather than from a global extern WBenchMsg.
*
*      - Use no global or static variables within your code.  Instead,
*        put all former globals in a dynamically allocated structure, and
*        pass around a pointer to that structure.  The only acceptable
*        globals are constants (message strings, etc) and global copies
*        of Library Bases to resolve Amiga.lib references.  Your code
*        must return all OpenLibrary's into non-global variables,
*        copy the result to the global library base only if successful,
*        and use the non-globals when deciding whether to Close any
*        opened libraries.  
*
*
*   By Carolyn Scheppner
*   Based on the following source
************************************************************************
*
* Source Control:
* --------------
*
* $Header: startup.asm,v 1.1 86/08/25 12:50:07 root Exp $
*
* $Locker: root $
*
* $Log:   startup.asm,v $
* Revision 1.1  86/08/25  12:50:07  root
* Initial revision
*
* Revision 33.6  86/06/12  10:34:36  neil
* changed so any version of dos is OK
* 
* Revision 33.5  86/06/11  19:03:47  neil
* fixed CloseLibrary if open of dos failed
* 
* Revision 33.4  86/06/09  16:48:59  neil
* now processes escapes also
* 
* Revision 33.3  86/06/09  16:18:12  neil
* another checkpoint -- quoted strings now work
* 
* Revision 1.1  85/11/23  13:49:01  neil
* Initial revision
* 
*
************************************************************************


******* Included Files *************************************************

   INCLUDE "exec/types.i"
   INCLUDE "exec/alerts.i"
   INCLUDE "exec/memory.i"
   INCLUDE "libraries/dos.i"
   INCLUDE "libraries/dosextens.i"
   INCLUDE "workbench/startup.i"


******* Macros *********************************************************

xlib   macro
   xref   _LVO\1
   endm

callsys   macro
   CALLLIB   _LVO\1
   endm

******* Imported *******************************************************

   xref   _AbsExecBase
   xref   _Input
   xref   _Output
   xref   _main         ; C code entry point

   IFGT  WINDOW
   xref   _AppWindow    ; CON: spec in application for WB stdio window
   xdef   _NeedWStartup ; May be externed and referenced in application
   ENDC  WINDOW

   xlib   Alert
   xlib   AllocMem
   xlib   FindTask
   xlib   Forbid
   xlib   FreeMem
   xlib   GetMsg
   xlib   OpenLibrary
   xlib   CloseLibrary
   xlib   ReplyMsg
   xlib   Wait
   xlib   WaitPort

   xlib   CurrentDir
   xlib   Open
   xlib   Close

******* Exported *******************************************************

*----- These globals are set up for standard startup code only
   IFGT  ASTART
   xdef   _stdin
   xdef   _stdout
   xdef   _stderr
   xdef   _errno
   xdef   _WBenchMsg
   ENDC  ASTART

*----- These globals available to normal and reentrant code

   xdef   _SysBase
   xdef   _DOSBase
   xdef   _exit         ; standard C exit function


***** Startup Variables structure **********************************

  STRUCTURE   SVAR,0
   LONG     sv_DOSBase
   LONG     sv_dosCmdLen
   LONG     sv_dosCmdBuf
   LONG     sv_WBenchMsg
   LONG     sv_WbOutput
   LONG     sv_Reserved1
   LONG     sv_Reserved2
   STRUCT   sv_argvArray,32*4
   STRUCT   sv_argvBuffer,256
  LABEL    SV_SIZEOF

************************************************************************
*
*   Standard Program Entry Point
*
************************************************************************
*
* Entered with  d0=dosCmdLen  a0=dosCmdBuf
*
* calls  main (argc, argv)
*        int  argc;
*        char *argv[]; 
*
* On Workbench startup, argc=0, argv=WBenchMsg
************************************************************************

startup:

   IFGT DEBUG
      move.l   SP,initialSP
      move.l   d0,dosCmdLen
      move.l   a0,dosCmdBuf
   ENDC DEBUG

      movem.l  d0/a0,-(sp)

      ;------ get Exec's library base pointer:
      movea.l  _AbsExecBase,a6
      move.l   a6,_SysBase

      ;------ alloc the argument structure
      move.l   #SV_SIZEOF,d0
      move.l   #(MEMF_PUBLIC!MEMF_CLEAR),d1
      callsys  AllocMem
      move.l   d0,a1             ;allocation to a1
      movem.l  (sp)+,d0/a0       ;d0/a0 and stack to initial state

      cmpa.l   #0,a1             ;did we get memory for variables ?
      bne.s    gotmem            ;yes
      moveq.l  #RETURN_FAIL,d0   ;no
      ;------ NOTE:  Exiting here if can't get arg memory
      ;------ Normal exits dependent on ptr to arg memory on stack
      rts

gotmem:
      move.l   a1,a5               ;Keep a5 as ptr to SVAR structure
      move.l   a5,-(sp)            ;Put a5 on stack next to Return addr
      move.l   d0,sv_dosCmdLen(a5) ;Save dos command buf len
      move.l   a0,sv_dosCmdBuf(a5) ;     dos command buf ptr

      ;------ get the address of our task  a6 = ExecBase
      suba.l   a1,a1               ;clear a1
      callsys  FindTask
      move.l   d0,a4               ;keep task address in a4

;-----------------------------------------------------------------------
;  Open the DOS library - a6 = AbsExecBase, a5 = svar
;  If successful, set up Global _DOSBase
;  Else Alert and exit

openDOS
      lea     DOSName(pc),A1     ;dos.library
      moveq.l #0,d0              ;any version
      callsys OpenLibrary
      move.l  d0,sv_DOSBase(a5)
      bne.s   gotDOS             ;Branch if successful

      ;----- Else do recoverable alert and exit
noDOS:
      ALERT   (AG_OpenLib!AO_DOSLib)
      moveq.l #100,d0
      bra     exit2

gotDOS:
      move.l  d0,_DOSBase        ;Set the global

;-----------------------------------------------------------------------
;  Branch to Workbench startup code if not a CLI process

      tst.l   pr_CLI(A4)
      beq     fromWorkbench


;=======================================================================
;====== CLI Startup Code ===============================================
;=======================================================================

*** Note    a4=Task    a5=SVAR structure    a6=AbsExecBase

fromCLI:
      ;------ find command name:
      suba.l   a0,a0
      move.l   pr_CLI(a4),d0
      lsl.l    #2,d0      ; bcpl pointer conversion
      move.l   cli_CommandName(a0,d0.l),d0
      lsl.l    #2,d0      ; bcpl pointer conversion

      ;------ create buffer and array:
      movem.l   a2/a3,-(sp)
      lea   sv_argvBuffer(a5),a2
      lea   sv_argvArray(a5),a3

      ;------ fetch command name:
      move.l   d0,a0
      moveq.l  #0,d0
      move.b   (a0)+,d0   ; size of command name
      clr.b    0(a0,d0.l)   ; terminate the string
      move.l   a0,(a3)+

      ;------   collect parameters:
      move.l   sv_dosCmdLen(a5),d0
      move.l   sv_dosCmdBuf(a5),a0

      ;------ null terminate the string, eat trailing garbage
      lea     0(a0,d0.l),a1
stripjunk:
      cmp.b   #' ',-(a1)

      ;-- jimm: 8/25/86: per kodiak's recommendation
      ; bls.s   stripjunk
      dbhi   D0,stripjunk

      clr.b   1(a1)

newarg:
      ;------ skip spaces
      move.b   (a0)+,d1
      beq.s   parmExit
      cmp.b   #' ',d1
      beq.s   newarg
      cmp.b   #9,d1         ; tab
      beq.s   newarg

      ;------ push address of the next parameter
      move.l  a2,(a3)+

      ;------ process quotes
      cmp.b   #'"',d1
      beq.s   doquote

      ;------ copy the parameter in
      move.b  d1,(a2)+

nextchar:
      ;------ null termination check
      move.b  (a0)+,d1
      beq.s   parmExit
      cmp.b   #' ',d1
      beq.s   endarg

      move.b  d1,(a2)+
      bra.s   nextchar

endarg:
      clr.b   (a2)+
      bra.s   newarg

doquote:
      ;------ process quoted strings
      move.b  (a0)+,d1
      beq.s   parmExit
      cmp.b   #'"',d1
      beq.s   endarg

      ;------ '*' is the BCPL escape character
      cmp.b   #'*',d1
      bne.s   addquotechar

      move.b  (a0)+,d1
      cmp.b   #'N',d1
      beq.s   1$
      cmp.b   #'n',d1
      bne.s   2$

1$:
      ;------ got a *N -- turn into a newline
      moveq   #10,d1
      bra.s   addquotechar

2$:
      cmp.b   #'E',d1
      beq.s   3$
      cmp.b   #'e',d1
      bne.s   addquotechar

3$:
      ;------ got a *E -- turn into a escape
      moveq   #27,d1

addquotechar:
      move.b  d1,(a2)+
      bra.s   doquote

parmExit:
      ;------ all done -- null terminate the baby
      clr.b   (a2)
      clr.l   (a3)

      ;------ compute the # of arguments (argc)
      move.l  a3,d0
      lea.l   sv_argvArray(a5),a3
      sub.l   a3,d0
      lsr.l   #2,d0

      movem.l  (sp)+,a2/a3
      pea      sv_argvArray(a5)
      move.l   d0,-(sp)


*
*  The above code relies on the end of line containing a control
*  character of any type, i.e. a valid character must not be the
*  last.  This fact is ensured by DOS.
*
      
   IFGT ASTART
      ;------ get standard input handle:
      jsr      _Input
      move.l   d0,_stdin

      ;------ get standard output handle:
      jsr      _Output
      move.l   d0,_stdout
      move.l   d0,_stderr
   ENDC ASTART

      bra      domain


;=======================================================================
;====== Workbench Startup Code =========================================
;=======================================================================

*** Note  a4=Task    a5=SVAR structure    a6=AbsExecBase

fromWorkbench:

;-----------------------------------------------------------------------
; This gets the startup message that workbench will send to us
; Must get this message before doing any DOS calls

waitmsg:
      lea   pr_MsgPort(A4),a0     * our process base
      callsys   WaitPort
      lea   pr_MsgPort(A4),a0     * our process base
      callsys GetMsg

      ;------ save the message so we can return it later
      move.l   d0,sv_WBenchMsg(a5)
   IFGT ASTART
      move.l   d0,_WBenchMsg
   ENDC ASTART

      ;------ push the message on the stack for wbmain (as argv)
      move.l   d0,-(SP)
      clr.l    -(SP)      indicate: run from Workbench (argc==0)

      ;------ put DOSBase in a6 for next few calls
      move.l   sv_DOSBase(a5),a6

      ;------ get the first argument
      move.l   d0,a2
      move.l   sm_ArgList(a2),d0
      beq.s    docons

      ;------ and set the current directory to the same directory
      move.l   d0,a0
      move.l   wa_Lock(a0),d1
      callsys  CurrentDir


docons: 
   IFGT WBOUT

      ;------ Open NIL: or AppWindow for WB Input()/Output() handle
      ;------ Also for possible initialization of stdio globals
      ;------ Stdio used to be initialized to -1
      ;------ a4 = task, a2 = wbenchmsg, DOSBase still in a6

      
   IFGT WINDOW
   ;------ Get AppWindow defined in application
      lea.l    _AppWindow,a0
      cmp.b    #0,(a0)
      bne.s    doOpen         ;Open if not null string
   ENDC WINDOW

      
   ;------ Open NIL: if no window provided
      lea.l    NilName(PC),a0

doOpen:
   ;------ Open up the file whose name is in a0
   ;------ Note - DOSBase still in a6
      move.l   a0,d1
      move.l   #MODE_OLDFILE,d2 
      callsys   Open 
      tst.l    d0
      beq.s    exit2

   ;------ save handle for closing on exit
      move.l   d0,sv_WbOutput(a5)

   ;------ d0 now contains handle for Workbench Output

   IFGT ASTART
   ;------ set the C input and output descriptors 
      move.l   d0,_stdin
      move.l   d0,_stdout
      move.l   d0,_stderr
   ENDC ASTART

   ;------ set the console task (so Open( "*", mode ) will work 
   ;       task pointer still in A4
      move.l   d0,pr_CIS(A4)
      move.l   d0,pr_COS(A4)
      lsl.l    #2,d0 
      move.l   d0,a0 
      move.l   fh_Type(a0),d0
      beq.s    noConTask
      move.l   d0,pr_ConsoleTask(A4)
noConTask:

   ENDC WBOUT

   ;------ Fall though to common WB/CLI code


****************************************************
**                                                **
** This code now used by both CLI and WB startup  **
**                                                **
****************************************************

domain:
      jsr      _main
      ;----- main didn't use exit(n) so provide success return code
      moveq.l  #RETURN_OK,d0
      bra.s    exit2

*******************************************************
**                                                   **
**  C Program exit() Function, return code on stack  **
**                                                   **
*******************************************************

_exit:
      move.l   4(SP),d0   ;exit(n) return code to d0

exit2:

      move.l   d0,d2      ;save return code in d2

      ;------ restore initial stack ptr
      ;------ FindTask
      movea.l  _AbsExecBase,a6
      suba.l   a1,a1
      callsys  FindTask
      ;------ get SP as it was prior to DOS's jsr to us
      move.l   d0,a4
      move.l   pr_ReturnAddr(a4),a5
      ;------ subtract 4 for return address, 4 for the a5 we pushed
      suba.l   #8,a5
      ;------ restore sp (stack now contains our a5, then address for rts)
      move.l   a5,sp
      ;------ pull our a5 (= ptr to svar)
      move.l   (sp)+,a5

      move.l   d2,-(SP)   ;put return code on stack


   IFGT WBOUT
   ;----- Close any WbOutput file before closing dos.library
      move.l  sv_WbOutput(a5),d1
      beq.s   noWbOut
      move.l  sv_DOSBase(a5),a6
      callsys Close
noWbOut:
      ;------ Restore a6 = ExecBase
      movea.l _AbsExecBase,a6
    ENDC WBOUT

      ;------ ExecBase still in a6
      ;------ Close DOS library if it was opened
      move.l   sv_DOSBase(a5),d0
      beq.s    1$
      move.l   d0,a1
      callsys  CloseLibrary
1$:

      ;------ if we ran from CLI, skip workbench reply
      tst.l   sv_WBenchMsg(a5)
      beq.s   deallocSV

      ;------ return the startup message to our parent
      ;------   we forbid so workbench can't UnLoadSeg() us
      ;------   before we are done:
      callsys  Forbid
      move.l   sv_WBenchMsg(a5),a1
      callsys  ReplyMsg

deallocSV:
      ;------ deallocate the SVAR structure
      move.l  a5,a1
      move.l  #SV_SIZEOF,d0
      callsys FreeMem     ;a6 still holds AbsExecBase

      ;------ this rts sends us back to DOS:

exitToDOS:
      move.l   (SP)+,d0
      rts


;----- PC relative data

DOSName      DOSNAME
NilName      dc.b   'NIL:',0

************************************************************************

   DATA

************************************************************************

_SysBase     dc.l   0
_DOSBase     dc.l   0

   IFGT ASTART
_WBenchMsg   dc.l   0
_stdin       dc.l   0
_stdout      dc.l   0
_stderr      dc.l   0
_errno       dc.l   0
   ENDC ASTART

   IFGT DEBUG
initialSP    dc.l   0
dosCmdLen    dc.l   0
dosCmdBuf    dc.l   0
   ENDC DEBUG

VerRev       dc.w   34,9
   IFGT ASTART
             dc.b   'A'
   ENDC ASTART
   IFEQ ASTART
             dc.b   'R'
   ENDC ASTART
   IFGT WINDOW
_NeedWStartup:
             dc.b   'W'
   ENDC WINDOW
   IFEQ WBOUT
             dc.b   'X'
   ENDC WBOUT
   IFGT DEBUG
             dc.b   'D'
   ENDC DEBUG

   END

