# Profile-Guided Optimization (PGO) Design for Novus Compiler

**Status:** Design Phase
**Date:** 2025-12-04
**Target:** Amiga 68k (via VBCC C99 backend)

---

## Executive Summary

Profile-Guided Optimization (PGO) is a two-phase compilation strategy that uses runtime profiling data to guide optimization decisions. This document details the design of a PGO infrastructure for the Novus compiler, which targets Amiga 68k systems and generates C99 code compiled by VBCC.

**Key Benefits:**
- **Function inlining decisions** based on actual call frequencies
- **Branch prediction hints** for conditional branches (likely/unlikely)
- **Loop unrolling** for hot loops with known iteration counts
- **Code layout optimization** (hot/cold function splitting)
- **Register allocation hints** for frequently accessed variables
- **Dead code elimination** of rarely-executed paths

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         COMPILATION FLOW                         │
└─────────────────────────────────────────────────────────────────┘

Phase 1: Instrumented Build
───────────────────────────
Source (.novus)
    ↓
  Parser
    ↓
   AST
    ↓
IR Builder
    ↓
   IR (unoptimized)
    ↓
┌─────────────────────────────┐
│ INSTRUMENTATION PASS        │ ← Inserts profiling counters
│ - Function entry/exit       │
│ - Branch frequencies        │
│ - Loop iteration counts     │
│ - Call site tracking        │
└─────────────────────────────┘
    ↓
   IR (instrumented)
    ↓
Standard Optimizer (-O1)
    ↓
C Code Generator
    ↓
VBCC (vc +aos68k -c99)
    ↓
Amiga Executable + libprofile.a
    ↓
[User runs program on Amiga]
    ↓
novus_profile.pgo (profile data file)


Phase 2: Optimized Build
─────────────────────────
Source (.novus)
    ↓
  Parser
    ↓
   AST
    ↓
IR Builder
    ↓
   IR (unoptimized)
    ↓
┌─────────────────────────────┐
│ PROFILE-GUIDED OPTIMIZER    │ ← Uses profile data
│ - Hot function inlining     │
│ - Branch prediction hints   │
│ - Loop unrolling            │
│ - Code layout optimization  │
│ - Dead path elimination     │
└─────────────────────────────┘
    ↓
   IR (optimized with profile)
    ↓
Standard Optimizer (-O3)
    ↓
C Code Generator (with pragmas)
    ↓
VBCC (vc +aos68k -c99)
    ↓
Optimized Amiga Executable
```

---

## 2. Profile Data Collection

### 2.1 Instrumentation Points

The instrumentation pass inserts counters at strategic points in the IR:

```csharp
public class InstrumentationPass : IOptimizationPass
{
    public string Name => "PGO Instrumentation";

    public bool Run(IrModule module)
    {
        // Insert global counter arrays
        AddGlobalCounters(module);

        foreach (var function in module.Functions)
        {
            if (function.IsExtern) continue;

            // 1. Function entry counter
            InstrumentFunctionEntry(function);

            // 2. Branch counters
            InstrumentBranches(function);

            // 3. Loop iteration counters
            InstrumentLoops(function);

            // 4. Call site counters
            InstrumentCallSites(function);
        }

        // Add profile dump on exit
        AddProfileDumpAtExit(module);

        return true;
    }
}
```

#### Instrumentation Types:

**1. Function Entry Counters**
```c
// Generated C code
static u32 __pgo_func_counter[256];  // Max 256 functions

void my_function() {
    __pgo_func_counter[42]++;  // Function ID 42
    // Original function body...
}
```

**2. Branch Counters**
```c
static u32 __pgo_branch_counter[1024];  // Max 1024 branches

if (condition) {
    __pgo_branch_counter[100]++;  // Branch ID 100, true path
    // true path
} else {
    __pgo_branch_counter[101]++;  // Branch ID 101, false path
    // false path
}
```

**3. Loop Iteration Counters**
```c
static u32 __pgo_loop_total[128];      // Total iterations
static u32 __pgo_loop_executions[128]; // Number of times loop executed

for (...) {
    __pgo_loop_total[20]++;  // Loop ID 20
    // loop body
}
__pgo_loop_executions[20]++;  // After loop
```

**4. Call Site Counters**
```c
static u32 __pgo_call_counter[512];  // Max 512 call sites

__pgo_call_counter[75]++;  // Call site ID 75
result = some_function(arg);
```

### 2.2 Memory Considerations (Amiga Constraints)

The Amiga has limited RAM, so we must be efficient with instrumentation:

**Counter Size Budget:**
```
Function counters:    256 × 4 bytes = 1 KB
Branch counters:     1024 × 4 bytes = 4 KB
Loop counters:        128 × 8 bytes = 1 KB (total + executions)
Call site counters:   512 × 4 bytes = 2 KB
────────────────────────────────────────
Total:                              ~8 KB
```

**Optimization Strategies:**
- Use 16-bit counters where 32-bit is unnecessary (saves 50%)
- Saturating counters (stop at max value to prevent overflow)
- Compile-time flag to disable instrumentation for less-important functions
- Option to instrument only hot functions after initial profiling pass

### 2.3 Profile Data Format (.pgo file)

The profile data is written to a simple binary format:

```
┌─────────────────────────────────────┐
│       NOVUS_PGO_MAGIC (4 bytes)     │  "NPGO"
├─────────────────────────────────────┤
│       Version (2 bytes)             │  0x0001
├─────────────────────────────────────┤
│       Source Hash (32 bytes)        │  SHA-256 of source files
├─────────────────────────────────────┤
│       Timestamp (8 bytes)           │  Unix timestamp
├─────────────────────────────────────┤
│       Num Functions (2 bytes)       │
├─────────────────────────────────────┤
│       Num Branches (2 bytes)        │
├─────────────────────────────────────┤
│       Num Loops (2 bytes)           │
├─────────────────────────────────────┤
│       Num Call Sites (2 bytes)      │
├─────────────────────────────────────┤
│   Function Counters (variable)      │
│   ┌────────────────────────────┐    │
│   │ ID (2 bytes)               │    │
│   │ Name Length (1 byte)       │    │
│   │ Name (variable)            │    │
│   │ Count (4 bytes)            │    │
│   └────────────────────────────┘    │
│   [Repeat for each function]        │
├─────────────────────────────────────┤
│   Branch Counters (variable)        │
│   ┌────────────────────────────┐    │
│   │ ID (2 bytes)               │    │
│   │ Source Location (8 bytes)  │    │
│   │ True Count (4 bytes)       │    │
│   │ False Count (4 bytes)      │    │
│   └────────────────────────────┘    │
│   [Repeat for each branch]          │
├─────────────────────────────────────┤
│   Loop Counters (variable)          │
│   ┌────────────────────────────┐    │
│   │ ID (2 bytes)               │    │
│   │ Source Location (8 bytes)  │    │
│   │ Total Iterations (4 bytes) │    │
│   │ Num Executions (4 bytes)   │    │
│   └────────────────────────────┘    │
│   [Repeat for each loop]            │
├─────────────────────────────────────┤
│   Call Site Counters (variable)     │
│   ┌────────────────────────────┐    │
│   │ ID (2 bytes)               │    │
│   │ Caller Length (1 byte)     │    │
│   │ Caller Name (variable)     │    │
│   │ Callee Length (1 byte)     │    │
│   │ Callee Name (variable)     │    │
│   │ Count (4 bytes)            │    │
│   └────────────────────────────┘    │
│   [Repeat for each call site]       │
└─────────────────────────────────────┘
```

**File Size Estimate:** ~16-32 KB for typical programs

### 2.4 Profile Data Collection Runtime

The runtime library (`libprofile.a`) provides:

```c
// novus_runtime_profile.c

#include <proto/dos.h>
#include <proto/exec.h>

typedef struct {
    u16 id;
    u32 count;
    char name[64];
} FunctionProfile;

typedef struct {
    u16 id;
    u32 true_count;
    u32 false_count;
    u32 source_line;
    char source_file[32];
} BranchProfile;

// Global profile data (initialized by instrumented code)
extern u32 __pgo_func_counter[256];
extern u32 __pgo_branch_counter[1024];
extern u32 __pgo_loop_total[128];
extern u32 __pgo_loop_executions[128];
extern u32 __pgo_call_counter[512];

// Metadata (populated by instrumentation pass)
extern FunctionProfile __pgo_func_metadata[256];
extern BranchProfile __pgo_branch_metadata[1024];
// ...

// Write profile data to file on program exit
void __pgo_dump_profile(void) {
    BPTR file = Open("novus_profile.pgo", MODE_NEWFILE);
    if (!file) return;

    // Write header
    Write(file, "NPGO", 4);
    u16 version = 0x0001;
    Write(file, &version, 2);

    // Write source hash (computed at compile time)
    Write(file, __pgo_source_hash, 32);

    // Write timestamp
    u64 timestamp = (u64)time(NULL);
    Write(file, &timestamp, 8);

    // Write function counters
    u16 num_functions = __pgo_num_functions;
    Write(file, &num_functions, 2);
    for (u16 i = 0; i < num_functions; i++) {
        Write(file, &__pgo_func_metadata[i].id, 2);
        u8 name_len = strlen(__pgo_func_metadata[i].name);
        Write(file, &name_len, 1);
        Write(file, __pgo_func_metadata[i].name, name_len);
        Write(file, &__pgo_func_counter[i], 4);
    }

    // Write branch counters...
    // Write loop counters...
    // Write call site counters...

    Close(file);
}

// Register cleanup handler
__attribute__((constructor))
void __pgo_init(void) {
    atexit(__pgo_dump_profile);
}
```

---

## 3. Profile Data Format - Detailed Specification

### 3.1 ProfileData C# Class

```csharp
namespace Novus.Optimizer;

/// <summary>
/// Represents profile data collected from an instrumented run
/// </summary>
public class ProfileData
{
    public const uint MAGIC = 0x4E50474F; // "NPGO"
    public const ushort VERSION = 0x0001;

    public byte[] SourceHash { get; set; } = new byte[32];
    public DateTime Timestamp { get; set; }

    public Dictionary<string, FunctionProfile> Functions { get; } = new();
    public Dictionary<int, BranchProfile> Branches { get; } = new();
    public Dictionary<int, LoopProfile> Loops { get; } = new();
    public Dictionary<string, CallSiteProfile> CallSites { get; } = new();

    /// <summary>
    /// Load profile data from .pgo file
    /// </summary>
    public static ProfileData Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // Verify magic and version
        uint magic = reader.ReadUInt32();
        if (magic != MAGIC)
            throw new InvalidDataException("Invalid .pgo file: bad magic number");

        ushort version = reader.ReadUInt16();
        if (version != VERSION)
            throw new InvalidDataException($"Unsupported .pgo version: {version}");

        var profile = new ProfileData
        {
            SourceHash = reader.ReadBytes(32),
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64()).DateTime
        };

        // Read function profiles
        ushort numFunctions = reader.ReadUInt16();
        for (int i = 0; i < numFunctions; i++)
        {
            ushort id = reader.ReadUInt16();
            byte nameLen = reader.ReadByte();
            string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLen));
            uint count = reader.ReadUInt32();

            profile.Functions[name] = new FunctionProfile
            {
                Id = id,
                Name = name,
                ExecutionCount = count
            };
        }

        // Read branch profiles
        ushort numBranches = reader.ReadUInt16();
        for (int i = 0; i < numBranches; i++)
        {
            ushort id = reader.ReadUInt16();
            ulong sourceLoc = reader.ReadUInt64(); // Encoded file:line
            uint trueCount = reader.ReadUInt32();
            uint falseCount = reader.ReadUInt32();

            profile.Branches[id] = new BranchProfile
            {
                Id = id,
                SourceLocation = sourceLoc,
                TrueCount = trueCount,
                FalseCount = falseCount
            };
        }

        // Read loop profiles
        ushort numLoops = reader.ReadUInt16();
        for (int i = 0; i < numLoops; i++)
        {
            ushort id = reader.ReadUInt16();
            ulong sourceLoc = reader.ReadUInt64();
            uint totalIters = reader.ReadUInt32();
            uint execCount = reader.ReadUInt32();

            profile.Loops[id] = new LoopProfile
            {
                Id = id,
                SourceLocation = sourceLoc,
                TotalIterations = totalIters,
                ExecutionCount = execCount
            };
        }

        // Read call site profiles
        ushort numCallSites = reader.ReadUInt16();
        for (int i = 0; i < numCallSites; i++)
        {
            ushort id = reader.ReadUInt16();
            byte callerLen = reader.ReadByte();
            string caller = Encoding.UTF8.GetString(reader.ReadBytes(callerLen));
            byte calleeLen = reader.ReadByte();
            string callee = Encoding.UTF8.GetString(reader.ReadBytes(calleeLen));
            uint count = reader.ReadUInt32();

            string key = $"{caller}->{callee}";
            profile.CallSites[key] = new CallSiteProfile
            {
                Id = id,
                Caller = caller,
                Callee = callee,
                CallCount = count
            };
        }

        return profile;
    }

    /// <summary>
    /// Verify that profile data matches the current source code
    /// </summary>
    public bool VerifySourceHash(IrModule module)
    {
        var currentHash = ComputeSourceHash(module);
        return currentHash.SequenceEqual(SourceHash);
    }

    private static byte[] ComputeSourceHash(IrModule module)
    {
        // Compute SHA-256 hash of all source file contents
        // This ensures profile data matches the source being compiled
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        // Implementation details...
        return sha256.ComputeHash(Array.Empty<byte>()); // Placeholder
    }
}

public class FunctionProfile
{
    public ushort Id { get; set; }
    public string Name { get; set; } = "";
    public uint ExecutionCount { get; set; }

    /// <summary>
    /// Is this a hot function? (executed more than threshold)
    /// </summary>
    public bool IsHot(uint threshold = 1000) => ExecutionCount >= threshold;

    /// <summary>
    /// Is this a cold function? (rarely or never executed)
    /// </summary>
    public bool IsCold(uint threshold = 10) => ExecutionCount < threshold;
}

public class BranchProfile
{
    public ushort Id { get; set; }
    public ulong SourceLocation { get; set; }
    public uint TrueCount { get; set; }
    public uint FalseCount { get; set; }

    /// <summary>
    /// Total times this branch was evaluated
    /// </summary>
    public uint TotalCount => TrueCount + FalseCount;

    /// <summary>
    /// Probability of taking the true branch (0.0 to 1.0)
    /// </summary>
    public double TrueProbability
    {
        get
        {
            if (TotalCount == 0) return 0.5; // Unknown, assume 50/50
            return (double)TrueCount / TotalCount;
        }
    }

    /// <summary>
    /// Is this branch predictable? (>90% bias one way)
    /// </summary>
    public bool IsPredictable(double threshold = 0.9)
    {
        double prob = TrueProbability;
        return prob >= threshold || prob <= (1.0 - threshold);
    }

    /// <summary>
    /// Which path is more likely?
    /// </summary>
    public bool TruePathIsLikely => TrueProbability >= 0.5;
}

public class LoopProfile
{
    public ushort Id { get; set; }
    public ulong SourceLocation { get; set; }
    public uint TotalIterations { get; set; }
    public uint ExecutionCount { get; set; }

    /// <summary>
    /// Average iterations per loop execution
    /// </summary>
    public double AverageIterations
    {
        get
        {
            if (ExecutionCount == 0) return 0;
            return (double)TotalIterations / ExecutionCount;
        }
    }

    /// <summary>
    /// Is this loop a candidate for unrolling?
    /// (small, predictable iteration count)
    /// </summary>
    public bool ShouldUnroll(double maxAvgIters = 8.0)
    {
        return ExecutionCount > 100 &&
               AverageIterations > 1.5 &&
               AverageIterations <= maxAvgIters;
    }
}

public class CallSiteProfile
{
    public ushort Id { get; set; }
    public string Caller { get; set; } = "";
    public string Callee { get; set; } = "";
    public uint CallCount { get; set; }

    /// <summary>
    /// Is this call site hot enough to inline?
    /// </summary>
    public bool ShouldInline(uint threshold = 1000) => CallCount >= threshold;
}
```

---

## 4. Profile-Guided Optimization Passes

### 4.1 PGO-Guided Function Inlining

```csharp
namespace Novus.Optimizer.Passes;

/// <summary>
/// Inline hot functions based on profile data
/// </summary>
public class ProfileGuidedInliningPass : IOptimizationPass
{
    private readonly ProfileData _profile;
    private readonly InliningPolicy _policy;

    public string Name => "Profile-Guided Inlining";

    public ProfileGuidedInliningPass(ProfileData profile, InliningPolicy? policy = null)
    {
        _profile = profile;
        _policy = policy ?? InliningPolicy.Default;
    }

    public bool Run(IrModule module)
    {
        bool changed = false;

        // Build call graph
        var callGraph = BuildCallGraph(module);

        // Identify hot call sites from profile
        var hotCallSites = _profile.CallSites.Values
            .Where(cs => cs.ShouldInline(_policy.MinCallCountThreshold))
            .OrderByDescending(cs => cs.CallCount)
            .ToList();

        foreach (var callSite in hotCallSites)
        {
            var caller = module.GetFunction(callSite.Caller);
            var callee = module.GetFunction(callSite.Callee);

            if (caller == null || callee == null) continue;
            if (callee.IsExtern) continue;

            // Check inlining profitability
            if (!ShouldInline(callee, callSite, _policy))
                continue;

            // Perform inlining
            changed |= InlineFunction(caller, callee, callSite);
        }

        return changed;
    }

    private bool ShouldInline(IrFunction callee, CallSiteProfile callSite, InliningPolicy policy)
    {
        // Size heuristic: don't inline large functions
        int calleeSize = EstimateFunctionSize(callee);
        if (calleeSize > policy.MaxInlineSize)
            return false;

        // Profile heuristic: inline if called frequently
        if (callSite.CallCount < policy.MinCallCountThreshold)
            return false;

        // Avoid inlining recursive functions
        if (IsRecursive(callee))
            return false;

        // Benefit estimation: call overhead vs. code size increase
        double benefit = EstimateInlineBenefit(callee, callSite);
        return benefit > policy.MinBenefitThreshold;
    }

    private double EstimateInlineBenefit(IrFunction callee, CallSiteProfile callSite)
    {
        // Benefit = (call overhead saved) × (call frequency)
        //         - (code size increase penalty)

        const int CALL_OVERHEAD_CYCLES = 20; // JSR + RTS on 68020
        const double CODE_SIZE_PENALTY = 0.1; // Per byte

        int calleeSize = EstimateFunctionSize(callee);
        int savedCycles = CALL_OVERHEAD_CYCLES * (int)callSite.CallCount;
        double sizePenalty = calleeSize * CODE_SIZE_PENALTY;

        return savedCycles - sizePenalty;
    }
}

public class InliningPolicy
{
    public int MaxInlineSize { get; set; } = 50; // Max IR instructions
    public uint MinCallCountThreshold { get; set; } = 1000;
    public double MinBenefitThreshold { get; set; } = 100.0;

    public static InliningPolicy Default => new InliningPolicy();

    public static InliningPolicy Aggressive => new InliningPolicy
    {
        MaxInlineSize = 100,
        MinCallCountThreshold = 100,
        MinBenefitThreshold = 10.0
    };
}
```

### 4.2 PGO-Guided Branch Prediction

```csharp
/// <summary>
/// Add branch prediction hints based on profile data
/// Generates __builtin_expect() or VBCC pragmas
/// </summary>
public class ProfileGuidedBranchPredictionPass : IOptimizationPass
{
    private readonly ProfileData _profile;

    public string Name => "Profile-Guided Branch Prediction";

    public ProfileGuidedBranchPredictionPass(ProfileData profile)
    {
        _profile = profile;
    }

    public bool Run(IrModule module)
    {
        bool changed = false;

        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrConditionalBranch condBranch)
                    {
                        // Find profile data for this branch
                        var branchId = GetBranchId(condBranch);
                        if (!_profile.Branches.TryGetValue(branchId, out var branchProfile))
                            continue;

                        // Only add hints for predictable branches
                        if (!branchProfile.IsPredictable(threshold: 0.85))
                            continue;

                        // Annotate branch with prediction hint
                        var hint = new BranchPredictionHint
                        {
                            ExpectedPath = branchProfile.TruePathIsLikely
                                ? BranchPath.True
                                : BranchPath.False,
                            Probability = branchProfile.TrueProbability
                        };

                        condBranch.PredictionHint = hint;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }
}

/// <summary>
/// Branch prediction hint attached to conditional branches
/// Used by C code generator to emit __builtin_expect() or pragmas
/// </summary>
public class BranchPredictionHint
{
    public BranchPath ExpectedPath { get; set; }
    public double Probability { get; set; }
}

public enum BranchPath
{
    True,
    False
}
```

**C Code Generation with Branch Hints:**

```c
// Without PGO:
if (condition) {
    // ...
} else {
    // ...
}

// With PGO (85% true probability):
if (__builtin_expect(condition, 1)) {  // Expect true
    // Hot path
} else {
    // Cold path
}

// Or using VBCC pragma:
#pragma vbcc_expect(1)  // Expect following branch to be true
if (condition) {
    // Hot path
} else {
    // Cold path
}
```

### 4.3 PGO-Guided Loop Unrolling

```csharp
/// <summary>
/// Unroll loops with predictable iteration counts based on profile data
/// </summary>
public class ProfileGuidedLoopUnrollingPass : IOptimizationPass
{
    private readonly ProfileData _profile;
    private readonly UnrollingPolicy _policy;

    public string Name => "Profile-Guided Loop Unrolling";

    public ProfileGuidedLoopUnrollingPass(ProfileData profile, UnrollingPolicy? policy = null)
    {
        _profile = profile;
        _policy = policy ?? UnrollingPolicy.Default;
    }

    public bool Run(IrModule module)
    {
        bool changed = false;

        // Detect loops in CFG
        foreach (var function in module.Functions)
        {
            var cfg = new ControlFlowGraph(function);
            var loopDetector = new LoopDetector(cfg);
            var loops = loopDetector.DetectLoops();

            foreach (var loop in loops)
            {
                // Find profile data for this loop
                var loopId = GetLoopId(loop);
                if (!_profile.Loops.TryGetValue(loopId, out var loopProfile))
                    continue;

                // Check if loop should be unrolled
                if (!loopProfile.ShouldUnroll(_policy.MaxAverageIterations))
                    continue;

                // Determine unroll factor
                int unrollFactor = CalculateUnrollFactor(loopProfile, _policy);

                // Perform loop unrolling
                changed |= UnrollLoop(loop, unrollFactor);
            }
        }

        return changed;
    }

    private int CalculateUnrollFactor(LoopProfile profile, UnrollingPolicy policy)
    {
        double avgIters = profile.AverageIterations;

        // Unroll by 2x if average is 2-4 iterations
        if (avgIters >= 2 && avgIters <= 4)
            return 2;

        // Unroll by 4x if average is 4-8 iterations
        if (avgIters > 4 && avgIters <= 8)
            return 4;

        // Fully unroll if average is constant and small
        if (avgIters == Math.Floor(avgIters) && avgIters <= policy.MaxFullUnroll)
            return (int)avgIters; // Full unroll

        // Default: no unrolling
        return 1;
    }
}

public class UnrollingPolicy
{
    public double MaxAverageIterations { get; set; } = 8.0;
    public int MaxFullUnroll { get; set; } = 4; // Fully unroll loops ≤ 4 iterations

    public static UnrollingPolicy Default => new UnrollingPolicy();
}
```

### 4.4 PGO-Guided Code Layout (Hot/Cold Splitting)

```csharp
/// <summary>
/// Split functions into hot and cold sections based on profile data
/// This improves instruction cache locality on 68040/68060
/// </summary>
public class ProfileGuidedCodeLayoutPass : IOptimizationPass
{
    private readonly ProfileData _profile;

    public string Name => "Profile-Guided Code Layout";

    public ProfileGuidedCodeLayoutPass(ProfileData profile)
    {
        _profile = profile;
    }

    public bool Run(IrModule module)
    {
        bool changed = false;

        // Separate hot and cold functions
        var hotFunctions = new List<IrFunction>();
        var coldFunctions = new List<IrFunction>();

        foreach (var function in module.Functions)
        {
            if (_profile.Functions.TryGetValue(function.Name, out var profile))
            {
                if (profile.IsHot(threshold: 1000))
                    hotFunctions.Add(function);
                else if (profile.IsCold(threshold: 10))
                    coldFunctions.Add(function);
            }
        }

        // Annotate functions with section hints for linker
        foreach (var hotFunc in hotFunctions)
        {
            hotFunc.Attributes ??= new AttributeCollection();
            hotFunc.Attributes.Add(new Attribute("section", "\"CODE_HOT\""));
            changed = true;
        }

        foreach (var coldFunc in coldFunctions)
        {
            coldFunc.Attributes ??= new AttributeCollection();
            coldFunc.Attributes.Add(new Attribute("section", "\"CODE_COLD\""));
            changed = true;
        }

        return changed;
    }
}
```

**Generated C Code:**

```c
// Hot function - place in CODE_HOT section for better cache locality
__attribute__((section("CODE_HOT")))
void render_frame(void) {
    // Frequently executed code
}

// Cold function - place in CODE_COLD section (evicted from cache)
__attribute__((section("CODE_COLD")))
void error_handler(void) {
    // Rarely executed code
}
```

### 4.5 PGO-Guided Dead Code Elimination

```csharp
/// <summary>
/// Eliminate code paths that are never executed according to profile data
/// </summary>
public class ProfileGuidedDeadCodeEliminationPass : IOptimizationPass
{
    private readonly ProfileData _profile;
    private readonly double _threshold;

    public string Name => "Profile-Guided Dead Code Elimination";

    public ProfileGuidedDeadCodeEliminationPass(ProfileData profile, double threshold = 0.001)
    {
        _profile = profile;
        _threshold = threshold; // Eliminate branches executed < 0.1% of the time
    }

    public bool Run(IrModule module)
    {
        bool changed = false;

        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrConditionalBranch condBranch)
                    {
                        var branchId = GetBranchId(condBranch);
                        if (!_profile.Branches.TryGetValue(branchId, out var branchProfile))
                            continue;

                        // If one path is almost never taken, eliminate it
                        if (branchProfile.TrueProbability < _threshold)
                        {
                            // True path is dead, convert to unconditional branch to false path
                            var uncondBranch = new IrBranch(condBranch.FalseTarget);
                            block.Instructions[block.Instructions.IndexOf(instruction)] = uncondBranch;
                            changed = true;
                        }
                        else if (branchProfile.TrueProbability > (1.0 - _threshold))
                        {
                            // False path is dead, convert to unconditional branch to true path
                            var uncondBranch = new IrBranch(condBranch.TrueTarget);
                            block.Instructions[block.Instructions.IndexOf(instruction)] = uncondBranch;
                            changed = true;
                        }
                    }
                }
            }
        }

        return changed;
    }
}
```

---

## 5. Integration with Build System

### 5.1 Command-Line Interface

```bash
# Phase 1: Build instrumented binary
novusc build --pgo-instrument -o program_instrumented.exe

# Run program to collect profile data
# (on Amiga or emulator)
program_instrumented.exe
# Creates novus_profile.pgo

# Phase 2: Build optimized binary with profile data
novusc build --pgo-use novus_profile.pgo -O 3 -o program_optimized.exe
```

### 5.2 Build Options

```csharp
public class BuildOptions
{
    // Existing options...

    [Option("pgo-instrument", Required = false,
        HelpText = "Build instrumented binary for profile data collection")]
    public bool PgoInstrument { get; set; }

    [Option("pgo-use", Required = false,
        HelpText = "Path to profile data file (.pgo) for profile-guided optimization")]
    public string? PgoProfilePath { get; set; }

    [Option("pgo-policy", Required = false,
        HelpText = "PGO policy: conservative, balanced, aggressive (default: balanced)")]
    public string PgoPolicy { get; set; } = "balanced";
}
```

### 5.3 Compilation Pipeline Integration

```csharp
// In BuildCommand.cs

if (options.PgoInstrument)
{
    // Phase 1: Instrumented build
    Console.WriteLine("Building instrumented binary for profile collection...");

    // Add instrumentation pass BEFORE optimization
    var instrumentationPass = new InstrumentationPass();
    irModule = instrumentationPass.Instrument(irModule);

    // Use light optimization for instrumented build
    var pipeline = OptimizationPipeline.CreatePipeline(level: 1, verbose: options.Verbose);
    pipeline.Run(irModule);

    // Link with libprofile.a
    linkerArgs.Add("-lprofile");
}
else if (!string.IsNullOrEmpty(options.PgoProfilePath))
{
    // Phase 2: Optimized build with profile data
    Console.WriteLine($"Using profile data from {options.PgoProfilePath}");

    // Load profile data
    var profileData = ProfileData.Load(options.PgoProfilePath);

    // Verify source hash
    if (!profileData.VerifySourceHash(irModule))
    {
        Console.WriteLine("WARNING: Profile data source hash mismatch!");
        Console.WriteLine("Profile data may be stale. Consider re-profiling.");
    }

    // Apply PGO passes BEFORE standard optimization
    var pgoPolicy = ParsePgoPolicy(options.PgoPolicy);
    var pgoPipeline = CreatePgoPipeline(profileData, pgoPolicy);
    pgoPipeline.Run(irModule);

    // Apply standard aggressive optimization
    var optPipeline = OptimizationPipeline.CreatePipeline(level: 3, verbose: options.Verbose);
    optPipeline.Run(irModule);
}
else
{
    // Standard build without PGO
    var pipeline = OptimizationPipeline.CreatePipeline(
        level: options.OptimizationLevel ?? 2,
        verbose: options.Verbose
    );
    pipeline.Run(irModule);
}

private static OptimizationPipeline CreatePgoPipeline(ProfileData profile, PgoPolicy policy)
{
    var pipeline = new OptimizationPipeline(verbose: true);

    // Order matters: inlining first exposes more optimization opportunities
    pipeline.AddPass(new ProfileGuidedInliningPass(profile, policy.InliningPolicy));
    pipeline.AddPass(new ProfileGuidedLoopUnrollingPass(profile, policy.UnrollingPolicy));
    pipeline.AddPass(new ProfileGuidedBranchPredictionPass(profile));
    pipeline.AddPass(new ProfileGuidedCodeLayoutPass(profile));
    pipeline.AddPass(new ProfileGuidedDeadCodeEliminationPass(profile, policy.DeadCodeThreshold));

    return pipeline;
}
```

---

## 6. VBCC Integration

### 6.1 Passing Hints to VBCC

VBCC doesn't natively support `__builtin_expect()`, but we can use pragmas and attributes:

**Branch Prediction:**
```c
// Use inline assembly for likely branches (68020+ branch prediction)
#define LIKELY(x) __builtin_expect(!!(x), 1)
#define UNLIKELY(x) __builtin_expect(!!(x), 0)

// Fallback for VBCC: manual code layout
if (hot_condition) {
    // Hot path inline
    hot_code();
} else {
    goto cold_path;  // Jump to cold code at end of function
}
return;

cold_path:
    cold_code();
    return;
```

**Function Attributes:**
```c
// Hot functions: encourage inlining
static inline __attribute__((always_inline)) void hot_function() {
    // ...
}

// Cold functions: mark for separate section
__attribute__((section("CODE_COLD"), noinline)) void cold_function() {
    // ...
}
```

**Loop Unrolling:**
```c
// Manual loop unrolling
// Before:
for (int i = 0; i < n; i++) {
    body(i);
}

// After (unroll by 4):
int i;
for (i = 0; i < n - 3; i += 4) {
    body(i);
    body(i + 1);
    body(i + 2);
    body(i + 3);
}
for (; i < n; i++) {
    body(i);  // Remainder iterations
}
```

### 6.2 VBCC Linker Script for Hot/Cold Sections

```ld
/* vlink script for hot/cold code layout */

SECTIONS
{
    . = 0;

    /* Hot code first (better cache locality) */
    .text_hot : {
        *(.CODE_HOT)
    }

    /* Regular code */
    .text : {
        *(.text)
    }

    /* Cold code last (low priority for cache) */
    .text_cold : {
        *(.CODE_COLD)
    }

    /* Data sections */
    .data : { *(.data) }
    .bss : { *(.bss) }
}
```

---

## 7. Example Workflow

### 7.1 Sample Program

```novus
// example.novus
fn main() -> i32 {
    let mut sum: i32 = 0

    // Hot loop (profiled: avg 1000 iterations)
    for i in 0..1000 {
        sum = sum + compute(i)
    }

    // Cold branch (profiled: 1% probability)
    if sum > 999999 {
        error_handler()
    }

    return sum
}

fn compute(x: i32) -> i32 {
    // Hot function (called 1000 times per main() call)
    return x * 2 + 1
}

fn error_handler() {
    // Cold function (rarely called)
    panic("Sum overflow!")
}
```

### 7.2 Build Process

**Step 1: Instrumented Build**
```bash
$ novusc build example.novus --pgo-instrument -o example_instr

Compiling example.novus...
Instrumenting for profile data collection...
  + 3 function counters
  + 2 branch counters
  + 1 loop counter
  + 1 call site counter
Generating C code...
Compiling with VBCC...
Linking with libprofile.a...
Done! Run example_instr to collect profile data.
```

**Step 2: Profile Collection**
```bash
$ fs-uae  # Or real Amiga
# Run example_instr
$ example_instr
Profile data saved to novus_profile.pgo
```

**Step 3: Optimized Build**
```bash
$ novusc build example.novus --pgo-use novus_profile.pgo -O 3 -o example

Compiling example.novus...
Loading profile data from novus_profile.pgo...
  ✓ Source hash verified
  ✓ Function profiles: 3
  ✓ Branch profiles: 2
  ✓ Loop profiles: 1
  ✓ Call site profiles: 1
Applying PGO optimizations...
  [PGO] Inlining hot function: compute (called 1000 times)
  [PGO] Unrolling loop in main (avg 1000 iterations, factor 4)
  [PGO] Branch prediction: sum > 999999 -> UNLIKELY (1% probability)
  [PGO] Code layout: main -> CODE_HOT, error_handler -> CODE_COLD
Applying standard optimizations (level 3)...
  [Iteration 1] Constant Folding...
  [Iteration 1] Dead Code Elimination...
Optimization converged after 2 iterations
Generating C code...
Compiling with VBCC...
Done!
```

**Step 4: Compare Results**
```bash
$ ls -lh
-rwxr-xr-x  1 user  staff   24K  example_instr   # Instrumented
-rwxr-xr-x  1 user  staff   16K  example         # Optimized with PGO

# PGO version is 33% smaller due to:
# - compute() inlined (no call overhead)
# - Loop unrolled (reduced branch overhead)
# - error_handler() moved to cold section (better cache usage)
```

---

## 8. Implementation Plan

### Phase 1: Foundation (Week 1-2)
- [ ] Define ProfileData C# classes
- [ ] Implement .pgo file format reader/writer
- [ ] Create InstrumentationPass skeleton
- [ ] Add --pgo-instrument and --pgo-use CLI flags

### Phase 2: Instrumentation (Week 3-4)
- [ ] Implement function entry counter insertion
- [ ] Implement branch counter insertion
- [ ] Implement loop counter insertion
- [ ] Implement call site counter insertion
- [ ] Create libprofile.a runtime library (C)
- [ ] Test instrumentation on simple programs

### Phase 3: Profile Collection (Week 5)
- [ ] Implement profile data dump on exit
- [ ] Test on FS-UAE emulator
- [ ] Test on real Amiga hardware
- [ ] Validate .pgo file format

### Phase 4: PGO Passes (Week 6-8)
- [ ] ProfileGuidedInliningPass
- [ ] ProfileGuidedBranchPredictionPass
- [ ] ProfileGuidedLoopUnrollingPass
- [ ] ProfileGuidedCodeLayoutPass
- [ ] ProfileGuidedDeadCodeEliminationPass

### Phase 5: Code Generation (Week 9)
- [ ] Emit __builtin_expect() for branch hints
- [ ] Emit section attributes for hot/cold code
- [ ] Emit unrolled loops
- [ ] Emit inlined functions

### Phase 6: Testing & Validation (Week 10-12)
- [ ] Unit tests for each PGO pass
- [ ] Integration tests with real programs
- [ ] Performance benchmarks (before/after PGO)
- [ ] Documentation and examples

---

## 9. Performance Expectations

Based on industry PGO implementations (GCC, LLVM, MSVC), we expect:

| Metric | Improvement Range |
|--------|-------------------|
| **Code Size** | 10-30% reduction (due to inlining, dead code elimination) |
| **Execution Speed** | 5-20% faster (better branch prediction, cache locality) |
| **Cache Misses** | 20-40% reduction (hot/cold splitting) |
| **Function Call Overhead** | 30-50% reduction (hot path inlining) |

**68k-Specific Benefits:**
- **68000/010:** Minimal benefit (no cache, no branch prediction)
- **68020/030:** Moderate benefit (instruction cache, some branch prediction)
- **68040/060:** Significant benefit (split I/D cache, better branch prediction)

---

## 10. Future Enhancements

### 10.1 Multi-Run Profile Merging
```bash
# Collect profiles from multiple runs
$ example_instr scenario1  # -> novus_profile_1.pgo
$ example_instr scenario2  # -> novus_profile_2.pgo

# Merge profiles
$ novusc pgo-merge novus_profile_*.pgo -o merged.pgo

# Build with merged profile
$ novusc build --pgo-use merged.pgo
```

### 10.2 Iterative PGO (AutoFDO)
```bash
# Automatically iterate: build -> profile -> rebuild
$ novusc build --pgo-auto -o optimized
  (1) Building instrumented...
  (2) Running and collecting profile...
  (3) Rebuilding with profile...
  Done!
```

### 10.3 Continuous PGO (Production Feedback)
- Collect profile data from real user runs
- Aggregate profiles from fleet of Amigas
- Rebuild with production workload profiles

### 10.4 PGO for Copper/Blitter
- Profile Copper list execution frequencies
- Profile Blitter operation frequencies
- Optimize hardware resource allocation based on usage patterns

---

## 11. Testing Strategy

### 11.1 Unit Tests
```csharp
[Fact]
public void ProfileData_LoadsCorrectly()
{
    var profile = ProfileData.Load("testdata/sample.pgo");
    Assert.Equal(5, profile.Functions.Count);
    Assert.Equal(10, profile.Branches.Count);
}

[Fact]
public void InliningPass_InlinesHotFunctions()
{
    var profile = CreateTestProfile();
    var pass = new ProfileGuidedInliningPass(profile);
    var module = CreateTestModule();

    bool changed = pass.Run(module);

    Assert.True(changed);
    Assert.Contains(module.Functions, f => f.Name == "main_with_inlined_compute");
}

[Fact]
public void BranchPredictionPass_AddsHintsForPredictableBranches()
{
    var profile = CreateTestProfile();
    var pass = new ProfileGuidedBranchPredictionPass(profile);
    var module = CreateTestModule();

    pass.Run(module);

    var branch = FindConditionalBranch(module, "main");
    Assert.NotNull(branch.PredictionHint);
    Assert.Equal(BranchPath.False, branch.PredictionHint.ExpectedPath);
}
```

### 11.2 Integration Tests
```bash
# Test full PGO workflow
$ dotnet test --filter "PGOIntegrationTests"
  ✓ InstrumentedBuildGeneratesProfile
  ✓ ProfileGuidedBuildUsesProfile
  ✓ PGOReducesCodeSize
  ✓ PGOImprovesPerformance
```

### 11.3 Benchmarks
```novus
// benchmark.novus
fn fibonacci(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}

fn main() -> i32 {
    let mut sum = 0
    for i in 0..20 {
        sum = sum + fibonacci(i)
    }
    return sum
}
```

**Measure:**
- Build time (instrumented vs. optimized)
- Binary size
- Execution time (on FS-UAE cycle-accurate mode)
- Cache hit rates (via UAE profiler)

---

## Conclusion

This PGO infrastructure will enable Novus to generate highly optimized code for Amiga 68k systems by leveraging real-world runtime behavior. The design is:

- **Modular:** Each PGO pass is independent and testable
- **Amiga-aware:** Memory-efficient instrumentation, VBCC integration
- **Practical:** Simple .pgo file format, easy CLI workflow
- **Extensible:** Can add more passes (auto-vectorization, etc.)

**Key Design Decisions:**
1. **Lightweight instrumentation** (~8KB overhead) suitable for Amiga RAM constraints
2. **Simple binary format** for fast profile I/O on slow Amiga filesystems
3. **Modular passes** that integrate cleanly with existing optimizer
4. **VBCC compatibility** using attributes and manual transformations
5. **Source hash verification** to prevent stale profile data usage

**Next Steps:**
1. Implement ProfileData classes and .pgo file format
2. Create InstrumentationPass
3. Build libprofile.a runtime
4. Implement first PGO pass (function inlining)
5. Test on FS-UAE

---

**Files to Create:**
- `Novus/Optimizer/ProfileData.cs` - Profile data structures
- `Novus/Optimizer/Passes/InstrumentationPass.cs` - Instrumentation
- `Novus/Optimizer/Passes/ProfileGuidedInliningPass.cs` - PGO inlining
- `Novus/Optimizer/Passes/ProfileGuidedBranchPredictionPass.cs` - PGO branch prediction
- `Novus/Optimizer/Passes/ProfileGuidedLoopUnrollingPass.cs` - PGO loop unrolling
- `Novus/Optimizer/Passes/ProfileGuidedCodeLayoutPass.cs` - PGO code layout
- `Novus/Optimizer/Passes/ProfileGuidedDeadCodeEliminationPass.cs` - PGO DCE
- `Novus.Runtime/profile/novus_runtime_profile.c` - Profile collection runtime
- `Novus.Tests/PGOTests.cs` - PGO unit tests
- `docs/PGO_USER_GUIDE.md` - User documentation

**Estimated Implementation Time:** 10-12 weeks (part-time)

**Dependencies:**
- Existing IR infrastructure
- Existing optimization pipeline
- VBCC toolchain
- FS-UAE or real Amiga for testing
