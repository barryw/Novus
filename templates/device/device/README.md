# Device implementation

Edit `src/dev.novus` to add state, lifecycle hooks, and commands.

- `@devicecmd(..., quick = true)` completes before `BeginIO` returns.
- A normal command is replied by generated code.
- `@devicecmd(..., deferred = true)` transfers ownership to the handler; it must later call `ReplyMsg`.
- `@abortio` returns `true` only after it has reclaimed the pending request. Generated code replies with `IOERR_ABORTED`.

The included commands demonstrate all three paths. Replace the immediate deferred example with a task or interrupt-backed queue when the device performs real background work, and protect that queue with the appropriate Exec synchronization.

Build from the workspace root with `novusc build --release`. Install `target/release/devs/{{PROJECT_NAME}}.device` in `DEVS:`.
