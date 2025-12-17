# {{PROJECT_NAME}} Example

This is an example program that demonstrates how to use {{PROJECT_NAME}}.device.

## Building

From the workspace root:
```bash
novusc build
```

Or from this directory:
```bash
novusc build
```

## Running

1. First, install the device to DEVS:
   ```bash
   copy ../device/build/{{PROJECT_NAME}}.device DEVS:
   ```

2. Run the example:
   ```bash
   ./{{PROJECT_NAME}}-example
   ```

## What It Does

This example:
1. Creates a message port for device replies
2. Creates an IORequest for communicating with the device
3. Opens {{PROJECT_NAME}}.device on unit 0
4. Sends custom commands to the device
5. Closes the device and cleans up

## Configuration

Edit `project.toml` to change build settings:

```toml
[package]
name = "{{PROJECT_NAME}}-example"
version = "1.0.0"
type = "cli"

[build]
target_cpu = "68020"
fpu = "auto"
```
