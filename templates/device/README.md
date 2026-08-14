# {{PROJECT_NAME}} device workspace

This workspace contains an AmigaOS device and a Novus client that exercises synchronous I/O, asynchronous completion, and safe cancellation.

```sh
novusc build --release
```

Outputs:

- `target/release/devs/{{PROJECT_NAME}}.device` — install in `DEVS:` or place beside the example during development.
- `target/release/bins/{{PROJECT_NAME}}-example` — returns `0` when every command succeeds, `5` when the device is absent, and `10` on a command failure.

The compiler generates the resident tag, lifecycle vectors, unit validation, `BeginIO`, `AbortIO`, and A6 wrappers. The example safely loads the driver from `DEVS:` or `PROGDIR:` when necessary. Its `DeviceRequest` owns the reply port, request allocation, device lease, and pending I/O; dropping it safely aborts and collects unfinished work.

Use `command()` for synchronous operations. For asynchronous work, call `send()`, do unrelated work, then `wait()` or `abort()`. A deferred driver handler owns the request until it replies; an `@abortio` hook must atomically reclaim cancellable requests.

Raw pointers are confined to `device/src/dev.novus`, the AmigaOS ABI boundary. Application code should stay on the safe `amiga::sys::device` API.
