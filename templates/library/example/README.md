# Safe library client

The workspace dependency links an auto-open/close stub and checked call stubs. The example wraps those private symbols in ordinary Novus functions returning `Result`, so an absent library is an expected branch rather than a crash.

The compiler also emits the same safe interface as `target/release/libs/{{PROJECT_NAME}}.novus`; use that generated module as the public client API when distributing your library.

Run `{{PROJECT_NAME}}-example` after installing `{{PROJECT_NAME}}.library` in `LIBS:`. Exit status `0` means success, `5` means unavailable, and `10` means a library result was incorrect.
