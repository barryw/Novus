/*
 * wrun - launch a GUI program, verify its window appeared, close it, hand
 *        focus back to the shell.
 *
 * Testing a windowed Amiga program from a driven shell has a bootstrapping
 * problem: the moment the program opens a window it takes focus, and every
 * subsequent command typed at the shell goes nowhere. A tool that restores
 * focus is useless if you cannot type it. So the whole cycle has to run from
 * a single invocation issued while the shell still has focus.
 *
 *   wrun <program> [seconds]     launch, wait, report windows, close, restore
 *   wrun -l                      list open windows and exit
 *
 * Exit codes: 0 a window appeared and closed, 5 appeared but did not close,
 * 10 no window appeared, 20 setup failure.
 */

#include <exec/types.h>
#include <exec/memory.h>
#include <intuition/intuition.h>
#include <intuition/intuitionbase.h>
#include <proto/exec.h>
#include <proto/intuition.h>
#include <proto/dos.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAX_WINDOWS 64
#define REPLY_POLLS 50           /* x 5 ticks = 5s before we give up waiting */

struct IntuitionBase *IntuitionBase = NULL;

/* Snapshot of the window list. Pointers only - we never dereference these
 * after unlocking, they are compared for identity. Titles are copied while
 * the lock is held so we can print them safely afterwards. */
struct Snapshot {
    struct Window *win[MAX_WINDOWS];
    char title[MAX_WINDOWS][64];
    int count;
};

static void snapshot(struct Snapshot *s)
{
    struct Screen *sc;
    struct Window *w;
    ULONG lock;

    s->count = 0;
    lock = LockIBase(0);
    for (sc = IntuitionBase->FirstScreen; sc; sc = sc->NextScreen) {
        for (w = sc->FirstWindow; w; w = w->NextWindow) {
            if (s->count >= MAX_WINDOWS) break;
            s->win[s->count] = w;
            if (w->Title)
                strncpy(s->title[s->count], (char *)w->Title, 63);
            else
                strcpy(s->title[s->count], "(untitled)");
            s->title[s->count][63] = '\0';
            s->count++;
        }
    }
    UnlockIBase(lock);
}

static int seen_in(struct Snapshot *s, struct Window *w)
{
    int i;
    for (i = 0; i < s->count; i++)
        if (s->win[i] == w) return 1;
    return 0;
}

/* Ask a window to close the same way a user clicking its close gadget would.
 * We own the message, so we must not exit until the app replies - a reply to
 * freed memory takes the machine down. If it never replies we deliberately
 * leak the port and message rather than risk that.
 * ponytail: polls instead of Wait() so a wedged app cannot hang the harness. */
static int ask_close(struct Window *w)
{
    struct MsgPort *port;
    struct IntuiMessage *msg;
    struct Message *reply;
    int i;

    port = CreateMsgPort();
    if (!port) return 0;

    msg = (struct IntuiMessage *)AllocMem(sizeof(struct IntuiMessage),
                                          MEMF_PUBLIC | MEMF_CLEAR);
    if (!msg) { DeleteMsgPort(port); return 0; }

    msg->ExecMessage.mn_Node.ln_Type = NT_MESSAGE;
    msg->ExecMessage.mn_Length = sizeof(struct IntuiMessage);
    msg->ExecMessage.mn_ReplyPort = port;
    msg->Class = IDCMP_CLOSEWINDOW;
    msg->IDCMPWindow = w;

    PutMsg(w->UserPort, (struct Message *)msg);

    for (i = 0; i < REPLY_POLLS; i++) {
        reply = GetMsg(port);
        if (reply) {
            FreeMem(reply, sizeof(struct IntuiMessage));
            DeleteMsgPort(port);
            return 1;
        }
        Delay(5);
    }

    printf("  warning: no reply from '%s', leaking its port to stay safe\n",
           w->Title ? (char *)w->Title : "(untitled)");
    return 0;
}

static void list_windows(void)
{
    struct Snapshot s;
    int i;
    snapshot(&s);
    printf("%d window(s):\n", s.count);
    for (i = 0; i < s.count; i++)
        printf("  %s\n", s.title[i]);
}

/* Static, not automatic. Two Snapshots plus the rest come to about 9.5K of
 * locals, and an AmigaDOS CLI hands a program a 4K stack - so main() overran it
 * and died with a privilege violation. It survived for a while on luck, which is
 * the worst way for it to fail: a tool for catching crashes, crashing. */
static struct Snapshot before, after;
static struct Window *fresh[MAX_WINDOWS];
static char cmd[512];

int main(int argc, char **argv)
{
    struct Window *shell;
    int seconds = 5, i, nfresh = 0, closed = 0, rc;

    IntuitionBase = (struct IntuitionBase *)OpenLibrary("intuition.library", 37);
    if (!IntuitionBase) {
        printf("wrun: cannot open intuition.library v37+\n");
        return 20;
    }

    if (argc < 2) {
        printf("usage: wrun <program> [seconds]   |   wrun -l\n");
        CloseLibrary((struct Library *)IntuitionBase);
        return 20;
    }

    if (strcmp(argv[1], "-l") == 0) {
        list_windows();
        CloseLibrary((struct Library *)IntuitionBase);
        return 0;
    }

    if (argc > 2) seconds = atoi(argv[2]);
    if (seconds < 1) seconds = 1;

    /* Whoever is active right now is who we hand focus back to. */
    shell = IntuitionBase->ActiveWindow;

    snapshot(&before);

    sprintf(cmd, "run >NIL: %s", argv[1]);
    if (Execute(cmd, 0, 0) == 0) {
        printf("wrun: failed to launch %s\n", argv[1]);
        CloseLibrary((struct Library *)IntuitionBase);
        return 20;
    }

    Delay(seconds * 50);
    snapshot(&after);

    for (i = 0; i < after.count; i++) {
        if (!seen_in(&before, after.win[i])) {
            printf("  window: %s\n", after.title[i]);
            fresh[nfresh++] = after.win[i];
        }
    }

    if (nfresh == 0) {
        printf("FAIL %s - no window appeared within %ds\n", argv[1], seconds);
        rc = 10;
    } else {
        for (i = 0; i < nfresh; i++)
            if (ask_close(fresh[i])) closed++;

        if (closed == nfresh) {
            printf("PASS %s - %d window(s) opened and closed\n", argv[1], nfresh);
            rc = 0;
        } else {
            printf("WARN %s - %d of %d window(s) closed\n", argv[1], closed, nfresh);
            rc = 5;
        }
    }

    /* Give the shell focus back so the next command actually lands. */
    if (shell) ActivateWindow(shell);

    CloseLibrary((struct Library *)IntuitionBase);
    return rc;
}
