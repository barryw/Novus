# {{PROJECT_NAME}} AmigaDOS Handler

This template uses the handler-specific startup path: the initial DOS packet is
left on the process message port rather than being mistaken for a Workbench
startup message.

`Packet::wait()` takes ownership of one DOS packet. Call `reply()` on every path;
if a path forgets, `Drop` replies with `ERROR_ACTION_NOT_KNOWN` so DOS cannot hang
forever. Add supported `ACTION_*` cases to `handler/src/main.novus`.

Novus handlers require a 68020 or newer CPU.
