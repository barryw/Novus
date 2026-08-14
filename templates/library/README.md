# {{PROJECT_NAME}} library workspace

This workspace contains an AmigaOS shared library and a safe Novus client.

```sh
novusc build --release
```

Outputs:

- `target/release/libs/{{PROJECT_NAME}}.library` — install in `LIBS:`.
- `target/release/libs/{{PROJECT_NAME}}.h` and `.fd` — generated C interface.
- `target/release/libs/{{PROJECT_NAME}}.novus` — generated Result-based Novus client interface.
- `target/release/bins/{{PROJECT_NAME}}-example` — returns `0` on success, `5` when the library is unavailable, and `10` on a wrong result.

Public methods in the library impl become vectors. A method with `&var self` uses compiler-owned persistent state without exposing the receiver to clients. The compiler also owns the resident tag, base layout, A6 wrappers, open counts, delayed expunge, and dependency lease.

The generated client checks availability before every call. A missing file in `LIBS:` becomes `Result::Err`, never a call through a null base and never a Guru.
