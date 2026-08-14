# Library implementation

Edit `src/lib.novus`. Public impl methods become library vectors in source order; use `&var self` only when a function needs persistent library state.

Optional `@libinit`, `@libopen`, `@libclose`, and `@libexpunge` hooks customize resource management. Returning `false` from initialization or open rejects the operation cleanly. Exec bookkeeping and delayed expunge remain compiler-owned.

Build from the workspace root with `novusc build --release`, then install `target/release/libs/{{PROJECT_NAME}}.library` in `LIBS:`.
