#ifndef	WORKBENCH_ICON_H
#define	WORKBENCH_ICON_H
#define	ICONNAME	"icon.library"
struct	WBObject	*GetWBObject(),	*AllocWBObject();
struct	DiskObject	*GetDiskObject();
LONG	PutWBObject(),	PutIcon(),	GetIcon(),	MatchToolValue();
VOID	FreeFreeList(),	FreeWBObject(),	AddFreeList();
char	*FindToolType();
#endif
