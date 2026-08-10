#include "novus_runtime.h"

float __novus_f32_from_bits(uint32_t bits)
{
    union { uint32_t bits; float value; } converted;
    converted.bits = bits;
    return converted.value;
}

double __novus_f64_from_bits(uint64_t bits)
{
    union { uint64_t bits; double value; } converted;
    converted.bits = bits;
    return converted.value;
}

int32_t __novus_cmp_eq_f32(float a, float b) { return a == b ? 1 : 0; }
int32_t __novus_cmp_ne_f32(float a, float b) { return a != b ? 1 : 0; }
int32_t __novus_cmp_eq_f64(double a, double b) { return a == b ? 1 : 0; }
int32_t __novus_cmp_ne_f64(double a, double b) { return a != b ? 1 : 0; }
