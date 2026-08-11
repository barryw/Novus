/* Equivalent AmigaOS 3.1 C for the homepage Novus screen example. */

#include <exec/libraries.h>
#include <graphics/gfxbase.h>
#include <intuition/intuition.h>
#include <proto/dos.h>
#include <proto/exec.h>
#include <proto/graphics.h>
#include <proto/intuition.h>

struct GfxBase *GfxBase;
struct IntuitionBase *IntuitionBase;

int main(void)
{
    struct Screen *screen = NULL;
    ULONG monitor_id, height;
    int result = 1;

    GfxBase = (struct GfxBase *)OpenLibrary("graphics.library", 33);
    IntuitionBase = (struct IntuitionBase *)OpenLibrary("intuition.library", 36);
    if (!GfxBase || !IntuitionBase)
        goto cleanup;

    monitor_id = (GfxBase->DisplayFlags & PAL) ? PAL_MONITOR_ID : NTSC_MONITOR_ID;
    height = (GfxBase->DisplayFlags & PAL) ? 256 : 200;
    screen = OpenScreenTags(NULL,
        SA_DisplayID, monitor_id,
        SA_Width, 320,
        SA_Height, height,
        SA_Depth, 5,
        SA_Title, (ULONG)"Demo Screen",
        SA_Type, CUSTOMSCREEN,
        TAG_END);
    if (!screen)
        goto cleanup;

    SetAPen(&screen->RastPort, 2);
    RectFill(&screen->RastPort, 10, 20, 100, 80);
    Delay(150);
    result = 0;

cleanup:
    if (screen) CloseScreen(screen);
    if (IntuitionBase) CloseLibrary((struct Library *)IntuitionBase);
    if (GfxBase) CloseLibrary((struct Library *)GfxBase);
    return result;
}
