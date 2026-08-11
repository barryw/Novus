/* Equivalent classic Amiga C using Intuition and GadTools directly. */

#include <exec/types.h>
#include <intuition/intuition.h>
#include <libraries/gadtools.h>
#include <proto/exec.h>
#include <proto/intuition.h>
#include <proto/gadtools.h>

struct IntuitionBase *IntuitionBase;
struct Library *GadToolsBase;

enum { QUIT_BUTTON = 1 };

static struct NewMenu menu_definition[] = {
    { NM_TITLE, (STRPTR)"File", NULL, 0, 0, NULL },
    { NM_ITEM,  (STRPTR)"Quit", (STRPTR)"Q", 0, 0, NULL },
    { NM_END, NULL, NULL, 0, 0, NULL }
};

int main(void)
{
    struct Screen *screen = NULL;
    struct Window *window = NULL;
    struct Gadget *gadgets = NULL, *previous = NULL;
    struct Menu *menus = NULL;
    APTR visual_info = NULL;
    struct NewGadget ng;
    ULONG signals, klass;
    UWORD code;
    BOOL running = TRUE;
    int result = 20;

    IntuitionBase = (struct IntuitionBase *)OpenLibrary("intuition.library", 36);
    GadToolsBase = OpenLibrary("gadtools.library", 36);
    if (!IntuitionBase || !GadToolsBase)
        goto cleanup;

    screen = LockPubScreen(NULL);
    if (!screen)
        goto cleanup;

    visual_info = GetVisualInfo(screen, TAG_END);
    previous = CreateContext(&gadgets);
    if (!visual_info || !previous)
        goto cleanup;

    ng.ng_TextAttr = screen->Font;
    ng.ng_VisualInfo = visual_info;
    ng.ng_UserData = NULL;
    ng.ng_Flags = PLACETEXT_LEFT;

    ng.ng_LeftEdge = 24; ng.ng_TopEdge = 34;
    ng.ng_Width = 170; ng.ng_Height = 14;
    ng.ng_GadgetText = (UBYTE *)"Enable feature";
    ng.ng_GadgetID = 2;
    previous = CreateGadget(CHECKBOX_KIND, previous, &ng,
                            GTCB_Checked, TRUE, TAG_END);
    if (!previous)
        goto cleanup;

    ng.ng_LeftEdge = 24; ng.ng_TopEdge = 58;
    ng.ng_Width = 170; ng.ng_Height = 18;
    ng.ng_GadgetText = (UBYTE *)"Value";
    ng.ng_GadgetID = 3;
    previous = CreateGadget(INTEGER_KIND, previous, &ng,
                            GTIN_Number, 42,
                            GTIN_MaxChars, 3,
                            TAG_END);
    if (!previous)
        goto cleanup;

    ng.ng_LeftEdge = 124; ng.ng_TopEdge = 88;
    ng.ng_Width = 70; ng.ng_Height = 18;
    ng.ng_GadgetText = (UBYTE *)"Quit";
    ng.ng_GadgetID = QUIT_BUTTON;
    ng.ng_Flags = PLACETEXT_IN;
    previous = CreateGadget(BUTTON_KIND, previous, &ng, TAG_END);
    if (!previous)
        goto cleanup;

    menus = CreateMenus(menu_definition, TAG_END);
    if (!menus || !LayoutMenus(menus, visual_info, TAG_END))
        goto cleanup;

    window = OpenWindowTags(NULL,
        WA_Title, (ULONG)"Idiomatic GUI",
        WA_CustomScreen, (ULONG)screen,
        WA_Left, 40,
        WA_Top, 30,
        WA_Width, 230,
        WA_Height, 135,
        WA_Gadgets, (ULONG)gadgets,
        WA_IDCMP, IDCMP_CLOSEWINDOW | IDCMP_MENUPICK |
                  CHECKBOXIDCMP | INTEGERIDCMP | BUTTONIDCMP,
        WA_Flags, WFLG_CLOSEGADGET | WFLG_DRAGBAR |
                  WFLG_DEPTHGADGET | WFLG_ACTIVATE,
        TAG_END);
    if (!window || !SetMenuStrip(window, menus))
        goto cleanup;

    GT_RefreshWindow(window, NULL);
    signals = 1UL << window->UserPort->mp_SigBit;

    while (running) {
        struct IntuiMessage *message;
        Wait(signals);
        while ((message = GT_GetIMsg(window->UserPort)) != NULL) {
            klass = message->Class;
            code = message->Code;
            if (klass == IDCMP_GADGETUP)
                code = ((struct Gadget *)message->IAddress)->GadgetID;
            GT_ReplyIMsg(message);

            if (klass == IDCMP_CLOSEWINDOW ||
                (klass == IDCMP_GADGETUP && code == QUIT_BUTTON) ||
                (klass == IDCMP_MENUPICK &&
                 MENUNUM(code) == 0 && ITEMNUM(code) == 0))
                running = FALSE;
        }
    }

    result = 0;

cleanup:
    if (window) {
        if (menus) ClearMenuStrip(window);
        CloseWindow(window);
    }
    if (menus) FreeMenus(menus);
    if (gadgets) FreeGadgets(gadgets);
    if (visual_info) FreeVisualInfo(visual_info);
    if (screen) UnlockPubScreen(NULL, screen);
    if (GadToolsBase) CloseLibrary(GadToolsBase);
    if (IntuitionBase) CloseLibrary((struct Library *)IntuitionBase);
    return result;
}
