# Stdlib Test Coverage Audit

**Date:** November 7, 2025
**Total Public Functions:** 35
**Functions with Tests:** 0
**Coverage:** 0%

## Critical Finding

**ZERO** stdlib functions have dedicated end-to-end tests. All existing tests are compiler feature tests, not stdlib API tests.

## Public Stdlib API Inventory

### std::exec (11 functions)
- [ ] `get_current_task() -> Option<*Task>`
- [ ] `find_task(name: *u8) -> Option<*Task>`
- [ ] `set_task_priority(task: *Task, priority: i32) -> i8`
- [ ] `allocate_signal(signal_num: i32) -> Option<i32>`
- [ ] `free_signal(signal_num: i32)`
- [ ] `wait_for_signals(signal_mask: u32) -> u32`
- [ ] `send_signal(task: *Task, signal_mask: u32)`
- [ ] `forbid()`
- [ ] `permit()`
- [ ] `disable()`
- [ ] `enable()`

### std::error (10 functions)
- [ ] `dos_last_error() -> DosError`
- [ ] `dos_error_from_code(code: i32) -> DosError`
- [ ] `dos_error_to_code(err: DosError) -> i32`
- [ ] `exec_error_to_code(err: ExecError) -> i32`
- [ ] `intuition_error_to_code(err: IntuitionError) -> i32`
- [ ] `graphics_error_to_code(err: GraphicsError) -> i32`
- [ ] `novus_error_from_dos(err: DosError) -> NovusError`
- [ ] `novus_error_from_exec(err: ExecError) -> NovusError`
- [ ] `novus_error_from_intuition(err: IntuitionError) -> NovusError`
- [ ] `novus_error_from_graphics(err: GraphicsError) -> NovusError`
- [ ] `novus_error_to_code(err: NovusError) -> i32`

### std::dos (9 functions)
- [ ] `open_file(path: *u8, mode: i32) -> Result<*FileHandle, NovusError>`
- [ ] `close_file(fh: *FileHandle)`
- [ ] `read_file(fh: *FileHandle, buffer: *u8, length: i32) -> Result<i32, NovusError>`
- [ ] `write_file(fh: *FileHandle, buffer: *u8, length: i32) -> Result<i32, NovusError>`
- [ ] `seek_file(fh: *FileHandle, position: i32, mode: i32) -> Result<i32, NovusError>`
- [ ] `AllocDos(obj_type: u32, tags_ptr: *TagItem, count: u32) -> Result<*u8, NovusError>`
- [ ] `CreateProcess(tags_ptr: *TagItem, count: u32) -> Result<*Process, NovusError>`
- [ ] `LoadSeg(file: *u8) -> Result<u32, NovusError>`
- [ ] `System(command: *u8, tags_ptr: *TagItem, count: u32) -> i32`

### std::intuition (2 functions)
- [ ] `OpenWindow(tags_ptr: *TagItem, count: u32) -> Result<*Window, NovusError>`
- [ ] `OpenScreen(tags_ptr: *TagItem, count: u32) -> Result<*Screen, NovusError>`

### std::io (1 function)
- [ ] `write_array(format: *u8, ...)`

### std::tags (1 function)
- [ ] `make_tags(tags_ptr: *TagItem, ...)`

### std::strings (1 function - MISSING from grep, need to verify)
- [ ] TODO: Audit std::strings module

## Top 10 Priority Tests (Immediate Action)

Based on usage frequency and criticality:

1. **exec::get_current_task** - Core Amiga operation, used in signal handling
2. **exec::allocate_signal** - Required for async/message ports
3. **exec::free_signal** - Resource cleanup
4. **dos::open_file** - File I/O foundation
5. **dos::close_file** - File I/O cleanup
6. **dos::read_file** - Basic file operations
7. **dos::write_file** - Basic file operations
8. **error::dos_last_error** - Error handling
9. **error::novus_error_from_dos** - Error conversion
10. **io::write_array** - Varargs testing

## Test Strategy

### End-to-End Tests
- Compile Novus code that calls stdlib function
- Run on UAE or verify generated C code
- Assert correct behavior

### Example Test Template
```csharp
[Fact]
public void Stdlib_Exec_GetCurrentTask_ReturnsValidTask()
{
    var code = @"
        import std::exec;

        fn main() -> i32 {
            let task = exec::get_current_task();
            if task.is_some() { 0 } else { 1 }
        }
    ";

    var result = CompileAndRun(code);
    Assert.Equal(0, result);
}
```

## Action Items

- [ ] Create `Novus.Tests/StdlibTests.cs`
- [ ] Write top 10 priority tests
- [ ] Add remaining 25 function tests
- [ ] Create stdlib versioning (see next doc)
- [ ] Add stdlib test CI check (must pass before merge)

## Risks

**Without stdlib tests:**
- Silent breakage when refactoring compiler
- Unknown if generated C code actually works on Amiga
- No regression protection for 52K LOC of stdlib
- User code breaks unexpectedly

**Recommendation:** BLOCK all new stdlib additions until test coverage exists for current API.
