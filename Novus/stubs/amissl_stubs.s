; Generated from the official AmiSSL v5.27 SDK SFD files.
; Only vectors exposed by amiga::raw::amissl are retained; each function uses
; its own section so vlink can discard every unused vector.

	xref	_AmiSSLBase
	xref	_AmiSSLMasterBase

	section	_BIO_new_stub,code

; BIO * BIO_new(const BIO_METHOD * type)
	xdef	_BIO_new
_BIO_new:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-1728(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_free_stub,code

; int BIO_free(BIO * a)
	xdef	_BIO_free
_BIO_free:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-1740(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_read_stub,code

; int BIO_read(BIO * b, void * data, int dlen)
	xdef	_BIO_read
_BIO_read:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-1752(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_write_stub,code

; int BIO_write(BIO * b, const void * data, int dlen)
	xdef	_BIO_write
_BIO_write:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-1764(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_ctrl_stub,code

; long BIO_ctrl(BIO * bp, int cmd, long larg, void * parg)
	xdef	_BIO_ctrl
_BIO_ctrl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-1782(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_free_all_stub,code

; void BIO_free_all(BIO * a)
	xdef	_BIO_free_all
_BIO_free_all:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-1818(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_new_mem_buf_stub,code

; BIO * BIO_new_mem_buf(const void * buf, int len)
	xdef	_BIO_new_mem_buf
_BIO_new_mem_buf:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-1890(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_new_socket_stub,code

; BIO * BIO_new_socket(int sock, int close_flag)
	xdef	_BIO_new_socket
_BIO_new_socket:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_AmiSSLBase,a6
	jsr	-2058(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_get_error_stub,code

; unsigned long ERR_get_error()
	xdef	_ERR_get_error
_ERR_get_error:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-3960(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_peek_error_stub,code

; unsigned long ERR_peek_error()
	xdef	_ERR_peek_error
_ERR_peek_error:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-3978(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_clear_error_stub,code

; void ERR_clear_error()
	xdef	_ERR_clear_error
_ERR_clear_error:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-4014(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_error_string_stub,code

; char * ERR_error_string(unsigned long e, char * buf)
	xdef	_ERR_error_string
_ERR_error_string:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-4020(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_error_string_n_stub,code

; void ERR_error_string_n(unsigned long e, char * buf, size_t len)
	xdef	_ERR_error_string_n
_ERR_error_string_n:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	move.l	16(sp),d1
	movea.l	_AmiSSLBase,a6
	jsr	-4026(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_reason_error_string_stub,code

; const char * ERR_reason_error_string(unsigned long e)
	xdef	_ERR_reason_error_string
_ERR_reason_error_string:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-4044(a6)
	movem.l	(sp)+,a6
	rts

	section	_ERR_print_errors_stub,code

; void ERR_print_errors(BIO * bp)
	xdef	_ERR_print_errors
_ERR_print_errors:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-4056(a6)
	movem.l	(sp)+,a6
	rts

	section	_RAND_bytes_stub,code

; int RAND_bytes(unsigned char * buf, int num)
	xdef	_RAND_bytes
_RAND_bytes:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8058(a6)
	movem.l	(sp)+,a6
	rts

	section	_RAND_seed_stub,code

; void RAND_seed(const void * buf, int num)
	xdef	_RAND_seed
_RAND_seed:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8070(a6)
	movem.l	(sp)+,a6
	rts

	section	_RAND_status_stub,code

; int RAND_status()
	xdef	_RAND_status
_RAND_status:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-8100(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_set_cipher_list_stub,code

; int SSL_CTX_set_cipher_list(SSL_CTX * a, const char * str)
	xdef	_SSL_CTX_set_cipher_list
_SSL_CTX_set_cipher_list:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8202(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_new_stub,code

; SSL_CTX * SSL_CTX_new(const SSL_METHOD * meth)
	xdef	_SSL_CTX_new
_SSL_CTX_new:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8208(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_free_stub,code

; void SSL_CTX_free(SSL_CTX * a)
	xdef	_SSL_CTX_free
_SSL_CTX_free:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8214(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_current_cipher_stub,code

; const SSL_CIPHER * SSL_get_current_cipher(const SSL * s)
	xdef	_SSL_get_current_cipher
_SSL_get_current_cipher:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8262(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CIPHER_get_bits_stub,code

; int SSL_CIPHER_get_bits(const SSL_CIPHER * c, int * alg_bits)
	xdef	_SSL_CIPHER_get_bits
_SSL_CIPHER_get_bits:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8268(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CIPHER_get_version_stub,code

; const char * SSL_CIPHER_get_version(const SSL_CIPHER * c)
	xdef	_SSL_CIPHER_get_version
_SSL_CIPHER_get_version:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8274(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CIPHER_get_name_stub,code

; const char * SSL_CIPHER_get_name(const SSL_CIPHER * c)
	xdef	_SSL_CIPHER_get_name
_SSL_CIPHER_get_name:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8280(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_fd_stub,code

; int SSL_get_fd(const SSL * s)
	xdef	_SSL_get_fd
_SSL_get_fd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8316(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_pending_stub,code

; int SSL_pending(const SSL * s)
	xdef	_SSL_pending
_SSL_pending:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8352(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_set_fd_stub,code

; int SSL_set_fd(SSL * s, int fd)
	xdef	_SSL_set_fd
_SSL_set_fd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8358(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_set_rfd_stub,code

; int SSL_set_rfd(SSL * s, int fd)
	xdef	_SSL_set_rfd
_SSL_set_rfd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8364(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_set_wfd_stub,code

; int SSL_set_wfd(SSL * s, int fd)
	xdef	_SSL_set_wfd
_SSL_set_wfd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8370(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_use_PrivateKey_file_stub,code

; int SSL_CTX_use_PrivateKey_file(SSL_CTX * ctx, const char * file, int type)
	xdef	_SSL_CTX_use_PrivateKey_file
_SSL_CTX_use_PrivateKey_file:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8496(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_use_certificate_file_stub,code

; int SSL_CTX_use_certificate_file(SSL_CTX * ctx, const char * file, int type)
	xdef	_SSL_CTX_use_certificate_file
_SSL_CTX_use_certificate_file:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8502(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_use_certificate_chain_file_stub,code

; int SSL_CTX_use_certificate_chain_file(SSL_CTX * ctx, const char * file)
	xdef	_SSL_CTX_use_certificate_chain_file
_SSL_CTX_use_certificate_chain_file:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8508(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_SESSION_free_stub,code

; void SSL_SESSION_free(SSL_SESSION * ses)
	xdef	_SSL_SESSION_free
_SSL_SESSION_free:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8616(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_set_session_stub,code

; int SSL_set_session(SSL * to, SSL_SESSION * session)
	xdef	_SSL_set_session
_SSL_set_session:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8628(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get1_peer_certificate_stub,code

; X509 * SSL_get1_peer_certificate(const SSL * s)
	xdef	_SSL_get1_peer_certificate
_SSL_get1_peer_certificate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8670(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_set_verify_stub,code

; void SSL_CTX_set_verify(SSL_CTX * ctx, int mode, int (*callback)(int, X509_STORE_CTX *) callback)
	xdef	_SSL_CTX_set_verify
_SSL_CTX_set_verify:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8700(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_set_verify_depth_stub,code

; void SSL_CTX_set_verify_depth(SSL_CTX * ctx, int depth)
	xdef	_SSL_CTX_set_verify_depth
_SSL_CTX_set_verify_depth:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8706(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_check_private_key_stub,code

; int SSL_CTX_check_private_key(const SSL_CTX * ctx)
	xdef	_SSL_CTX_check_private_key
_SSL_CTX_check_private_key:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8766(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_new_stub,code

; SSL * SSL_new(SSL_CTX * ctx)
	xdef	_SSL_new
_SSL_new:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8784(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_free_stub,code

; void SSL_free(SSL * ssl)
	xdef	_SSL_free
_SSL_free:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8820(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_accept_stub,code

; int SSL_accept(SSL * ssl)
	xdef	_SSL_accept
_SSL_accept:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8826(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_connect_stub,code

; int SSL_connect(SSL * ssl)
	xdef	_SSL_connect
_SSL_connect:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8832(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_read_stub,code

; int SSL_read(SSL * ssl, void * buf, int num)
	xdef	_SSL_read
_SSL_read:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8838(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_peek_stub,code

; int SSL_peek(SSL * ssl, void * buf, int num)
	xdef	_SSL_peek
_SSL_peek:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8844(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_write_stub,code

; int SSL_write(SSL * ssl, const void * buf, int num)
	xdef	_SSL_write
_SSL_write:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8850(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_ctrl_stub,code

; long SSL_ctrl(SSL * ssl, int cmd, long larg, void * parg)
	xdef	_SSL_ctrl
_SSL_ctrl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8856(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_ctrl_stub,code

; long SSL_CTX_ctrl(SSL_CTX * ctx, int cmd, long larg, void * parg)
	xdef	_SSL_CTX_ctrl
_SSL_CTX_ctrl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-8868(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_error_stub,code

; int SSL_get_error(const SSL * s, int ret_code)
	xdef	_SSL_get_error
_SSL_get_error:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-8880(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_version_stub,code

; const char * SSL_get_version(const SSL * s)
	xdef	_SSL_get_version
_SSL_get_version:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8886(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_do_handshake_stub,code

; int SSL_do_handshake(SSL * s)
	xdef	_SSL_do_handshake
_SSL_do_handshake:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8976(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_shutdown_stub,code

; int SSL_shutdown(SSL * s)
	xdef	_SSL_shutdown
_SSL_shutdown:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-8994(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CIPHER_description_stub,code

; char * SSL_CIPHER_description(const SSL_CIPHER * a1, char * buf, int size)
	xdef	_SSL_CIPHER_description
_SSL_CIPHER_description:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-9096(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_set_shutdown_stub,code

; void SSL_set_shutdown(SSL * ssl, int mode)
	xdef	_SSL_set_shutdown
_SSL_set_shutdown:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-9150(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_shutdown_stub,code

; int SSL_get_shutdown(const SSL * ssl)
	xdef	_SSL_get_shutdown
_SSL_get_shutdown:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9156(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_version_stub,code

; int SSL_version(const SSL * ssl)
	xdef	_SSL_version
_SSL_version:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9162(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_set_default_verify_paths_stub,code

; int SSL_CTX_set_default_verify_paths(SSL_CTX * ctx)
	xdef	_SSL_CTX_set_default_verify_paths
_SSL_CTX_set_default_verify_paths:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9168(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_load_verify_locations_stub,code

; int SSL_CTX_load_verify_locations(SSL_CTX * ctx, const char * CAfile, const char * CApath)
	xdef	_SSL_CTX_load_verify_locations
_SSL_CTX_load_verify_locations:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmiSSLBase,a6
	jsr	-9174(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_SSL_get_session_stub,code

; SSL_SESSION * SSL_get_session(const SSL * ssl)
	xdef	_SSL_get_session
_SSL_get_session:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9180(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get1_session_stub,code

; SSL_SESSION * SSL_get1_session(SSL * ssl)
	xdef	_SSL_get1_session
_SSL_get1_session:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9186(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_verify_result_stub,code

; long SSL_get_verify_result(const SSL * ssl)
	xdef	_SSL_get_verify_result
_SSL_get_verify_result:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-9222(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_free_stub,code

; void X509_free(X509 * a)
	xdef	_X509_free
_X509_free:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-10620(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_NAME_oneline_stub,code

; char * X509_NAME_oneline(const X509_NAME * a, char * buf, int size)
	xdef	_X509_NAME_oneline
_X509_NAME_oneline:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-10980(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_get_issuer_name_stub,code

; X509_NAME * X509_get_issuer_name(const X509 * a)
	xdef	_X509_get_issuer_name
_X509_get_issuer_name:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-11046(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_get_subject_name_stub,code

; X509_NAME * X509_get_subject_name(const X509 * a)
	xdef	_X509_get_subject_name
_X509_get_subject_name:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-11058(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_check_issued_stub,code

; int X509_check_issued(X509 * issuer, X509 * subject)
	xdef	_X509_check_issued
_X509_check_issued:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-13368(a6)
	movem.l	(sp)+,a6
	rts

	section	_BIO_test_flags_stub,code

; int BIO_test_flags(const BIO * b, int flags)
	xdef	_BIO_test_flags
_BIO_test_flags:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-15570(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_get_servername_stub,code

; const char * SSL_get_servername(const SSL * s, const int type)
	xdef	_SSL_get_servername
_SSL_get_servername:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmiSSLBase,a6
	jsr	-15786(a6)
	movem.l	(sp)+,a6
	rts

	section	_OPENSSL_cleanup_stub,code

; void OPENSSL_cleanup()
	xdef	_OPENSSL_cleanup
_OPENSSL_cleanup:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-24906(a6)
	movem.l	(sp)+,a6
	rts

	section	_OPENSSL_init_crypto_stub,code

; int OPENSSL_init_crypto(uint64_t opts, const OPENSSL_INIT_SETTINGS * settings)
	xdef	_OPENSSL_init_crypto
_OPENSSL_init_crypto:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	16(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-24912(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_getm_notAfter_stub,code

; ASN1_TIME * X509_getm_notAfter(const X509 * x)
	xdef	_X509_getm_notAfter
_X509_getm_notAfter:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-26286(a6)
	movem.l	(sp)+,a6
	rts

	section	_X509_getm_notBefore_stub,code

; ASN1_TIME * X509_getm_notBefore(const X509 * x)
	xdef	_X509_getm_notBefore
_X509_getm_notBefore:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-26292(a6)
	movem.l	(sp)+,a6
	rts

	section	_OPENSSL_init_ssl_stub,code

; int OPENSSL_init_ssl(uint64_t opts, const OPENSSL_INIT_SETTINGS * settings)
	xdef	_OPENSSL_init_ssl
_OPENSSL_init_ssl:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	16(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-26568(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_clear_options_stub,code

; uint64_t SSL_CTX_clear_options(SSL_CTX * ctx, uint64_t op)
	xdef	_SSL_CTX_clear_options
_SSL_CTX_clear_options:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movem.l	12(sp),d0-d1
	movea.l	_AmiSSLBase,a6
	jsr	-26610(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_CTX_set_options_stub,code

; uint64_t SSL_CTX_set_options(SSL_CTX * ctx, uint64_t op)
	xdef	_SSL_CTX_set_options
_SSL_CTX_set_options:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movem.l	12(sp),d0-d1
	movea.l	_AmiSSLBase,a6
	jsr	-26682(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_session_reused_stub,code

; int SSL_session_reused(const SSL * s)
	xdef	_SSL_session_reused
_SSL_session_reused:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmiSSLBase,a6
	jsr	-26844(a6)
	movem.l	(sp)+,a6
	rts

	section	_TLS_client_method_stub,code

; const SSL_METHOD * TLS_client_method()
	xdef	_TLS_client_method
_TLS_client_method:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-26934(a6)
	movem.l	(sp)+,a6
	rts

	section	_TLS_method_stub,code

; const SSL_METHOD * TLS_method()
	xdef	_TLS_method
_TLS_method:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-26940(a6)
	movem.l	(sp)+,a6
	rts

	section	_TLS_server_method_stub,code

; const SSL_METHOD * TLS_server_method()
	xdef	_TLS_server_method
_TLS_server_method:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLBase,a6
	jsr	-26946(a6)
	movem.l	(sp)+,a6
	rts

	section	_SSL_read_ex_stub,code

; int SSL_read_ex(SSL * ssl, void * buf, size_t num, size_t * readbytes)
	xdef	_SSL_read_ex
_SSL_read_ex:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	move.l	20(sp),d0
	movea.l	24(sp),a2
	movea.l	_AmiSSLBase,a6
	jsr	-29484(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_SSL_write_ex_stub,code

; int SSL_write_ex(SSL * s, const void * buf, size_t num, size_t * written)
	xdef	_SSL_write_ex
_SSL_write_ex:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	move.l	20(sp),d0
	movea.l	24(sp),a2
	movea.l	_AmiSSLBase,a6
	jsr	-29496(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_SSL_CTX_set_ciphersuites_stub,code

; int SSL_CTX_set_ciphersuites(SSL_CTX * ctx, const char * str)
	xdef	_SSL_CTX_set_ciphersuites
_SSL_CTX_set_ciphersuites:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmiSSLBase,a6
	jsr	-29946(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitAmiSSLMaster_stub,code

; LONG InitAmiSSLMaster(LONG APIVersion, LONG UsesOpenSSLStructs)
	xdef	_InitAmiSSLMaster
_InitAmiSSLMaster:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_AmiSSLMasterBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmiSSL_stub,code

; struct Library * OpenAmiSSL()
	xdef	_OpenAmiSSL
_OpenAmiSSL:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLMasterBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseAmiSSL_stub,code

; void CloseAmiSSL()
	xdef	_CloseAmiSSL
_CloseAmiSSL:
	movem.l	a6,-(sp)
	movea.l	_AmiSSLMasterBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmiSSLTagList_stub,code

; LONG OpenAmiSSLTagList(LONG APIVersion, struct TagItem * tagList)
	xdef	_OpenAmiSSLTagList
_OpenAmiSSLTagList:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_AmiSSLMasterBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmiSSLTags_stub,code

; LONG OpenAmiSSLTags(LONG APIVersion, Tag tag, ... )
	xdef	_OpenAmiSSLTags
_OpenAmiSSLTags:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	lea	12(sp),a0
	movea.l	_AmiSSLMasterBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

