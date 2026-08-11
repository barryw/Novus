// Novus Runtime - VBCC comparison sequence points

#include "novus_runtime.h"

NOVUS_RUNTIME_SECTION(__novus_is_null) int32_t __novus_is_null(void* ptr)
{
    return ptr == NULL ? 1 : 0;
}

NOVUS_RUNTIME_SECTION(__novus_cmp_eq_i32) int32_t __novus_cmp_eq_i32(int32_t a, int32_t b)
{
    return a == b ? 1 : 0;
}

NOVUS_RUNTIME_SECTION(__novus_cmp_ne_i32) int32_t __novus_cmp_ne_i32(int32_t a, int32_t b)
{
    return a != b ? 1 : 0;
}

NOVUS_RUNTIME_SECTION(__novus_cmp_eq_i64) int32_t __novus_cmp_eq_i64(int64_t a, int64_t b) { return a == b ? 1 : 0; }
NOVUS_RUNTIME_SECTION(__novus_cmp_ne_i64) int32_t __novus_cmp_ne_i64(int64_t a, int64_t b) { return a != b ? 1 : 0; }
NOVUS_RUNTIME_SECTION(__novus_cmp_eq_u64) int32_t __novus_cmp_eq_u64(uint64_t a, uint64_t b) { return a == b ? 1 : 0; }
NOVUS_RUNTIME_SECTION(__novus_cmp_ne_u64) int32_t __novus_cmp_ne_u64(uint64_t a, uint64_t b) { return a != b ? 1 : 0; }
