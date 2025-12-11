// Novus Runtime - Raw Memory Allocation
// Minimal allocation functions with NO dependencies on error handling
// These call AllocMem/FreeMem directly without any tracking or error display

#include "novus_runtime.h"

// ============================================================================
// Raw Memory Allocation (bypasses tracking)
// ============================================================================
// These functions call AllocMem/FreeMem directly without going through the
// memory tracking system. Used for closure environments, stdlib internals,
// and any code that needs precise control over memory.
//
// IMPORTANT: This file must have NO references to error handling functions
// (display_error_requester, display_error_safe, etc.) to ensure that
// programs only using allocation don't pull in Intuition library.

/**
 * Allocate memory directly (bypasses memory tracking)
 * Use this for stdlib internals where tracking would interfere.
 */
void* __novus_alloc_raw(uint32_t size, uint32_t flags) {
    return AllocMem(size, flags);
}

/**
 * Free memory directly (bypasses memory tracking)
 * Use this to free memory allocated with __novus_alloc_raw.
 */
void __novus_free_raw(void* ptr, uint32_t size) {
    if (ptr != NULL) {
        FreeMem(ptr, size);
    }
}
