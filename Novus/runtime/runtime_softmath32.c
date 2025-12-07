/**
 * Novus Runtime - 32-bit Soft Math for 68000
 *
 * The 68000 only has 16x16->32 multiply and 32/16->16:16 divide.
 * This module provides 32-bit multiply, divide, and modulo using
 * the standard GCC libgcc naming convention (__mulsi3, __divsi3, etc.)
 *
 * These functions follow the Amiga/68k ABI:
 *   - Arguments in d0, d1
 *   - Result in d0
 *   - d2-d7 and a2-a6 must be preserved
 */

#include "novus_runtime.h"

/**
 * 32-bit unsigned multiply: d0 * d1 -> d0
 *
 * Algorithm: Split into 16-bit parts and combine partial products
 *   (a*2^16 + b) * (c*2^16 + d) = ac*2^32 + (ad + bc)*2^16 + bd
 *   Since we only need low 32 bits: (ad + bc)*2^16 + bd
 */
uint32_t __mulsi3(uint32_t a, uint32_t b)
{
    uint16_t a_hi = (uint16_t)(a >> 16);
    uint16_t a_lo = (uint16_t)a;
    uint16_t b_hi = (uint16_t)(b >> 16);
    uint16_t b_lo = (uint16_t)b;

    // bd - low * low (32-bit result)
    uint32_t result = (uint32_t)a_lo * (uint32_t)b_lo;

    // ad + bc - cross products (only low 16 bits matter for final result)
    result += ((uint32_t)a_lo * (uint32_t)b_hi) << 16;
    result += ((uint32_t)a_hi * (uint32_t)b_lo) << 16;

    return result;
}

/**
 * 32-bit unsigned divide: d0 / d1 -> d0
 *
 * Algorithm: Binary long division (shift and subtract)
 */
uint32_t __udivsi3(uint32_t dividend, uint32_t divisor)
{
    uint32_t quotient = 0;
    uint32_t remainder = 0;
    int i;

    if (divisor == 0) {
        // Division by zero - return max value (undefined behavior)
        return 0xFFFFFFFF;
    }

    // Binary long division
    for (i = 31; i >= 0; i--) {
        remainder <<= 1;
        remainder |= (dividend >> i) & 1;

        if (remainder >= divisor) {
            remainder -= divisor;
            quotient |= (1UL << i);
        }
    }

    return quotient;
}

/**
 * 32-bit signed divide: d0 / d1 -> d0
 *
 * Handles sign, then calls unsigned divide
 */
int32_t __divsi3(int32_t dividend, int32_t divisor)
{
    int negative = 0;
    uint32_t udividend, udivisor, result;

    if (dividend < 0) {
        negative = !negative;
        udividend = (uint32_t)(-dividend);
    } else {
        udividend = (uint32_t)dividend;
    }

    if (divisor < 0) {
        negative = !negative;
        udivisor = (uint32_t)(-divisor);
    } else {
        udivisor = (uint32_t)divisor;
    }

    result = __udivsi3(udividend, udivisor);

    return negative ? -(int32_t)result : (int32_t)result;
}

/**
 * 32-bit unsigned modulo: d0 % d1 -> d0
 *
 * Algorithm: Binary long division, return remainder
 */
uint32_t __umodsi3(uint32_t dividend, uint32_t divisor)
{
    uint32_t remainder = 0;
    int i;

    if (divisor == 0) {
        // Division by zero - return 0 (undefined behavior)
        return 0;
    }

    // Binary long division - we only need the remainder
    for (i = 31; i >= 0; i--) {
        remainder <<= 1;
        remainder |= (dividend >> i) & 1;

        if (remainder >= divisor) {
            remainder -= divisor;
        }
    }

    return remainder;
}

/**
 * 32-bit signed modulo: d0 % d1 -> d0
 *
 * Sign of result matches sign of dividend (C99 behavior)
 */
int32_t __modsi3(int32_t dividend, int32_t divisor)
{
    int negative = 0;
    uint32_t udividend, udivisor, result;

    if (dividend < 0) {
        negative = 1;
        udividend = (uint32_t)(-dividend);
    } else {
        udividend = (uint32_t)dividend;
    }

    if (divisor < 0) {
        udivisor = (uint32_t)(-divisor);
    } else {
        udivisor = (uint32_t)divisor;
    }

    result = __umodsi3(udividend, udivisor);

    return negative ? -(int32_t)result : (int32_t)result;
}
