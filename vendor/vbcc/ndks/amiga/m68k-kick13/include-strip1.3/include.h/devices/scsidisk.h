#ifndef	DEVICES_SCSIDISK_H
#define	DEVICES_SCSIDISK_H
#define	HD_SCSICMD	28
struct	SCSICmd	{
UWORD	*scsi_Data;
ULONG	scsi_Length;
ULONG	scsi_Actual;
UBYTE	*scsi_Command;
UWORD	scsi_CmdLength;
UWORD	scsi_CmdActual;
UBYTE	scsi_Flags;
UBYTE	scsi_Status;
};
#define	SCSIF_WRITE	0
#define	SCSIF_READ	1
#define	HFERR_SelfUnit	40
#define	HFERR_DMA	41
#define	HFERR_Phase	42
#define	HFERR_Parity	43
#define	HFERR_SelTimeout	44
#define	HFERR_BadStatus	45
#define	HFERR_NoBoard	50
#endif
