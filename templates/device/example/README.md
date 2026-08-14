# Safe device client

The example first opens an already-resident device. If necessary, it safely loads the driver from `DEVS:` or from beside the executable in `PROGDIR:`. It then opens a standard request, runs synchronous commands, collects a normally completed asynchronous command, and cancels a pending one.

No manual `MsgPort`, `IORequest`, `OpenDevice`, or cleanup calls are needed. `Result` exposes missing devices and command errors without a Guru, while ownership prevents the request or reply port from being freed during I/O.

Run `{{PROJECT_NAME}}-example` after installing or loading `{{PROJECT_NAME}}.device`. Exit status `0` means success, `5` means the device is unavailable, and `10` means a command contract failed.
