// Novus Runtime - Memory Debug Tracking
// Memory leak, double-free, buffer overflow, and use-after-free detection

#include "novus_runtime.h"

// ============================================================================
// Memory Debug Tracking
// ============================================================================
//
// This section implements memory debugging features for detecting:
// - Memory leaks (allocations not freed before program exit)
// - Double-free errors (freeing the same memory twice)
// - Buffer overflows (writing past allocation boundaries) [Paranoid level]
// - Use-after-free (accessing freed memory) [Paranoid level]
//
// These functions are always compiled into the runtime, but are only called
// when the compiler generates code with memory tracking enabled (SafetyLevel >= Full).
// In release builds, the compiler emits direct AllocMem/FreeMem calls instead.
// ============================================================================

// Allocation record - stored in a linked list
typedef struct AllocationRecord {
    void* ptr;                      // Allocated pointer (user-visible)
    void* actual_ptr;               // Actual allocated pointer (may include guard bytes)
    uint32_t size;                  // Requested size
    uint32_t actual_size;           // Actual allocated size (with guards if paranoid)
    const char* file;               // Source file
    int32_t line;                   // Line number
    uint32_t sequence;              // Allocation sequence number
    uint8_t freed;                  // 0 = active, 1 = freed (for double-free detection)
    const char* free_file;          // File where freed (for double-free reporting)
    int32_t free_line;              // Line where freed
    struct AllocationRecord* next;  // Next in list
} AllocationRecord;

// Global tracking state
static AllocationRecord* _alloc_list_head = NULL;
static uint32_t _alloc_sequence = 0;
static uint32_t _total_allocated = 0;
static uint32_t _total_freed = 0;

// Guard byte patterns (for Paranoid level)
#define GUARD_SIZE 4
#define GUARD_PATTERN_START 0xDEADC0DE
#define GUARD_PATTERN_END   0xBEEFCAFE
#define POISON_BYTE         0xDE        // Pattern for poisoned memory

// Helper to convert pointer to hex string
static void ptr_to_hex(char* buf, void* ptr) {
    uint32_t val = (uint32_t)ptr;
    int i;
    buf[0] = '0';
    buf[1] = 'x';
    for (i = 0; i < 8; i++) {
        int digit = (val >> (28 - i * 4)) & 0xF;
        buf[2 + i] = (digit < 10) ? ('0' + digit) : ('A' + digit - 10);
    }
    buf[10] = '\0';
}

/**
 * Tracked memory allocation
 * Wraps AllocMem and records the allocation for leak/error detection.
 *
 * @param size Requested allocation size
 * @param flags AmigaOS memory flags (MEMF_PUBLIC, MEMF_CHIP, etc.)
 * @param file Source file where allocation occurred
 * @param line Line number
 * @return Allocated pointer or NULL on failure
 */
void* __novus_tracked_alloc(uint32_t size, uint32_t flags, const char* file, int32_t line)
{
    uint32_t actual_size = size;
    void* actual_ptr;
    void* user_ptr;

    // Add space for guard bytes: 4 bytes before + 4 bytes after
    // Guard bytes help detect buffer overflows when memory is freed
    actual_size = size + (GUARD_SIZE * 2);

    // Allocate the memory
    actual_ptr = AllocMem(actual_size, flags);
    if (actual_ptr == NULL) {
        return NULL;
    }

    // Write guard patterns
    *(uint32_t*)actual_ptr = GUARD_PATTERN_START;
    user_ptr = (uint8_t*)actual_ptr + GUARD_SIZE;
    *(uint32_t*)((uint8_t*)user_ptr + size) = GUARD_PATTERN_END;

    // Create allocation record (allocate from public memory to avoid affecting user's chip mem)
    AllocationRecord* record = (AllocationRecord*)AllocMem(sizeof(AllocationRecord), MEMF_PUBLIC | MEMF_CLEAR);
    if (record == NULL) {
        // Can't track - free and return failure
        FreeMem(actual_ptr, actual_size);
        return NULL;
    }

    record->ptr = user_ptr;
    record->actual_ptr = actual_ptr;
    record->size = size;
    record->actual_size = actual_size;
    record->file = file;
    record->line = line;
    record->sequence = _alloc_sequence++;
    record->freed = 0;
    record->free_file = NULL;
    record->free_line = 0;
    record->next = _alloc_list_head;
    _alloc_list_head = record;

    _total_allocated += size;

    return user_ptr;
}

/**
 * Tracked memory free with double-free detection
 * Wraps FreeMem and checks for errors.
 *
 * @param ptr Pointer to free
 * @param size Size that was allocated
 * @param file Source file where free occurred
 * @param line Line number
 */
void __novus_tracked_free(void* ptr, uint32_t size, const char* file, int32_t line)
{
    if (ptr == NULL) {
        return;  // Freeing NULL is a no-op
    }

    // Search for allocation record
    AllocationRecord* record = _alloc_list_head;

    while (record != NULL) {
        if (record->ptr == ptr) {
            // Found the record
            if (record->freed) {
                // DOUBLE FREE DETECTED!
                char line_str[12];
                char orig_line_str[12];
                char free_line_str[12];
                char ptr_str[12];
                char* msg = error_buffer;

                int_to_str(line_str, line);
                int_to_str(orig_line_str, record->line);
                int_to_str(free_line_str, record->free_line);
                ptr_to_hex(ptr_str, ptr);

                msg = strcpy_helper(msg, "DOUBLE FREE DETECTED!\n\n");
                msg = strcpy_helper(msg, "Pointer: ");
                msg = strcpy_helper(msg, ptr_str);
                msg = strcpy_helper(msg, "\n\nAttempted free at:\n  ");
                msg = strcpy_helper(msg, file ? file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, line_str);
                msg = strcpy_helper(msg, "\n\nOriginally allocated at:\n  ");
                msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, orig_line_str);
                msg = strcpy_helper(msg, "\n\nFirst freed at:\n  ");
                msg = strcpy_helper(msg, record->free_file ? record->free_file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, free_line_str);

                display_error_requester(AO_DoubleFree);
                return;  // Don't actually free - it's already freed
            }

            // Check guard bytes for buffer overflow
            {
                uint32_t* start_guard = (uint32_t*)record->actual_ptr;
                uint32_t* end_guard = (uint32_t*)((uint8_t*)record->ptr + record->size);

                if (*start_guard != GUARD_PATTERN_START || *end_guard != GUARD_PATTERN_END) {
                    // BUFFER OVERFLOW DETECTED!
                    char ov_line_str[12];
                    char ov_orig_line_str[12];
                    char ov_ptr_str[12];
                    char* ov_msg = error_buffer;

                    int_to_str(ov_line_str, line);
                    int_to_str(ov_orig_line_str, record->line);
                    ptr_to_hex(ov_ptr_str, ptr);

                    ov_msg = strcpy_helper(ov_msg, "BUFFER OVERFLOW DETECTED!\n\n");
                    ov_msg = strcpy_helper(ov_msg, "Pointer: ");
                    ov_msg = strcpy_helper(ov_msg, ov_ptr_str);
                    if (*start_guard != GUARD_PATTERN_START) {
                        ov_msg = strcpy_helper(ov_msg, "\n\nUnderflow: Start guard corrupted");
                    }
                    if (*end_guard != GUARD_PATTERN_END) {
                        ov_msg = strcpy_helper(ov_msg, "\n\nOverflow: End guard corrupted");
                    }
                    ov_msg = strcpy_helper(ov_msg, "\n\nAllocated at:\n  ");
                    ov_msg = strcpy_helper(ov_msg, record->file ? record->file : "<unknown>");
                    ov_msg = strcpy_helper(ov_msg, ":");
                    ov_msg = strcpy_helper(ov_msg, ov_orig_line_str);
                    ov_msg = strcpy_helper(ov_msg, "\n\nFreed at:\n  ");
                    ov_msg = strcpy_helper(ov_msg, file ? file : "<unknown>");
                    ov_msg = strcpy_helper(ov_msg, ":");
                    ov_msg = strcpy_helper(ov_msg, ov_line_str);

                    display_error_requester(AO_BufferOverflow);
                    // Continue to free the memory anyway
                }
            }

            // Poison the user memory to detect use-after-free
            // Fill with 0xDE pattern - reads after free will get garbage
            __novus_memset(record->ptr, POISON_BYTE, record->size);

            // Mark as freed and record where
            record->freed = 1;
            record->free_file = file;
            record->free_line = line;
            _total_freed += record->size;

            // USE-AFTER-FREE DETECTION STRATEGY:
            // We do NOT free the memory here. Instead we keep it allocated
            // but poisoned. This allows us to:
            // 1. Detect when the program reads from freed memory (gets poison)
            // 2. Detect when the program writes to freed memory (overwrites poison)
            // 3. Most importantly: detect when they try to FREE it again
            //    (we catch this as use-after-free, not just double-free)
            //
            // The memory will be properly freed in __novus_memory_report() at exit.
            // This trades memory for better debugging - acceptable in debug builds.
            //
            // With MMU (future): Mark pages as MAPP_INVALID for immediate detection

            // DON'T free: FreeMem(record->actual_ptr, record->actual_size);
            return;
        }
        record = record->next;
    }

    // Pointer not found in tracking list - wasn't allocated via tracked_alloc
    // This could be corruption or a pointer allocated before tracking started
    // Just free it normally to avoid a leak
    FreeMem(ptr, size);
}

/**
 * Memory report - called on program exit to report leaks
 * Shows all allocations that were not freed.
 */
void __novus_memory_report(void)
{
    AllocationRecord* record;
    uint32_t leak_count = 0;
    uint32_t leak_bytes = 0;
    int shown = 0;
    char num_buf[12];
    char* msg;

    // Count active (unfreed) allocations
    record = _alloc_list_head;
    while (record != NULL) {
        if (!record->freed) {
            leak_count++;
            leak_bytes += record->size;
        }
        record = record->next;
    }

    if (leak_count == 0) {
        // All memory was freed - clean up tracking records and return silently
        goto cleanup;
    }

    // Build leak report
    msg = error_buffer;
    msg = strcpy_helper(msg, "MEMORY LEAK DETECTED!\n\n");

    int_to_str(num_buf, leak_count);
    msg = strcpy_helper(msg, num_buf);
    msg = strcpy_helper(msg, " allocation(s) not freed\n");
    msg = strcpy_helper(msg, "Total leaked: ");
    int_to_str(num_buf, leak_bytes);
    msg = strcpy_helper(msg, num_buf);
    msg = strcpy_helper(msg, " bytes\n\n");

    // Show first few leaks with locations
    record = _alloc_list_head;
    while (record != NULL && shown < 5) {
        if (!record->freed) {
            msg = strcpy_helper(msg, "* ");
            int_to_str(num_buf, record->size);
            msg = strcpy_helper(msg, num_buf);
            msg = strcpy_helper(msg, " bytes at ");
            msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
            msg = strcpy_helper(msg, ":");
            int_to_str(num_buf, record->line);
            msg = strcpy_helper(msg, num_buf);
            msg = strcpy_helper(msg, "\n");
            shown++;
        }
        record = record->next;
    }

    if (leak_count > 5) {
        msg = strcpy_helper(msg, "... and ");
        int_to_str(num_buf, leak_count - 5);
        msg = strcpy_helper(msg, num_buf);
        msg = strcpy_helper(msg, " more\n");
    }

    display_error_requester(AO_MemoryLeak);

cleanup:
    // Clean up all tracking records and free the memory we held onto
    // (We don't free during normal operation to enable UAF detection)
    record = _alloc_list_head;
    while (record != NULL) {
        AllocationRecord* next = record->next;

        // If the memory was "freed" by the user, we still have it allocated
        // Now actually release it back to the OS
        if (record->freed && record->actual_ptr != NULL) {
            FreeMem(record->actual_ptr, record->actual_size);
        }
        // If the memory was never freed (leak), also free it to avoid OS-level leak
        else if (!record->freed && record->actual_ptr != NULL) {
            FreeMem(record->actual_ptr, record->actual_size);
        }

        // Free the tracking record itself
        FreeMem(record, sizeof(AllocationRecord));
        record = next;
    }
    _alloc_list_head = NULL;
}

/**
 * Check if a pointer access is a use-after-free
 * Called by generated code when dereferencing pointers in paranoid mode
 * Returns the allocation record if UAF detected, NULL otherwise
 */
static AllocationRecord* __check_uaf(void* ptr)
{
    AllocationRecord* record = _alloc_list_head;

    while (record != NULL) {
        // Check if ptr falls within this allocation's range
        uint8_t* start = (uint8_t*)record->ptr;
        uint8_t* end = start + record->size;

        if ((uint8_t*)ptr >= start && (uint8_t*)ptr < end) {
            // Pointer is within this allocation
            if (record->freed) {
                // USE AFTER FREE!
                return record;
            }
            // Valid access to live allocation
            return NULL;
        }
        record = record->next;
    }

    // Pointer not in any tracked allocation - could be stack, global, etc.
    return NULL;
}

/**
 * Report use-after-free error
 * Shows dialog with allocation/free information
 */
static void __report_uaf(AllocationRecord* record, void* access_ptr, const char* file, int32_t line)
{
    char line_str[12];
    char alloc_line_str[12];
    char free_line_str[12];
    char ptr_str[12];
    char access_ptr_str[12];
    char* msg = error_buffer;

    int_to_str(line_str, line);
    int_to_str(alloc_line_str, record->line);
    int_to_str(free_line_str, record->free_line);
    ptr_to_hex(ptr_str, record->ptr);
    ptr_to_hex(access_ptr_str, access_ptr);

    msg = strcpy_helper(msg, "USE AFTER FREE DETECTED!\n\n");
    msg = strcpy_helper(msg, "Access at: ");
    msg = strcpy_helper(msg, access_ptr_str);
    msg = strcpy_helper(msg, "\nBlock: ");
    msg = strcpy_helper(msg, ptr_str);
    msg = strcpy_helper(msg, "\n\nAccessed at:\n  ");
    msg = strcpy_helper(msg, file ? file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, line_str);
    msg = strcpy_helper(msg, "\n\nOriginally allocated at:\n  ");
    msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, alloc_line_str);
    msg = strcpy_helper(msg, "\n\nFreed at:\n  ");
    msg = strcpy_helper(msg, record->free_file ? record->free_file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, free_line_str);

    display_error_requester(AO_UseAfterFree);
}

/**
 * Check pointer access and report use-after-free if detected
 * Called by generated code before dereferencing tracked pointers
 *
 * @param ptr The pointer being accessed
 * @param file Source file where access occurred
 * @param line Line number
 */
void __novus_check_ptr_access(void* ptr, const char* file, int32_t line)
{
    AllocationRecord* record = __check_uaf(ptr);
    if (record != NULL) {
        __report_uaf(record, ptr, file, line);
    }
}
