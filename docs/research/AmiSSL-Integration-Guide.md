# AmiSSL Integration Guide for Novus

This document provides comprehensive research on AmiSSL for integrating TLS/SSL support into Novus applications on AmigaOS.

---

## 1. What is AmiSSL?

**AmiSSL** is the AmigaOS/MorphOS/AROS port of OpenSSL. It wraps the full functionality of OpenSSL into a full-fledged Amiga shared library, enabling Amiga applications to use the complete OpenSSL API through standard Amiga shared library interfaces.

### OpenSSL Version

- **Current Version**: AmiSSL v5.x (as of 2025)
- **Based on**: OpenSSL 3.6 (full API/ABI compatibility)
- **Latest Release**: OpenSSL 3.5.4 backend (September 30, 2025) with CVE fixes
- **Previous Versions**:
  - AmiSSL v4.x: Based on OpenSSL 1.1.x
  - AmiSSL v1-v3: Legacy versions

The library maintains 100% API/ABI compatibility with the OpenSSL version it's based on, meaning standard OpenSSL documentation applies directly to AmiSSL.

### Distribution and Installation

**Download Sources**:
- [GitHub Releases](https://github.com/jens-maus/amissl/releases)
- [Aminet](https://aminet.net/package/util/libs/AmiSSL-v5-OS3)
- AmiUpdate (for AmigaOS 4.x automatic installation)

**Installation**:
1. Download the appropriate archive (OS3/m68k or OS4/PPC)
2. Extract to temporary directory
3. Run the Install script
4. Components install to standard locations:
   - `LIBS:amisslmaster.library` - Proxy library
   - `AmiSSL:Libs/AmiSSL/` - Versioned SSL libraries
   - `AmiSSL:Certs/` - Root CA certificates
   - `AmiSSL:UserCerts/` - User certificates
   - `AmiSSL:Private/` - Private keys

**Multiple Versions**: AmiSSL supports parallel installation of different versions. Applications linked against older AmiSSL versions (v1-v4) continue working alongside v5 installations.

### Supported Systems

**Platform Requirements**:
- **exec.library** v38+ minimum
- **AmigaOS 3.0+/68020+** (m68k)
- **AmigaOS 4.0+** (PPC)
- **MorphOS**
- **AROS**

**Important**: For 68k systems, requires 68020 or higher CPU. Will NOT work on 68000/68010.

---

## 2. AmiSSL Architecture

### Component Structure

AmiSSL consists of four major components:

1. **amisslmaster.library**: Main proxy library that applications open
2. **Shared Libraries**: Actual OpenSSL implementations in `AmiSSL:Libs/AmiSSL/`
3. **Root CA Certificates**: Mozilla-synchronized certificates in `AmiSSL:Certs/`
4. **Developer SDK**: Headers, autodocs, examples, link libraries

### Library Architecture

```
Application
    ↓
amisslmaster.library (proxy - always latest version)
    ↓
amisslv5_68020.library or amisslv4_68020.library (actual SSL implementation)
```

**Important**: Applications NEVER open version-specific libraries directly (except v1). Always use `amisslmaster.library` as the entry point.

### Backward Compatibility

- Applications compiled for AmiSSL v5 automatically use latest v5 updates
- Old applications targeting v1-v4 continue working without recompilation
- SDK changes between major versions require recompilation to use newer features

---

## 3. Using AmiSSL with bsdsocket.library

### Initialization Sequence

#### Manual Initialization (Full Control)

```c
// 1. Open bsdsocket.library (must be opened in same thread as socket operations)
struct Library *SocketBase = OpenLibrary("bsdsocket.library", 4);
if (!SocketBase) {
    // Error handling
}

// 2. Open amisslmaster.library
struct Library *AmiSSLMasterBase = OpenLibrary("amisslmaster.library", AMISSLMASTER_MIN_VERSION);
if (!AmiSSLMasterBase) {
    CloseLibrary(SocketBase);
    // Error handling
}

// 3. Initialize AmiSSL Master (AmigaOS 3.x)
if (!InitAmiSSLMaster(AMISSL_CURRENT_VERSION, TRUE)) {
    CloseLibrary(AmiSSLMasterBase);
    CloseLibrary(SocketBase);
    // Error handling
}

// 4. Open AmiSSL with tags
struct Library *AmiSSLBase = NULL;
if (OpenAmiSSLTags(
    AMISSL_UsesOpenSSLStructs, TRUE,
    AMISSL_GetAmiSSLBase, &AmiSSLBase,
    TAG_DONE) != 0) {
    // Error handling
}

// 5. Initialize SSL library
OPENSSL_init_ssl(OPENSSL_INIT_SSL_DEFAULT |
                 OPENSSL_INIT_LOAD_SSL_STRINGS |
                 OPENSSL_INIT_LOAD_CRYPTO_STRINGS, NULL);

// 6. Seed entropy (important!)
struct timeval tv;
GetSysTime(&tv);
RAND_seed(&tv, sizeof(tv));
```

#### Auto-Initialization (Simplified)

When using `libamisslauto.a`, much of the initialization is automated:

```c
#define USE_AUTOINIT
#include <proto/amissl.h>

// Just create SSL context - initialization handled automatically
SSL_CTX *ctx = SSL_CTX_new(TLS_client_method());
if (!ctx) {
    // Error handling
}
```

### SSL Context Setup

```c
// Create SSL context
SSL_CTX *ctx = SSL_CTX_new(TLS_client_method());
if (!ctx) {
    ERR_print_errors_fp(stderr);
    // Error handling
}

// Set certificate verification paths
SSL_CTX_set_default_verify_paths(ctx);

// Or specify custom certificate directory
SSL_CTX_load_verify_locations(ctx, NULL, "AmiSSL:Certs");

// Enable peer certificate verification
SSL_CTX_set_verify(ctx, SSL_VERIFY_PEER, verify_callback);

// Set minimum TLS version (optional)
SSL_CTX_set_min_proto_version(ctx, TLS1_2_VERSION);
```

### Wrapping a Socket with SSL/TLS

```c
// 1. Create standard bsdsocket socket
int sock = socket(AF_INET, SOCK_STREAM, 0);
if (sock < 0) {
    // Error handling
}

// 2. Connect socket (standard TCP connection)
struct sockaddr_in addr;
addr.sin_family = AF_INET;
addr.sin_port = htons(443);
addr.sin_addr.s_addr = inet_addr("93.184.216.34"); // Example IP
if (connect(sock, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
    CloseSocket(sock);
    // Error handling
}

// 3. Create SSL object from context
SSL *ssl = SSL_new(ctx);
if (!ssl) {
    CloseSocket(sock);
    // Error handling
}

// 4. Associate socket with SSL object
if (!SSL_set_fd(ssl, sock)) {
    SSL_free(ssl);
    CloseSocket(sock);
    // Error handling
}

// 5. Set SNI hostname (required for modern HTTPS)
SSL_set_tlsext_host_name(ssl, "example.com");

// 6. Perform SSL handshake
if (SSL_connect(ssl) != 1) {
    ERR_print_errors_fp(stderr);
    SSL_free(ssl);
    CloseSocket(sock);
    // Error handling
}

// 7. Verify peer certificate (recommended)
X509 *cert = SSL_get_peer_certificate(ssl);
if (cert) {
    long verify_result = SSL_get_verify_result(ssl);
    if (verify_result != X509_V_OK) {
        Printf("Certificate verification failed: %ld\n", verify_result);
        // Decide whether to proceed or abort
    }
    X509_free(cert);
} else {
    Printf("No peer certificate!\n");
    // Error handling
}
```

### Key SSL/TLS Functions

#### SSL_new
```c
SSL *SSL_new(SSL_CTX *ctx);
```
Creates a new SSL structure from an SSL context. Returns NULL on failure.

#### SSL_connect
```c
int SSL_connect(SSL *ssl);
```
Initiates TLS/SSL handshake with server. Returns:
- `1` on success
- `0` on controlled shutdown
- `<0` on fatal error (check `SSL_get_error()`)

#### SSL_read
```c
int SSL_read(SSL *ssl, void *buf, int num);
```
Reads up to `num` bytes into `buf`. Returns:
- `>0` number of bytes read
- `0` clean shutdown
- `<0` error (check `SSL_get_error()`)

#### SSL_write
```c
int SSL_write(SSL *ssl, const void *buf, int num);
```
Writes `num` bytes from `buf`. Returns:
- `>0` number of bytes written
- `≤0` error (check `SSL_get_error()`)

#### SSL_shutdown
```c
int SSL_shutdown(SSL *ssl);
```
Shuts down TLS/SSL connection. Returns:
- `0` shutdown not yet finished (call again)
- `1` shutdown successfully completed
- `<0` shutdown failed (check `SSL_get_error()`)

#### SSL_get_error
```c
int SSL_get_error(SSL *ssl, int ret);
```
Returns error code for the most recent SSL operation. Pass the return value from the failed operation.

Error codes:
- `SSL_ERROR_NONE` - No error
- `SSL_ERROR_ZERO_RETURN` - Connection closed
- `SSL_ERROR_WANT_READ` - Need to read more data (non-blocking)
- `SSL_ERROR_WANT_WRITE` - Need to write more data (non-blocking)
- `SSL_ERROR_SYSCALL` - System I/O error
- `SSL_ERROR_SSL` - SSL library error

### Cleanup

```c
// Proper cleanup sequence:

// 1. Send close_notify and shutdown SSL
SSL_shutdown(ssl);

// 2. Free SSL object
SSL_free(ssl);

// 3. Close socket (use CloseSocket, not close!)
CloseSocket(sock);

// 4. Free SSL context (once at program end)
SSL_CTX_free(ctx);

// 5. Cleanup AmiSSL
CleanupAmiSSLA();  // or CleanupAmiSSLTags() for OS4

// 6. Close libraries (reverse order of opening)
CloseAmiSSL();
CloseLibrary(AmiSSLMasterBase);
CloseLibrary(SocketBase);
```

**Important Notes**:
- Use `CloseSocket()` and `IoctlSocket()` from bsdsocket.library, NOT standard POSIX `close()` and `ioctl()`
- bsdsocket.library must be opened in the same thread where socket operations occur
- Socket descriptors CANNOT be shared between threads

---

## 4. Key Data Structures

### OpenSSL 3.x Structure Changes

**Critical Change**: OpenSSL 3.x made most structures opaque. You CANNOT directly access structure members - use accessor functions instead.

### Main Structures

#### SSL_CTX (SSL Context)
```c
typedef struct ssl_ctx_st SSL_CTX;
```
Global context structure created once per program. Holds default values for SSL connections.

**Creation**: `SSL_CTX *SSL_CTX_new(const SSL_METHOD *method)`

**Common Methods**:
- `TLS_client_method()` - TLS client (any version)
- `TLS_server_method()` - TLS server (any version)
- `TLS_method()` - TLS client or server

**Key Functions**:
- `SSL_CTX_set_verify()` - Set certificate verification mode
- `SSL_CTX_load_verify_locations()` - Load CA certificates
- `SSL_CTX_set_default_verify_paths()` - Use system default CA paths
- `SSL_CTX_set_min_proto_version()` - Set minimum TLS version
- `SSL_CTX_free()` - Free context

#### SSL (Connection)
```c
typedef struct ssl_st SSL;
```
Main SSL/TLS structure created per connection.

**Creation**: `SSL *SSL_new(SSL_CTX *ctx)`

**Key Functions**:
- `SSL_set_fd()` / `SSL_set_rfd()` / `SSL_set_wfd()` - Attach socket
- `SSL_connect()` - Client handshake
- `SSL_accept()` - Server handshake
- `SSL_read()` / `SSL_write()` - Data transfer
- `SSL_shutdown()` - Close connection
- `SSL_free()` - Free structure

#### BIO (I/O Abstraction)
```c
typedef struct bio_st BIO;
```
Binary I/O abstraction layer. Can wrap sockets, files, memory, etc.

**Socket BIO**:
```c
BIO *bio = BIO_new_socket(sock, BIO_NOCLOSE);
SSL_set_bio(ssl, bio, bio); // Set both read and write BIO
```

**AmiSSL-Specific Note**: File-based BIO functions (`BIO_set_fp`, `BIO_get_fp`) are NOT available because `FILE*` is incompatible. Use `_amiga` variants that accept `BPTR` instead:
- `BIO_set_fp_amiga()`
- `BIO_get_fp_amiga()`

#### X509 (Certificate)
```c
typedef struct x509_st X509;
```
X.509 certificate structure.

**Key Functions**:
- `SSL_get_peer_certificate()` - Get peer's certificate
- `X509_get_subject_name()` - Get subject DN
- `X509_get_issuer_name()` - Get issuer DN
- `X509_NAME_oneline()` - Convert DN to string
- `X509_free()` - Free certificate

#### SSL_SESSION
```c
typedef struct ssl_session_st SSL_SESSION;
```
Contains current TLS/SSL session details: ciphers, certificates, keys.

#### SSL_CIPHER
```c
typedef struct ssl_cipher_st SSL_CIPHER;
```
Algorithm information for a cipher.

**AmiSSL Addition**: `SSL_CIPHER_get_encryption()` - Inspect cipher details (AmiSSL-specific function)

---

## 5. Certificate Handling

### Certificate Verification

#### Setting Verification Mode

```c
// Enable peer certificate verification
SSL_CTX_set_verify(ctx, SSL_VERIFY_PEER, verify_callback);

// Verification modes:
// SSL_VERIFY_NONE - Don't verify (insecure!)
// SSL_VERIFY_PEER - Verify peer certificate
// SSL_VERIFY_FAIL_IF_NO_PEER_CERT - Require peer certificate (server mode)
// SSL_VERIFY_CLIENT_ONCE - Only verify once (server mode)
```

#### Verification Callback

```c
int verify_callback(int preverify_ok, X509_STORE_CTX *ctx) {
    // preverify_ok: 1 if certificate passed basic checks, 0 otherwise

    // Get certificate being verified
    X509 *cert = X509_STORE_CTX_get_current_cert(ctx);

    // Get verification error (if any)
    int err = X509_STORE_CTX_get_error(ctx);
    int depth = X509_STORE_CTX_get_error_depth(ctx);

    // Get subject name
    char subject[256];
    X509_NAME_oneline(X509_get_subject_name(cert), subject, sizeof(subject));

    Printf("Verify depth=%d, subject=%s\n", depth, subject);

    if (!preverify_ok) {
        Printf("Certificate verification error: %s\n",
               X509_verify_cert_error_string(err));
        // Return 1 to accept despite error, 0 to reject
    }

    return preverify_ok; // Accept OpenSSL's decision
}
```

#### Checking Verification Result

```c
// After SSL_connect()
X509 *cert = SSL_get_peer_certificate(ssl);
if (cert) {
    long result = SSL_get_verify_result(ssl);
    if (result != X509_V_OK) {
        const char *err_str = X509_verify_cert_error_string(result);
        Printf("Certificate verification failed: %s\n", err_str);
        // Decide whether to proceed or abort connection
    }
    X509_free(cert);
} else {
    Printf("No peer certificate received!\n");
}
```

### CA Certificate Storage

**System Locations**:
- `AmiSSL:Certs/` - Root CA certificates (managed by AmiSSL updates)
- `AmiSSL:UserCerts/` - User-added certificates (preserved across updates)
- `AmiSSL:Private/` - Private keys

**Certificate Format**: PEM format with hash-based filenames (OpenSSL c_rehash style)

**Updates**: Root CA certificates are automatically updated with each AmiSSL release, synchronized with Mozilla's certificate bundle from [curl.se](https://curl.se/docs/caextract.html).

**Important**:
- Do NOT manually add certificates to `AmiSSL:Certs/` - they will be lost on updates
- Use `AmiSSL:UserCerts/` for custom certificates
- System clock MUST be set correctly for certificate validation (expiry checks)

### Loading Certificates

```c
// Load system default CA paths
SSL_CTX_set_default_verify_paths(ctx);

// Or specify custom locations
SSL_CTX_load_verify_locations(ctx,
    "AmiSSL:UserCerts/my-ca.pem",  // Single file
    "AmiSSL:Certs");                // Directory

// Load client certificate (for mutual TLS)
SSL_CTX_use_certificate_file(ctx, "AmiSSL:UserCerts/client-cert.pem",
                             SSL_FILETYPE_PEM);
SSL_CTX_use_PrivateKey_file(ctx, "AmiSSL:Private/client-key.pem",
                            SSL_FILETYPE_PEM);

// Verify private key matches certificate
if (!SSL_CTX_check_private_key(ctx)) {
    Printf("Private key does not match certificate!\n");
}
```

### Custom Certificate Validation

For advanced use cases (pinning, custom trust stores, etc.):

```c
// Custom verification callback with full control
int custom_verify(int preverify_ok, X509_STORE_CTX *ctx) {
    X509 *cert = X509_STORE_CTX_get_current_cert(ctx);

    // Get certificate fingerprint
    unsigned char fingerprint[EVP_MAX_MD_SIZE];
    unsigned int fingerprint_len;
    X509_digest(cert, EVP_sha256(), fingerprint, &fingerprint_len);

    // Compare against pinned fingerprint
    unsigned char pinned[] = { /* ... expected SHA-256 ... */ };
    if (fingerprint_len == 32 &&
        memcmp(fingerprint, pinned, 32) == 0) {
        return 1; // Accept
    }

    // Fall back to standard verification
    return preverify_ok;
}
```

---

## 6. Non-Blocking Sockets Integration

### Why Non-Blocking Matters

Non-blocking sockets are essential for:
- Async I/O integration with Novus runtime
- Event-driven applications
- GUI responsiveness
- Multi-connection handling
- Integration with `WaitSelect()` for signal-based async

### Setting Non-Blocking Mode

```c
// bsdsocket.library method
long nonblock = 1;
if (IoctlSocket(sock, FIONBIO, &nonblock) != 0) {
    // Error handling
}

// Or using OpenSSL BIO helper (after SSL_set_fd)
BIO *bio = SSL_get_rbio(ssl);
BIO_set_nbio(bio, 1);
```

### SSL_ERROR_WANT_READ and SSL_ERROR_WANT_WRITE

**Critical Concept**: SSL operations can require OPPOSITE I/O direction due to renegotiation:
- `SSL_read()` may return `SSL_ERROR_WANT_WRITE` (needs to send data first)
- `SSL_write()` may return `SSL_ERROR_WANT_READ` (needs to receive data first)

**Handling Pattern**:

```c
int do_ssl_read(SSL *ssl, void *buf, size_t len, size_t *readbytes) {
    int ret = SSL_read_ex(ssl, buf, len, readbytes);
    if (ret > 0) {
        return 1; // Success
    }

    int err = SSL_get_error(ssl, ret);
    switch (err) {
        case SSL_ERROR_WANT_READ:
            // Socket needs to be readable - wait for read event
            return 0; // Retry later

        case SSL_ERROR_WANT_WRITE:
            // Socket needs to be writable - wait for write event
            // (This can happen during renegotiation!)
            return 0; // Retry later

        case SSL_ERROR_ZERO_RETURN:
            // Clean shutdown
            return -1;

        case SSL_ERROR_SYSCALL:
        case SSL_ERROR_SSL:
            // Fatal error
            ERR_print_errors_fp(stderr);
            return -1;
    }

    return -1; // Unknown error
}

int do_ssl_write(SSL *ssl, const void *buf, size_t len, size_t *written) {
    int ret = SSL_write_ex(ssl, buf, len, written);
    if (ret > 0) {
        return 1; // Success
    }

    int err = SSL_get_error(ssl, ret);
    switch (err) {
        case SSL_ERROR_WANT_READ:
            // Socket needs to be readable
            return 0; // Retry later

        case SSL_ERROR_WANT_WRITE:
            // Socket needs to be writable
            return 0; // Retry later

        case SSL_ERROR_ZERO_RETURN:
            // Peer closed connection (fatal during write)
            return -1;

        case SSL_ERROR_SYSCALL:
        case SSL_ERROR_SSL:
            ERR_print_errors_fp(stderr);
            return -1;
    }

    return -1;
}
```

### Integration with WaitSelect()

bsdsocket.library provides `WaitSelect()` which can integrate with Exec signals:

```c
// State machine for async SSL operations
typedef enum {
    SSL_STATE_IDLE,
    SSL_STATE_CONNECTING,
    SSL_STATE_WANT_READ,
    SSL_STATE_WANT_WRITE,
    SSL_STATE_CONNECTED,
    SSL_STATE_ERROR
} ssl_state_t;

typedef struct {
    SSL *ssl;
    int sock;
    ssl_state_t state;
    ULONG signal_mask;  // Signal to trigger on I/O ready
} ssl_async_t;

// Example async SSL_connect wrapper
int ssl_async_connect_step(ssl_async_t *async) {
    int ret = SSL_connect(async->ssl);

    if (ret == 1) {
        async->state = SSL_STATE_CONNECTED;
        return 1; // Handshake complete
    }

    int err = SSL_get_error(async->ssl, ret);
    switch (err) {
        case SSL_ERROR_WANT_READ:
            async->state = SSL_STATE_WANT_READ;
            return 0; // Not done, wait for readable

        case SSL_ERROR_WANT_WRITE:
            async->state = SSL_STATE_WANT_WRITE;
            return 0; // Not done, wait for writable

        default:
            async->state = SSL_STATE_ERROR;
            return -1; // Fatal error
    }
}

// In main event loop:
void event_loop(ssl_async_t *async) {
    fd_set readfds, writefds;
    ULONG exec_signals = 0; // Other Exec signals to wait on

    while (async->state != SSL_STATE_CONNECTED &&
           async->state != SSL_STATE_ERROR) {

        FD_ZERO(&readfds);
        FD_ZERO(&writefds);

        // Set appropriate fd_set based on what SSL needs
        if (async->state == SSL_STATE_WANT_READ ||
            async->state == SSL_STATE_CONNECTING) {
            FD_SET(async->sock, &readfds);
        }
        if (async->state == SSL_STATE_WANT_WRITE) {
            FD_SET(async->sock, &writefds);
        }

        // WaitSelect integrates with Exec signals
        ULONG signals = WaitSelect(async->sock + 1,
                                   &readfds, &writefds, NULL,
                                   NULL, &exec_signals);

        if (signals & exec_signals) {
            // Handle Exec signals (e.g., Ctrl-C)
            break;
        }

        if (FD_ISSET(async->sock, &readfds) ||
            FD_ISSET(async->sock, &writefds)) {
            // Socket ready, retry SSL operation
            ssl_async_connect_step(async);
        }
    }
}
```

### Critical Retry Rules

**From OpenSSL Documentation**:

1. **Same Arguments Required**: When retrying after `SSL_ERROR_WANT_READ/WRITE`, you MUST call the same SSL function with the SAME arguments
   ```c
   // WRONG:
   SSL_read(ssl, buf1, 100);  // Returns WANT_READ
   SSL_read(ssl, buf2, 200);  // Different buffer/size - BREAKS SSL STATE!

   // RIGHT:
   int ret = SSL_read(ssl, buf, 100);
   if (SSL_get_error(ssl, ret) == SSL_ERROR_WANT_READ) {
       // Wait for readable...
       ret = SSL_read(ssl, buf, 100); // Same buf, same size
   }
   ```

2. **Buffer Pointer Exception**: You CAN use different buffer pointers IF `SSL_MODE_ACCEPT_MOVING_WRITE_BUFFER` is enabled:
   ```c
   SSL_set_mode(ssl, SSL_MODE_ACCEPT_MOVING_WRITE_BUFFER);
   // Now buffer pointer can change, but data must be identical
   ```

3. **Partial Processing**: When `SSL_write()` is interrupted, it may have processed part of the data. The retry must still pass the FULL original buffer.

### Tracking State for Async

```c
typedef struct {
    SSL *ssl;
    int sock;

    // Read state
    int read_blocked_on_write;  // SSL_read needs write
    void *read_buf;
    size_t read_len;
    size_t read_offset;

    // Write state
    int write_blocked_on_read;  // SSL_write needs read
    const void *write_buf;
    size_t write_len;
    size_t write_offset;
} ssl_async_state_t;
```

### Example: Non-Blocking HTTPS GET

Complete example combining all concepts:

```c
typedef struct {
    SSL_CTX *ctx;
    SSL *ssl;
    int sock;

    enum { CONNECTING, HANDSHAKING, SENDING, RECEIVING, DONE } phase;
    int want_read, want_write;

    char send_buf[1024];
    size_t send_len, send_offset;

    char recv_buf[4096];
    size_t recv_len;
} https_request_t;

int https_request_step(https_request_t *req) {
    int ret;

    switch (req->phase) {
        case CONNECTING:
            // Socket connection already established
            req->ssl = SSL_new(req->ctx);
            SSL_set_fd(req->ssl, req->sock);
            SSL_set_tlsext_host_name(req->ssl, "example.com");
            req->phase = HANDSHAKING;
            req->want_read = 1; // Handshake needs read
            return 0; // Continue

        case HANDSHAKING:
            ret = SSL_connect(req->ssl);
            if (ret == 1) {
                // Handshake complete
                req->phase = SENDING;
                req->want_read = 0;
                req->want_write = 1;
                return 0;
            }

            int err = SSL_get_error(req->ssl, ret);
            if (err == SSL_ERROR_WANT_READ) {
                req->want_read = 1;
                req->want_write = 0;
                return 0; // Retry
            } else if (err == SSL_ERROR_WANT_WRITE) {
                req->want_read = 0;
                req->want_write = 1;
                return 0; // Retry
            } else {
                return -1; // Error
            }

        case SENDING:
            ret = SSL_write(req->ssl,
                          req->send_buf + req->send_offset,
                          req->send_len - req->send_offset);
            if (ret > 0) {
                req->send_offset += ret;
                if (req->send_offset >= req->send_len) {
                    // All sent
                    req->phase = RECEIVING;
                    req->want_read = 1;
                    req->want_write = 0;
                }
                return 0;
            }

            err = SSL_get_error(req->ssl, ret);
            if (err == SSL_ERROR_WANT_READ) {
                req->want_read = 1;
                req->want_write = 0;
            } else if (err == SSL_ERROR_WANT_WRITE) {
                req->want_read = 0;
                req->want_write = 1;
            } else {
                return -1; // Error
            }
            return 0; // Retry

        case RECEIVING:
            ret = SSL_read(req->ssl,
                         req->recv_buf + req->recv_len,
                         sizeof(req->recv_buf) - req->recv_len);
            if (ret > 0) {
                req->recv_len += ret;
                // Check if response complete (simplified)
                if (req->recv_len >= sizeof(req->recv_buf) - 1) {
                    req->phase = DONE;
                    return 1; // Complete
                }
                return 0; // Continue reading
            } else if (ret == 0) {
                // EOF
                req->phase = DONE;
                return 1; // Complete
            }

            err = SSL_get_error(req->ssl, ret);
            if (err == SSL_ERROR_WANT_READ) {
                req->want_read = 1;
                req->want_write = 0;
            } else if (err == SSL_ERROR_WANT_WRITE) {
                req->want_read = 0;
                req->want_write = 1;
            } else {
                return -1; // Error
            }
            return 0; // Retry
    }

    return -1; // Unknown state
}

// Usage in event loop:
void https_event_loop(https_request_t *req) {
    fd_set readfds, writefds;

    while (req->phase != DONE) {
        FD_ZERO(&readfds);
        FD_ZERO(&writefds);

        if (req->want_read) FD_SET(req->sock, &readfds);
        if (req->want_write) FD_SET(req->sock, &writefds);

        int nfds = WaitSelect(req->sock + 1, &readfds, &writefds,
                             NULL, NULL, NULL);
        if (nfds < 0) break;

        if (FD_ISSET(req->sock, &readfds) ||
            FD_ISSET(req->sock, &writefds)) {
            int result = https_request_step(req);
            if (result < 0) {
                Printf("HTTPS request failed\n");
                break;
            } else if (result > 0) {
                Printf("HTTPS request complete\n");
                break;
            }
        }
    }
}
```

---

## 7. Practical HTTPS Client Example

Based on AmiSSL's `test/https.c` example:

```c
#include <proto/exec.h>
#include <proto/dos.h>
#include <proto/socket.h>
#include <proto/amissl.h>
#include <openssl/ssl.h>
#include <openssl/err.h>

struct Library *SocketBase = NULL;
struct Library *AmiSSLMasterBase = NULL;
struct Library *AmiSSLBase = NULL;

int main(void) {
    SSL_CTX *ctx = NULL;
    SSL *ssl = NULL;
    int sock = -1;
    int ret = 1;

    // 1. Open libraries
    SocketBase = OpenLibrary("bsdsocket.library", 4);
    if (!SocketBase) {
        Printf("Failed to open bsdsocket.library\n");
        return 1;
    }

    AmiSSLMasterBase = OpenLibrary("amisslmaster.library",
                                   AMISSLMASTER_MIN_VERSION);
    if (!AmiSSLMasterBase) {
        Printf("Failed to open amisslmaster.library\n");
        goto cleanup;
    }

    if (!InitAmiSSLMaster(AMISSL_CURRENT_VERSION, TRUE)) {
        Printf("Failed to initialize AmiSSL Master\n");
        goto cleanup;
    }

    if (OpenAmiSSLTags(AMISSL_UsesOpenSSLStructs, TRUE,
                       AMISSL_GetAmiSSLBase, &AmiSSLBase,
                       TAG_DONE) != 0) {
        Printf("Failed to open AmiSSL\n");
        goto cleanup;
    }

    // 2. Initialize SSL
    OPENSSL_init_ssl(OPENSSL_INIT_SSL_DEFAULT |
                     OPENSSL_INIT_LOAD_SSL_STRINGS |
                     OPENSSL_INIT_LOAD_CRYPTO_STRINGS, NULL);

    // Seed entropy
    struct timeval tv;
    GetSysTime(&tv);
    RAND_seed(&tv, sizeof(tv));

    // 3. Create SSL context
    ctx = SSL_CTX_new(TLS_client_method());
    if (!ctx) {
        Printf("Failed to create SSL context\n");
        ERR_print_errors_fp(Output());
        goto cleanup;
    }

    // Set certificate verification
    SSL_CTX_set_default_verify_paths(ctx);
    SSL_CTX_set_verify(ctx, SSL_VERIFY_PEER, NULL);

    // 4. Create socket and connect
    sock = socket(AF_INET, SOCK_STREAM, 0);
    if (sock < 0) {
        Printf("Failed to create socket\n");
        goto cleanup;
    }

    // DNS lookup
    struct hostent *he = gethostbyname("example.com");
    if (!he) {
        Printf("DNS lookup failed\n");
        goto cleanup;
    }

    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(443);
    memcpy(&addr.sin_addr, he->h_addr_list[0], he->h_length);

    if (connect(sock, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
        Printf("Connection failed\n");
        goto cleanup;
    }

    Printf("Connected to %s:443\n", "example.com");

    // 5. SSL handshake
    ssl = SSL_new(ctx);
    if (!ssl) {
        Printf("Failed to create SSL object\n");
        goto cleanup;
    }

    SSL_set_fd(ssl, sock);
    SSL_set_tlsext_host_name(ssl, "example.com");

    if (SSL_connect(ssl) != 1) {
        Printf("SSL handshake failed\n");
        ERR_print_errors_fp(Output());
        goto cleanup;
    }

    Printf("SSL handshake successful\n");

    // Verify certificate
    X509 *cert = SSL_get_peer_certificate(ssl);
    if (cert) {
        long verify_result = SSL_get_verify_result(ssl);
        if (verify_result == X509_V_OK) {
            Printf("Certificate verified successfully\n");
        } else {
            Printf("Certificate verification failed: %s\n",
                   X509_verify_cert_error_string(verify_result));
        }

        char subject[256];
        X509_NAME_oneline(X509_get_subject_name(cert),
                         subject, sizeof(subject));
        Printf("Subject: %s\n", subject);

        X509_free(cert);
    }

    // 6. Send HTTP request
    const char *request =
        "GET / HTTP/1.1\r\n"
        "Host: example.com\r\n"
        "User-Agent: AmiSSL-Test/1.0\r\n"
        "Connection: close\r\n"
        "\r\n";

    int written = SSL_write(ssl, request, strlen(request));
    if (written <= 0) {
        Printf("SSL_write failed\n");
        goto cleanup;
    }

    Printf("Sent %d bytes\n", written);

    // 7. Receive response
    char buffer[4096];
    int total = 0;
    int len;

    Printf("Response:\n");
    while ((len = SSL_read(ssl, buffer, sizeof(buffer) - 1)) > 0) {
        buffer[len] = '\0';
        Printf("%s", buffer);
        total += len;
    }

    Printf("\n\nReceived %d bytes total\n", total);
    ret = 0; // Success

cleanup:
    // 8. Cleanup
    if (ssl) {
        SSL_shutdown(ssl);
        SSL_free(ssl);
    }
    if (sock >= 0) {
        CloseSocket(sock);
    }
    if (ctx) {
        SSL_CTX_free(ctx);
    }
    if (AmiSSLBase) {
        CleanupAmiSSLA();
        CloseAmiSSL();
    }
    if (AmiSSLMasterBase) {
        CloseLibrary(AmiSSLMasterBase);
    }
    if (SocketBase) {
        CloseLibrary(SocketBase);
    }

    return ret;
}
```

---

## 8. Integration with Novus Async Runtime

### Design Considerations

Novus uses signal-based futures with `WaitSelect()` integration. AmiSSL fits naturally into this model:

1. **Non-blocking sockets**: Enable via `IoctlSocket(sock, FIONBIO, &nonblock)`
2. **Signal integration**: `WaitSelect()` can trigger on socket readiness OR Exec signals
3. **State machine**: Track SSL operation state across async yields
4. **Result types**: Wrap SSL operations in `Result[T, SSLError]`

### Proposed Novus API

```novus
// stdlib module: std::net::ssl

use std::net::{TcpStream, SocketAddr}
use std::async::{Future, Poll}
use std::result::Result

// Opaque handles (mapped to SSL_CTX*, SSL*, X509*)
type SSLContext = Handle
type SSLConnection = Handle
type Certificate = Handle

enum SSLError {
    HandshakeFailed(String),
    CertificateVerificationFailed(String),
    ReadFailed(String),
    WriteFailed(String),
    SystemError(i32),
}

// SSL Context builder
struct SSLContextBuilder {
    verify_peer: bool,
    ca_file: Option[String],
    ca_dir: Option[String],
    min_tls_version: TLSVersion,
}

impl SSLContextBuilder {
    fn new() -> Self { ... }

    fn verify_peer(mut self, verify: bool) -> Self {
        self.verify_peer = verify
        self
    }

    fn ca_certificates(mut self, dir: String) -> Self {
        self.ca_dir = Some(dir)
        self
    }

    fn min_tls_version(mut self, version: TLSVersion) -> Self {
        self.min_tls_version = version
        self
    }

    fn build(self) -> Result[SSLContext, SSLError] {
        unsafe {
            let ctx = SSL_CTX_new(TLS_client_method())
            if ctx.is_null() {
                return Err(SSLError::HandshakeFailed("Context creation failed"))
            }

            if self.verify_peer {
                SSL_CTX_set_verify(ctx, SSL_VERIFY_PEER, None)
            }

            if let Some(dir) = self.ca_dir {
                SSL_CTX_load_verify_locations(ctx, None, Some(dir))
            } else {
                SSL_CTX_set_default_verify_paths(ctx)
            }

            Ok(SSLContext::from_raw(ctx))
        }
    }
}

// Async SSL connection
struct SSLStream {
    ssl: SSLConnection,
    sock: TcpStream,
    state: SSLState,
}

enum SSLState {
    Idle,
    Handshaking { want_read: bool, want_write: bool },
    Connected,
    ShuttingDown,
    Closed,
}

impl SSLStream {
    async fn connect(
        ctx: SSLContext,
        stream: TcpStream,
        hostname: String
    ) -> Result[Self, SSLError] {
        // Set non-blocking
        stream.set_nonblocking(true)?

        unsafe {
            let ssl = SSL_new(ctx.as_raw())
            SSL_set_fd(ssl, stream.as_raw_fd())
            SSL_set_tlsext_host_name(ssl, hostname.as_ptr())
        }

        let mut conn = SSLStream {
            ssl: SSLConnection::from_raw(ssl),
            sock: stream,
            state: SSLState::Handshaking { want_read: true, want_write: false },
        }

        // Async handshake
        conn.do_handshake().await?

        Ok(conn)
    }

    async fn do_handshake(&mut self) -> Result[(), SSLError] {
        loop {
            let ret = unsafe { SSL_connect(self.ssl.as_raw()) }

            if ret == 1 {
                self.state = SSLState::Connected
                return Ok(())
            }

            let err = unsafe { SSL_get_error(self.ssl.as_raw(), ret) }

            match err {
                SSL_ERROR_WANT_READ => {
                    // Yield until socket is readable
                    self.sock.wait_readable().await?
                }
                SSL_ERROR_WANT_WRITE => {
                    // Yield until socket is writable
                    self.sock.wait_writable().await?
                }
                _ => {
                    return Err(SSLError::HandshakeFailed(
                        get_ssl_error_string()))
                }
            }
        }
    }

    async fn read(&mut self, buf: &mut [u8]) -> Result[usize, SSLError] {
        loop {
            let ret = unsafe {
                SSL_read(self.ssl.as_raw(), buf.as_mut_ptr(), buf.len())
            }

            if ret > 0 {
                return Ok(ret as usize)
            }

            let err = unsafe { SSL_get_error(self.ssl.as_raw(), ret) }

            match err {
                SSL_ERROR_WANT_READ => {
                    self.sock.wait_readable().await?
                }
                SSL_ERROR_WANT_WRITE => {
                    self.sock.wait_writable().await?
                }
                SSL_ERROR_ZERO_RETURN => {
                    return Ok(0) // EOF
                }
                _ => {
                    return Err(SSLError::ReadFailed(get_ssl_error_string()))
                }
            }
        }
    }

    async fn write(&mut self, buf: &[u8]) -> Result[usize, SSLError] {
        let mut offset = 0

        // Must retry with same buffer until all written
        while offset < buf.len() {
            let ret = unsafe {
                SSL_write(self.ssl.as_raw(),
                         buf[offset..].as_ptr(),
                         buf.len() - offset)
            }

            if ret > 0 {
                offset += ret as usize
                continue
            }

            let err = unsafe { SSL_get_error(self.ssl.as_raw(), ret) }

            match err {
                SSL_ERROR_WANT_READ => {
                    self.sock.wait_readable().await?
                }
                SSL_ERROR_WANT_WRITE => {
                    self.sock.wait_writable().await?
                }
                _ => {
                    return Err(SSLError::WriteFailed(get_ssl_error_string()))
                }
            }
        }

        Ok(offset)
    }

    fn peer_certificate(&self) -> Option[Certificate] {
        unsafe {
            let cert = SSL_get_peer_certificate(self.ssl.as_raw())
            if cert.is_null() {
                None
            } else {
                Some(Certificate::from_raw(cert))
            }
        }
    }

    fn verify_result(&self) -> Result[(), SSLError] {
        unsafe {
            let result = SSL_get_verify_result(self.ssl.as_raw())
            if result == X509_V_OK {
                Ok(())
            } else {
                Err(SSLError::CertificateVerificationFailed(
                    X509_verify_cert_error_string(result)))
            }
        }
    }
}

impl Drop for SSLStream {
    fn drop(&mut self) {
        unsafe {
            SSL_shutdown(self.ssl.as_raw())
            SSL_free(self.ssl.as_raw())
        }
    }
}

// Usage example:
async fn https_get(url: String) -> Result[String, SSLError] {
    let ctx = SSLContextBuilder::new()
        .verify_peer(true)
        .ca_certificates("AmiSSL:Certs")
        .build()?

    let stream = TcpStream::connect("example.com:443").await?
    let mut ssl_stream = SSLStream::connect(ctx, stream, "example.com").await?

    // Verify certificate
    ssl_stream.verify_result()?

    // Send request
    let request = "GET / HTTP/1.1\r\nHost: example.com\r\n\r\n"
    ssl_stream.write(request.as_bytes()).await?

    // Read response
    let mut response = String::new()
    let mut buf = [0u8; 4096]

    loop {
        let n = ssl_stream.read(&mut buf).await?
        if n == 0 { break }
        response.push_str(String::from_utf8_lossy(&buf[..n]))
    }

    Ok(response)
}
```

### Key Integration Points

1. **TcpStream::wait_readable() / wait_writable()**: Must integrate with `WaitSelect()` and signal back to async runtime
2. **Handle types**: RAII wrappers ensuring proper cleanup via `Drop`
3. **Error propagation**: Convert OpenSSL errors to Novus `Result` types
4. **Buffer management**: Ensure buffer stability across yield points (no moving buffers unless `SSL_MODE_ACCEPT_MOVING_WRITE_BUFFER`)

---

## 9. Key Differences from Standard OpenSSL

### AmiSSL-Specific

1. **Library Opening**: Must use `amisslmaster.library` proxy, not version-specific libraries
2. **Initialization**: Requires `InitAmiSSLMaster()` and `OpenAmiSSLTags()` before SSL use
3. **FILE* Incompatibility**: Cannot use `BIO_set_fp()` / `BIO_get_fp()` - use `_amiga` variants with `BPTR`
4. **Cleanup**: Must call `CleanupAmiSSLA()` / `CleanupAmiSSLTags()` before closing libraries
5. **Multi-version Support**: Different applications can use different AmiSSL versions simultaneously

### bsdsocket.library Specifics

1. **Use CloseSocket()**, not `close()`
2. **Use IoctlSocket()**, not `ioctl()`
3. **Thread Safety**: Socket descriptors cannot be shared between threads; open bsdsocket.library in each thread
4. **WaitSelect()**: AmigaOS-specific `select()` variant that integrates with Exec signals

### OpenSSL 3.x Changes (affects AmiSSL v5)

1. **Opaque Structures**: Cannot access structure fields directly - use accessor functions
2. **New Initialization**: `OPENSSL_init_ssl()` instead of `SSL_library_init()`
3. **Algorithm Providers**: New provider architecture for cryptographic algorithms
4. **Deprecated APIs**: Many legacy functions removed (e.g., `SSL_library_init()`, `SSL_load_error_strings()`)

---

## 10. Resources and References

### Official Documentation

- **AmiSSL GitHub**: [https://github.com/jens-maus/amissl](https://github.com/jens-maus/amissl)
- **AmiSSL Releases**: [https://github.com/jens-maus/amissl/releases](https://github.com/jens-maus/amissl/releases)
- **SDK Documentation**: [https://github.com/jens-maus/amissl/blob/master/dist/README-SDK](https://github.com/jens-maus/amissl/blob/master/dist/README-SDK)
- **AmiSSL README**: [https://github.com/jens-maus/amissl/blob/master/README.md](https://github.com/jens-maus/amissl/blob/master/README.md)

### Example Code

- **HTTPS Client Example**: [https://github.com/jens-maus/amissl/blob/master/test/https.c](https://github.com/jens-maus/amissl/blob/master/test/https.c)
- **HTTP GET Example**: [https://github.com/jens-maus/amissl/blob/master/test/httpget.c](https://github.com/jens-maus/amissl/blob/master/test/httpget.c)

### OpenSSL Documentation

- **OpenSSL 3.x Documentation**: [https://docs.openssl.org/3.3/](https://docs.openssl.org/3.3/)
- **Non-Blocking TLS Client Guide**: [https://docs.openssl.org/3.3/man7/ossl-guide-tls-client-non-block/](https://docs.openssl.org/3.3/man7/ossl-guide-tls-client-non-block/)
- **SSL Manual**: [https://www.openssl.org/docs/man1.1.1/man7/ssl.html](https://www.openssl.org/docs/man1.1.1/man7/ssl.html)
- **SSL_read Manual**: [https://linux.die.net/man/3/ssl_read](https://linux.die.net/man/3/ssl_read)

### Non-Blocking Examples

- **OpenSSL Non-Blocking Example (Gist)**: [https://gist.github.com/zapstar/cc043ff21b8dcb1419770405ef78cf27](https://gist.github.com/zapstar/cc043ff21b8dcb1419770405ef78cf27)

### Distribution

- **Aminet AmiSSL v5 OS3**: [https://aminet.net/package/util/libs/AmiSSL-v5-OS3](https://aminet.net/package/util/libs/AmiSSL-v5-OS3)
- **Aminet AmiSSL v5 OS4**: [https://aminet.net/package/util/libs/AmiSSL-v5-OS4](https://aminet.net/package/util/libs/AmiSSL-v5-OS4)

### Community

- **IBrowse AmiSSL Page**: [https://www.ibrowse-dev.net/amissl/](https://www.ibrowse-dev.net/amissl/)
- **English Amiga Board**: Discussion threads about AmiSSL usage

---

## 11. Summary for Novus Integration

### What AmiSSL Provides

- Full OpenSSL 3.6 API/ABI compatibility
- TLS 1.0 - 1.3 support with modern ciphers
- Standard SSL functions: `SSL_connect()`, `SSL_read()`, `SSL_write()`, etc.
- Certificate verification with Mozilla CA bundle
- Works on 68020+ AmigaOS systems

### Integration Strategy

1. **Wrap as stdlib module** (`std::net::ssl`)
2. **Use RAII handles** for automatic cleanup (`SSLContext`, `SSLConnection`)
3. **Async by default** - integrate with Novus signal-based futures
4. **Result-based API** - no exceptions, explicit error handling
5. **Non-blocking I/O** - use `WaitSelect()` for async yields

### Critical Implementation Notes

- Open bsdsocket.library in same thread as socket operations
- Always use `CloseSocket()` / `IoctlSocket()`, never POSIX `close()` / `ioctl()`
- Retry SSL operations with SAME arguments after `WANT_READ` / `WANT_WRITE`
- SSL operations can need opposite I/O direction (read may need write, vice versa)
- Buffers must not move during retry unless `SSL_MODE_ACCEPT_MOVING_WRITE_BUFFER` set
- System clock must be correct for certificate expiry validation
- CA certificates in `AmiSSL:Certs` updated automatically, use `AmiSSL:UserCerts` for custom

### Novus-Specific Advantages

- Signal-based async runtime fits perfectly with `WaitSelect()` integration
- No GC means predictable SSL buffer management
- `defer` blocks ensure proper `SSL_shutdown()` / `SSL_free()` even on early return
- `Result[T, SSLError]` prevents silent SSL failures
- `unsafe` blocks clearly mark FFI boundaries

This integration will enable secure HTTPS communication in Novus applications while maintaining the language's safety and performance guarantees.
