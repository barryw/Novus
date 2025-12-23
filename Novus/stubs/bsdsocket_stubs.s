; bsdsocket.library stubs for Novus
; Generated from AmiTCP/Roadshow bsdsocket_lib.fd
; Library: bsdsocket.library
; Base: _SocketBase
; Each function is in its own section for dead code elimination

	xref	_SocketBase

; ============================================================================
; Core Socket Functions
; ============================================================================

	section	_socket_stub,code

; int socket(int domain, int type, int protocol)
; Registers: d0=domain, d1=type, d2=protocol
; Returns: socket fd in d0
	xdef	_socket
_socket:
	move.l	4(sp),d0	; domain
	move.l	8(sp),d1	; type
	move.l	12(sp),d2	; protocol
	movea.l	_SocketBase,a6
	jsr	-30(a6)
	rts

	section	_bind_stub,code

; int bind(int sock, struct sockaddr *name, int namelen)
; Registers: d0=sock, a0=name, d1=namelen
	xdef	_bind
_bind:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; name
	move.l	12(sp),d1	; namelen
	movea.l	_SocketBase,a6
	jsr	-36(a6)
	rts

	section	_listen_stub,code

; int listen(int sock, int backlog)
; Registers: d0=sock, d1=backlog
	xdef	_listen
_listen:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; backlog
	movea.l	_SocketBase,a6
	jsr	-42(a6)
	rts

	section	_accept_stub,code

; int accept(int sock, struct sockaddr *addr, int *addrlen)
; Registers: d0=sock, a0=addr, a1=addrlen
	xdef	_accept
_accept:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; addr
	movea.l	12(sp),a1	; addrlen
	movea.l	_SocketBase,a6
	jsr	-48(a6)
	rts

	section	_connect_stub,code

; int connect(int sock, struct sockaddr *name, int namelen)
; Registers: d0=sock, a0=name, d1=namelen
	xdef	_connect
_connect:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; name
	move.l	12(sp),d1	; namelen
	movea.l	_SocketBase,a6
	jsr	-54(a6)
	rts

; ============================================================================
; Data Transfer Functions
; ============================================================================

	section	_sendto_stub,code

; int sendto(int sock, void *buf, int len, int flags, struct sockaddr *to, int tolen)
; Registers: d0=sock, a0=buf, d1=len, d2=flags, a1=to, d3=tolen
	xdef	_sendto
_sendto:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; buf
	move.l	12(sp),d1	; len
	move.l	16(sp),d2	; flags
	movea.l	20(sp),a1	; to
	move.l	24(sp),d3	; tolen
	movea.l	_SocketBase,a6
	jsr	-60(a6)
	rts

	section	_send_stub,code

; int send(int sock, void *buf, int len, int flags)
; Registers: d0=sock, a0=buf, d1=len, d2=flags
	xdef	_send
_send:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; buf
	move.l	12(sp),d1	; len
	move.l	16(sp),d2	; flags
	movea.l	_SocketBase,a6
	jsr	-66(a6)
	rts

	section	_recvfrom_stub,code

; int recvfrom(int sock, void *buf, int len, int flags, struct sockaddr *addr, int *addrlen)
; Registers: d0=sock, a0=buf, d1=len, d2=flags, a1=addr, a2=addrlen
	xdef	_recvfrom
_recvfrom:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; buf
	move.l	12(sp),d1	; len
	move.l	16(sp),d2	; flags
	movea.l	20(sp),a1	; addr
	movea.l	24(sp),a2	; addrlen
	movea.l	_SocketBase,a6
	jsr	-72(a6)
	rts

	section	_recv_stub,code

; int recv(int sock, void *buf, int len, int flags)
; Registers: d0=sock, a0=buf, d1=len, d2=flags
	xdef	_recv
_recv:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; buf
	move.l	12(sp),d1	; len
	move.l	16(sp),d2	; flags
	movea.l	_SocketBase,a6
	jsr	-78(a6)
	rts

	section	_sendmsg_stub,code

; int sendmsg(int sock, struct msghdr *msg, int flags)
; Registers: d0=sock, a0=msg, d1=flags
	xdef	_sendmsg
_sendmsg:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; msg
	move.l	12(sp),d1	; flags
	movea.l	_SocketBase,a6
	jsr	-270(a6)
	rts

	section	_recvmsg_stub,code

; int recvmsg(int sock, struct msghdr *msg, int flags)
; Registers: d0=sock, a0=msg, d1=flags
	xdef	_recvmsg
_recvmsg:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; msg
	move.l	12(sp),d1	; flags
	movea.l	_SocketBase,a6
	jsr	-276(a6)
	rts

; ============================================================================
; Socket Control Functions
; ============================================================================

	section	_shutdown_stub,code

; int shutdown(int sock, int how)
; Registers: d0=sock, d1=how
	xdef	_shutdown
_shutdown:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; how
	movea.l	_SocketBase,a6
	jsr	-84(a6)
	rts

	section	_setsockopt_stub,code

; int setsockopt(int sock, int level, int optname, void *optval, int optlen)
; Registers: d0=sock, d1=level, d2=optname, a0=optval, d3=optlen
	xdef	_setsockopt
_setsockopt:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; level
	move.l	12(sp),d2	; optname
	movea.l	16(sp),a0	; optval
	move.l	20(sp),d3	; optlen
	movea.l	_SocketBase,a6
	jsr	-90(a6)
	rts

	section	_getsockopt_stub,code

; int getsockopt(int sock, int level, int optname, void *optval, int *optlen)
; Registers: d0=sock, d1=level, d2=optname, a0=optval, a1=optlen
	xdef	_getsockopt
_getsockopt:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; level
	move.l	12(sp),d2	; optname
	movea.l	16(sp),a0	; optval
	movea.l	20(sp),a1	; optlen
	movea.l	_SocketBase,a6
	jsr	-96(a6)
	rts

	section	_getsockname_stub,code

; int getsockname(int sock, struct sockaddr *name, int *namelen)
; Registers: d0=sock, a0=name, a1=namelen
	xdef	_getsockname
_getsockname:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; name
	movea.l	12(sp),a1	; namelen
	movea.l	_SocketBase,a6
	jsr	-102(a6)
	rts

	section	_getpeername_stub,code

; int getpeername(int sock, struct sockaddr *name, int *namelen)
; Registers: d0=sock, a0=name, a1=namelen
	xdef	_getpeername
_getpeername:
	move.l	4(sp),d0	; sock
	movea.l	8(sp),a0	; name
	movea.l	12(sp),a1	; namelen
	movea.l	_SocketBase,a6
	jsr	-108(a6)
	rts

	section	_IoctlSocket_stub,code

; int IoctlSocket(int sock, unsigned long req, void *argp)
; Registers: d0=sock, d1=req, a0=argp
	xdef	_IoctlSocket
_IoctlSocket:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; req
	movea.l	12(sp),a0	; argp
	movea.l	_SocketBase,a6
	jsr	-114(a6)
	rts

	section	_CloseSocket_stub,code

; int CloseSocket(int sock)
; Registers: d0=sock
	xdef	_CloseSocket
_CloseSocket:
	move.l	4(sp),d0	; sock
	movea.l	_SocketBase,a6
	jsr	-120(a6)
	rts

	section	_WaitSelect_stub,code

; int WaitSelect(int nfds, fd_set *read_fds, fd_set *write_fds, fd_set *except_fds, struct timeval *timeout, ULONG *signals)
; Registers: d0=nfds, a0=read_fds, a1=write_fds, a2=except_fds, a3=timeout, d1=signals
	xdef	_WaitSelect
_WaitSelect:
	move.l	4(sp),d0	; nfds
	movea.l	8(sp),a0	; read_fds
	movea.l	12(sp),a1	; write_fds
	movea.l	16(sp),a2	; except_fds
	movea.l	20(sp),a3	; timeout
	move.l	24(sp),d1	; signals
	movea.l	_SocketBase,a6
	jsr	-126(a6)
	rts

	section	_SetSocketSignals_stub,code

; void SetSocketSignals(ULONG int_mask, ULONG io_mask, ULONG urgent_mask)
; Registers: d0=int_mask, d1=io_mask, d2=urgent_mask
	xdef	_SetSocketSignals
_SetSocketSignals:
	move.l	4(sp),d0	; int_mask
	move.l	8(sp),d1	; io_mask
	move.l	12(sp),d2	; urgent_mask
	movea.l	_SocketBase,a6
	jsr	-132(a6)
	rts

	section	_getdtablesize_stub,code

; int getdtablesize(void)
	xdef	_getdtablesize
_getdtablesize:
	movea.l	_SocketBase,a6
	jsr	-138(a6)
	rts

; ============================================================================
; Socket Sharing Functions
; ============================================================================

	section	_ObtainSocket_stub,code

; int ObtainSocket(long id, int domain, int type, int protocol)
; Registers: d0=id, d1=domain, d2=type, d3=protocol
	xdef	_ObtainSocket
_ObtainSocket:
	move.l	4(sp),d0	; id
	move.l	8(sp),d1	; domain
	move.l	12(sp),d2	; type
	move.l	16(sp),d3	; protocol
	movea.l	_SocketBase,a6
	jsr	-144(a6)
	rts

	section	_ReleaseSocket_stub,code

; int ReleaseSocket(int sock, long id)
; Registers: d0=sock, d1=id
	xdef	_ReleaseSocket
_ReleaseSocket:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; id
	movea.l	_SocketBase,a6
	jsr	-150(a6)
	rts

	section	_ReleaseCopyOfSocket_stub,code

; int ReleaseCopyOfSocket(int sock, long id)
; Registers: d0=sock, d1=id
	xdef	_ReleaseCopyOfSocket
_ReleaseCopyOfSocket:
	move.l	4(sp),d0	; sock
	move.l	8(sp),d1	; id
	movea.l	_SocketBase,a6
	jsr	-156(a6)
	rts

	section	_Dup2Socket_stub,code

; int Dup2Socket(int old_socket, int new_socket)
; Registers: d0=old_socket, d1=new_socket
	xdef	_Dup2Socket
_Dup2Socket:
	move.l	4(sp),d0	; old_socket
	move.l	8(sp),d1	; new_socket
	movea.l	_SocketBase,a6
	jsr	-264(a6)
	rts

; ============================================================================
; Error Handling Functions
; ============================================================================

	section	_Errno_stub,code

; int Errno(void)
	xdef	_Errno
_Errno:
	movea.l	_SocketBase,a6
	jsr	-162(a6)
	rts

	section	_SetErrnoPtr_stub,code

; void SetErrnoPtr(int *errno_ptr, int size)
; Registers: a0=errno_ptr, d0=size
	xdef	_SetErrnoPtr
_SetErrnoPtr:
	movea.l	4(sp),a0	; errno_ptr
	move.l	8(sp),d0	; size
	movea.l	_SocketBase,a6
	jsr	-168(a6)
	rts

; ============================================================================
; Address Conversion Functions
; ============================================================================

	section	_Inet_NtoA_stub,code

; char *Inet_NtoA(unsigned long ip)
; Registers: d0=ip
	xdef	_Inet_NtoA
_Inet_NtoA:
	move.l	4(sp),d0	; ip
	movea.l	_SocketBase,a6
	jsr	-174(a6)
	rts

	section	_inet_addr_stub,code

; unsigned long inet_addr(const char *cp)
; Registers: a0=cp (per FD file)
; NOTE: Also loading d0 in case UAE expects it there
	xdef	_inet_addr
_inet_addr:
	move.l	4(sp),d0	; cp (try d0 first)
	movea.l	4(sp),a0	; cp (also in a0 per spec)
	movea.l	_SocketBase,a6
	jsr	-180(a6)
	rts

	section	_Inet_LnaOf_stub,code

; unsigned long Inet_LnaOf(unsigned long in)
; Registers: d0=in
	xdef	_Inet_LnaOf
_Inet_LnaOf:
	move.l	4(sp),d0	; in
	movea.l	_SocketBase,a6
	jsr	-186(a6)
	rts

	section	_Inet_NetOf_stub,code

; unsigned long Inet_NetOf(unsigned long in)
; Registers: d0=in
	xdef	_Inet_NetOf
_Inet_NetOf:
	move.l	4(sp),d0	; in
	movea.l	_SocketBase,a6
	jsr	-192(a6)
	rts

	section	_Inet_MakeAddr_stub,code

; unsigned long Inet_MakeAddr(unsigned long net, unsigned long host)
; Registers: d0=net, d1=host
	xdef	_Inet_MakeAddr
_Inet_MakeAddr:
	move.l	4(sp),d0	; net
	move.l	8(sp),d1	; host
	movea.l	_SocketBase,a6
	jsr	-198(a6)
	rts

	section	_inet_network_stub,code

; unsigned long inet_network(const char *cp)
; Registers: a0=cp
	xdef	_inet_network
_inet_network:
	movea.l	4(sp),a0	; cp
	movea.l	_SocketBase,a6
	jsr	-204(a6)
	rts

	section	_inet_aton_stub,code

; int inet_aton(const char *cp, struct in_addr *addr)
; Registers: a0=cp, a1=addr
; Returns: 1 on success, 0 on failure
	xdef	_inet_aton
_inet_aton:
	movea.l	4(sp),a0	; cp
	movea.l	8(sp),a1	; addr
	movea.l	_SocketBase,a6
	jsr	-594(a6)
	rts

	section	_inet_ntop_stub,code

; const char *inet_ntop(int af, const void *src, char *dst, int size)
; Registers: d0=af, a0=src, a1=dst, d1=size
	xdef	_inet_ntop
_inet_ntop:
	move.l	4(sp),d0	; af
	movea.l	8(sp),a0	; src
	movea.l	12(sp),a1	; dst
	move.l	16(sp),d1	; size
	movea.l	_SocketBase,a6
	jsr	-600(a6)
	rts

	section	_inet_pton_stub,code

; int inet_pton(int af, const char *src, void *dst)
; Registers: d0=af, a0=src, a1=dst
	xdef	_inet_pton
_inet_pton:
	move.l	4(sp),d0	; af
	movea.l	8(sp),a0	; src
	movea.l	12(sp),a1	; dst
	movea.l	_SocketBase,a6
	jsr	-606(a6)
	rts

; ============================================================================
; Host/Network Database Functions
; ============================================================================

	section	_gethostbyname_stub,code

; struct hostent *gethostbyname(const char *name)
; Registers: a0=name
	xdef	_gethostbyname
_gethostbyname:
	movea.l	4(sp),a0	; name
	movea.l	_SocketBase,a6
	jsr	-210(a6)
	rts

	section	_gethostbyaddr_stub,code

; struct hostent *gethostbyaddr(const void *addr, int len, int type)
; Registers: a0=addr, d0=len, d1=type
	xdef	_gethostbyaddr
_gethostbyaddr:
	movea.l	4(sp),a0	; addr
	move.l	8(sp),d0	; len
	move.l	12(sp),d1	; type
	movea.l	_SocketBase,a6
	jsr	-216(a6)
	rts

	section	_getnetbyname_stub,code

; struct netent *getnetbyname(const char *name)
; Registers: a0=name
	xdef	_getnetbyname
_getnetbyname:
	movea.l	4(sp),a0	; name
	movea.l	_SocketBase,a6
	jsr	-222(a6)
	rts

	section	_getnetbyaddr_stub,code

; struct netent *getnetbyaddr(unsigned long net, int type)
; Registers: d0=net, d1=type
	xdef	_getnetbyaddr
_getnetbyaddr:
	move.l	4(sp),d0	; net
	move.l	8(sp),d1	; type
	movea.l	_SocketBase,a6
	jsr	-228(a6)
	rts

	section	_getservbyname_stub,code

; struct servent *getservbyname(const char *name, const char *proto)
; Registers: a0=name, a1=proto
	xdef	_getservbyname
_getservbyname:
	movea.l	4(sp),a0	; name
	movea.l	8(sp),a1	; proto
	movea.l	_SocketBase,a6
	jsr	-234(a6)
	rts

	section	_getservbyport_stub,code

; struct servent *getservbyport(int port, const char *proto)
; Registers: d0=port, a0=proto
	xdef	_getservbyport
_getservbyport:
	move.l	4(sp),d0	; port
	movea.l	8(sp),a0	; proto
	movea.l	_SocketBase,a6
	jsr	-240(a6)
	rts

	section	_getprotobyname_stub,code

; struct protoent *getprotobyname(const char *name)
; Registers: a0=name
	xdef	_getprotobyname
_getprotobyname:
	movea.l	4(sp),a0	; name
	movea.l	_SocketBase,a6
	jsr	-246(a6)
	rts

	section	_getprotobynumber_stub,code

; struct protoent *getprotobynumber(int proto)
; Registers: d0=proto
	xdef	_getprotobynumber
_getprotobynumber:
	move.l	4(sp),d0	; proto
	movea.l	_SocketBase,a6
	jsr	-252(a6)
	rts

	section	_gethostname_stub,code

; int gethostname(char *name, int namelen)
; Registers: a0=name, d0=namelen
	xdef	_gethostname
_gethostname:
	movea.l	4(sp),a0	; name
	move.l	8(sp),d0	; namelen
	movea.l	_SocketBase,a6
	jsr	-282(a6)
	rts

	section	_gethostid_stub,code

; unsigned long gethostid(void)
	xdef	_gethostid
_gethostid:
	movea.l	_SocketBase,a6
	jsr	-288(a6)
	rts

; ============================================================================
; Miscellaneous Functions
; ============================================================================

	section	_SocketBaseTagList_stub,code

; LONG SocketBaseTagList(struct TagItem *tags)
; Registers: a0=tags
	xdef	_SocketBaseTagList
_SocketBaseTagList:
	movea.l	4(sp),a0	; tags
	movea.l	_SocketBase,a6
	jsr	-294(a6)
	rts

	section	_GetSocketEvents_stub,code

; LONG GetSocketEvents(ULONG *event_ptr)
; Registers: a0=event_ptr
	xdef	_GetSocketEvents
_GetSocketEvents:
	movea.l	4(sp),a0	; event_ptr
	movea.l	_SocketBase,a6
	jsr	-300(a6)
	rts

	section	_vsyslog_stub,code

; void vsyslog(int pri, const char *msg, va_list args)
; Registers: d0=pri, a0=msg, a1=args
	xdef	_vsyslog
_vsyslog:
	move.l	4(sp),d0	; pri
	movea.l	8(sp),a0	; msg
	movea.l	12(sp),a1	; args
	movea.l	_SocketBase,a6
	jsr	-258(a6)
	rts

	end
