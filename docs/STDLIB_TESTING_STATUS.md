# Standard-library testing status

The standard library is checked at four boundaries:

1. C# compiler tests validate parsing, ownership, borrow checking, imports, IR, C generation, and library architecture.
2. The example compiler sweep compiles every supported Novus example in process.
3. Full-link tests exercise representative programs through VBCC and enforce the idiomatic GUI size budget.
4. `tools/amiga/run_runtime_suite.py` executes behavior on the A4000 guest and reports program failures and Gurus.

The canonical library-architecture checks live in `Novus.Tests/AmigaLibraryDesignTests.cs`. Borrow and ownership conventions are checked by `StdlibBorrowContractTests.cs`, while `Novus.Tests/AmigaRuntime/interop_ownership.novus` exercises application → systems → raw borrowing and ownership round trips on AmigaOS.

Do not add compile-only probes at the repository root. Add focused C# compiler tests or Novus `@test` cases to the appropriate runtime suite.

Run the focused acceptance checks with:

```text
dotnet test Novus.Tests/Novus.Tests.csproj --filter FullyQualifiedName~AmigaLibraryDesignTests
dotnet test Novus.Tests/Novus.Tests.csproj --filter FullyQualifiedName~StdlibBorrowContractTests
python3 tools/amiga/run_runtime_suite.py --suite interop-ownership --profile release-o1
```
