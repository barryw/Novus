# Parameter Passing in Novus

Status: implemented. For the developer-facing rules and examples, see the
[Ownership and Memory Safety guide](../website/src/content/docs/guide/memory.md).

## Source-level contracts

| Signature | Contract |
|---|---|
| `value: T` | No ownership transfer; intended for primitives, `Copy` values, and small views |
| `consuming value: T` | Ownership transfers to the callee |
| `value: &T` | Shared, read-only borrow |
| `value: &var T` | Exclusive, mutable borrow |
| `value: *T` | Unchecked raw FFI/hardware address |

Use `&var value` at a mutable-borrow call site. Novus does not use `&mut`.

```novus
fn inspect(point: &Point) -> i16 {
    return point.x
}

fn translate(point: &var Point, dx: i16) {
    point.x = point.x + dx
}

fn enqueue(consuming job: Job) {
    // owns job
}

fn run() {
    var point = Point { x: 10, y: 20 }
    let x = inspect(&point)
    translate(&var point, 5)

    let job = Job::new()
    enqueue(job)
    // job is moved
}
```

## ABI

The C backend lowers parameters according to the selected ABI. That physical
representation is not the ownership contract: semantic analysis and IR retain
`IsConsuming`, reference mutability, and move state through imports, generic
instantiation, lowering, and code generation.

On 68020+, scalar values and addresses use 32-bit ABI slots. Aggregate layout
and Amiga register annotations follow the declared calling convention. Safe
Novus code should choose parameters from the source-level table rather than
encoding pass-by-reference with raw pointers.

Raw `*T` parameters remain necessary for NDK declarations and hardware access.
Safe wrapper modules should convert them immediately into an owning handle or
owner-tied view and keep the raw operation inside an explicit `unsafe` boundary.
