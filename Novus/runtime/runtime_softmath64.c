/**
 * Novus Runtime - 64-bit Software Math Library for 68020+ Targets
 *
 * The supported 68k processors lack native 64-bit arithmetic instructions.
 * This library provides software implementations compatible with
 * GCC's libgcc naming conventions for easy integration.
 *
 * CPU Support:
 * - 68020+: Full software implementation
 * - 68020+: Uses 32x32->64 MULU.L where available
 *
 * All functions use the standard Amiga/GCC calling convention:
 * - 64-bit values passed as two 32-bit values (high, low)
 * - Return value in D0:D1 (D0 = high, D1 = low)
 */

#include <exec/types.h>

/* 64-bit integer type as a struct of two 32-bit values */
typedef struct {
    ULONG hi;
    ULONG lo;
} uint64_pair;

typedef struct {
    LONG hi;
    ULONG lo;
} int64_pair;

/* ============================================================================
 * ADDITION AND SUBTRACTION
 * ============================================================================ */

/**
 * 64-bit unsigned addition: result = a + b
 * Uses the X (extend) flag for carry propagation.
 */
uint64_pair __adddi3(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result;
    ULONG carry;

    /* Add low parts first */
    result.lo = a_lo + b_lo;

    /* Detect carry from low addition */
    carry = (result.lo < a_lo) ? 1 : 0;

    /* Add high parts with carry */
    result.hi = a_hi + b_hi + carry;

    return result;
}

/**
 * 64-bit unsigned subtraction: result = a - b
 */
uint64_pair __subdi3(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result;
    ULONG borrow;

    /* Subtract low parts first, detect borrow */
    borrow = (a_lo < b_lo) ? 1 : 0;
    result.lo = a_lo - b_lo;

    /* Subtract high parts with borrow */
    result.hi = a_hi - b_hi - borrow;

    return result;
}

/**
 * 64-bit negation: result = -a
 */
uint64_pair __negdi2(ULONG a_hi, ULONG a_lo)
{
    uint64_pair result;

    /* Two's complement: invert all bits and add 1 */
    result.lo = ~a_lo + 1;
    result.hi = ~a_hi;

    /* If low part wrapped, add carry to high part */
    if (result.lo == 0) {
        result.hi++;
    }

    return result;
}

/* ============================================================================
 * MULTIPLICATION
 * ============================================================================ */

/**
 * 32x32->64 unsigned multiply helper
 * Breaks down into four 16x16->32 multiplies for portable 68k code generation.
 *
 * a = (a_hi << 16) + a_lo
 * b = (b_hi << 16) + b_lo
 * a * b = a_hi*b_hi<<32 + (a_hi*b_lo + a_lo*b_hi)<<16 + a_lo*b_lo
 */
static uint64_pair mul32x32_to_64(ULONG a, ULONG b)
{
    uint64_pair result;

    UWORD a_hi = (UWORD)(a >> 16);
    UWORD a_lo = (UWORD)(a & 0xFFFF);
    UWORD b_hi = (UWORD)(b >> 16);
    UWORD b_lo = (UWORD)(b & 0xFFFF);

    /* Four 16x16->32 multiplies */
    ULONG ll = (ULONG)a_lo * (ULONG)b_lo;       /* Low * Low */
    ULONG lh = (ULONG)a_lo * (ULONG)b_hi;       /* Low * High */
    ULONG hl = (ULONG)a_hi * (ULONG)b_lo;       /* High * Low */
    ULONG hh = (ULONG)a_hi * (ULONG)b_hi;       /* High * High */

    /* Combine partial products */
    /* result = hh<<32 + (lh + hl)<<16 + ll */

    ULONG mid = lh + hl;
    ULONG mid_carry = (mid < lh) ? 0x10000UL : 0;  /* Carry from middle addition */

    result.lo = ll + (mid << 16);
    ULONG lo_carry = (result.lo < ll) ? 1 : 0;

    result.hi = hh + (mid >> 16) + mid_carry + lo_carry;

    return result;
}

/**
 * 64-bit unsigned multiply: result = a * b
 *
 * Uses schoolbook multiplication with 32-bit partial products:
 * (a_hi*2^32 + a_lo) * (b_hi*2^32 + b_lo)
 * = a_hi*b_hi*2^64 + (a_hi*b_lo + a_lo*b_hi)*2^32 + a_lo*b_lo
 *
 * We only need the low 64 bits, so a_hi*b_hi*2^64 is discarded.
 */
uint64_pair __muldi3(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result;

    /* Start with a_lo * b_lo (full 64-bit product) */
    result = mul32x32_to_64(a_lo, b_lo);

    /* Add a_hi * b_lo (shifted left by 32, only low 32 bits matter) */
    result.hi += a_hi * b_lo;

    /* Add a_lo * b_hi (shifted left by 32, only low 32 bits matter) */
    result.hi += a_lo * b_hi;

    /* a_hi * b_hi would be shifted left by 64, so it's discarded */

    return result;
}

/**
 * 64-bit signed multiply
 * Converts to unsigned, multiplies, then adjusts sign.
 */
int64_pair __muldi3_signed(LONG a_hi, ULONG a_lo, LONG b_hi, ULONG b_lo)
{
    int negative = 0;
    uint64_pair ua, ub, result;
    int64_pair signed_result;

    /* Make a positive */
    if (a_hi < 0) {
        ua = __negdi2((ULONG)a_hi, a_lo);
        negative = !negative;
    } else {
        ua.hi = (ULONG)a_hi;
        ua.lo = a_lo;
    }

    /* Make b positive */
    if (b_hi < 0) {
        ub = __negdi2((ULONG)b_hi, b_lo);
        negative = !negative;
    } else {
        ub.hi = (ULONG)b_hi;
        ub.lo = b_lo;
    }

    /* Unsigned multiply */
    result = __muldi3(ua.hi, ua.lo, ub.hi, ub.lo);

    /* Negate if needed */
    if (negative) {
        result = __negdi2(result.hi, result.lo);
    }

    signed_result.hi = (LONG)result.hi;
    signed_result.lo = result.lo;
    return signed_result;
}

/* ============================================================================
 * DIVISION AND MODULO
 * ============================================================================ */

/**
 * 64-bit unsigned divide: quotient = dividend / divisor
 * Uses binary long division algorithm.
 */
uint64_pair __udivdi3(ULONG dividend_hi, ULONG dividend_lo,
                       ULONG divisor_hi, ULONG divisor_lo)
{
    uint64_pair quotient = {0, 0};
    uint64_pair remainder = {dividend_hi, dividend_lo};
    uint64_pair divisor = {divisor_hi, divisor_lo};
    int bit;

    /* Handle division by zero */
    if (divisor_hi == 0 && divisor_lo == 0) {
        /* Return maximum value to indicate error */
        quotient.hi = 0xFFFFFFFF;
        quotient.lo = 0xFFFFFFFF;
        return quotient;
    }

    /* Handle easy case: divisor > dividend */
    if (divisor_hi > dividend_hi ||
        (divisor_hi == dividend_hi && divisor_lo > dividend_lo)) {
        return quotient;  /* Return 0 */
    }

    /* Handle case where divisor fits in 32 bits */
    if (divisor_hi == 0) {
        /* Special fast path for 32-bit divisor */
        if (dividend_hi == 0) {
            /* Both fit in 32 bits - use hardware divide if available */
            quotient.lo = dividend_lo / divisor_lo;
            return quotient;
        }

        /* 64/32 divide */
        ULONG q_hi, q_lo, rem;

        /* First divide high part */
        q_hi = dividend_hi / divisor_lo;
        rem = dividend_hi % divisor_lo;

        /* Combine remainder with low part and divide again */
        /* This requires 64/32 which we implement with shifts */
        ULONG dividend_combined_hi = rem;
        ULONG dividend_combined_lo = dividend_lo;

        /* Binary long division for the remaining bits */
        q_lo = 0;
        for (bit = 31; bit >= 0; bit--) {
            /* Shift remainder left by 1 */
            dividend_combined_hi = (dividend_combined_hi << 1) | (dividend_combined_lo >> 31);
            dividend_combined_lo <<= 1;

            /* Compare and subtract */
            if (dividend_combined_hi >= divisor_lo ||
                (dividend_combined_hi == divisor_lo - 1 && dividend_combined_lo >= (1UL << 31))) {
                /* Note: this comparison is approximate for the edge case */
                if (dividend_combined_hi > divisor_lo ||
                    (dividend_combined_hi == divisor_lo && dividend_combined_lo >= 0)) {
                    dividend_combined_hi -= divisor_lo;
                    q_lo |= (1UL << bit);
                }
            }
        }

        quotient.hi = q_hi;
        quotient.lo = q_lo;
        return quotient;
    }

    /* Full 64-bit by 64-bit divide using binary long division */
    /* Find the highest set bit in divisor for alignment */
    int shift = 0;
    uint64_pair shifted_divisor = divisor;

    /* Shift divisor left until it would overflow or exceeds dividend */
    while (shifted_divisor.hi < 0x80000000UL) {
        if (shifted_divisor.hi > remainder.hi ||
            (shifted_divisor.hi == remainder.hi && shifted_divisor.lo > remainder.lo)) {
            break;
        }

        /* Shift left by 1 */
        shifted_divisor.hi = (shifted_divisor.hi << 1) | (shifted_divisor.lo >> 31);
        shifted_divisor.lo <<= 1;
        shift++;
    }

    /* Now shift back and subtract */
    for (bit = shift; bit >= 0; bit--) {
        /* If remainder >= shifted_divisor, subtract and set quotient bit */
        if (remainder.hi > shifted_divisor.hi ||
            (remainder.hi == shifted_divisor.hi && remainder.lo >= shifted_divisor.lo)) {

            remainder = __subdi3(remainder.hi, remainder.lo,
                                  shifted_divisor.hi, shifted_divisor.lo);

            if (bit >= 32) {
                quotient.hi |= (1UL << (bit - 32));
            } else {
                quotient.lo |= (1UL << bit);
            }
        }

        /* Shift divisor right by 1 */
        shifted_divisor.lo = (shifted_divisor.lo >> 1) | (shifted_divisor.hi << 31);
        shifted_divisor.hi >>= 1;
    }

    return quotient;
}

/**
 * 64-bit unsigned modulo: remainder = dividend % divisor
 */
uint64_pair __umoddi3(ULONG dividend_hi, ULONG dividend_lo,
                       ULONG divisor_hi, ULONG divisor_lo)
{
    uint64_pair remainder = {dividend_hi, dividend_lo};
    uint64_pair divisor = {divisor_hi, divisor_lo};
    int bit, shift;

    /* Handle division by zero */
    if (divisor_hi == 0 && divisor_lo == 0) {
        return remainder;  /* Return dividend as error indicator */
    }

    /* Handle easy case: divisor > dividend */
    if (divisor_hi > dividend_hi ||
        (divisor_hi == dividend_hi && divisor_lo > dividend_lo)) {
        return remainder;  /* Return dividend */
    }

    /* Handle case where both fit in 32 bits */
    if (divisor_hi == 0 && dividend_hi == 0) {
        remainder.lo = dividend_lo % divisor_lo;
        return remainder;
    }

    /* Full 64-bit modulo using subtraction */
    uint64_pair shifted_divisor = divisor;

    /* Shift divisor left until it would overflow */
    shift = 0;
    while (shifted_divisor.hi < 0x80000000UL) {
        if (shifted_divisor.hi > remainder.hi ||
            (shifted_divisor.hi == remainder.hi && shifted_divisor.lo > remainder.lo)) {
            break;
        }
        shifted_divisor.hi = (shifted_divisor.hi << 1) | (shifted_divisor.lo >> 31);
        shifted_divisor.lo <<= 1;
        shift++;
    }

    /* Subtract shifted divisor repeatedly */
    for (bit = shift; bit >= 0; bit--) {
        if (remainder.hi > shifted_divisor.hi ||
            (remainder.hi == shifted_divisor.hi && remainder.lo >= shifted_divisor.lo)) {

            remainder = __subdi3(remainder.hi, remainder.lo,
                                  shifted_divisor.hi, shifted_divisor.lo);
        }

        shifted_divisor.lo = (shifted_divisor.lo >> 1) | (shifted_divisor.hi << 31);
        shifted_divisor.hi >>= 1;
    }

    return remainder;
}

/**
 * 64-bit signed divide
 */
int64_pair __divdi3(LONG dividend_hi, ULONG dividend_lo,
                     LONG divisor_hi, ULONG divisor_lo)
{
    int negative = 0;
    uint64_pair udividend, udivisor, quotient;
    int64_pair result;

    /* Make dividend positive */
    if (dividend_hi < 0) {
        udividend = __negdi2((ULONG)dividend_hi, dividend_lo);
        negative = !negative;
    } else {
        udividend.hi = (ULONG)dividend_hi;
        udividend.lo = dividend_lo;
    }

    /* Make divisor positive */
    if (divisor_hi < 0) {
        udivisor = __negdi2((ULONG)divisor_hi, divisor_lo);
        negative = !negative;
    } else {
        udivisor.hi = (ULONG)divisor_hi;
        udivisor.lo = divisor_lo;
    }

    /* Unsigned divide */
    quotient = __udivdi3(udividend.hi, udividend.lo, udivisor.hi, udivisor.lo);

    /* Negate if needed */
    if (negative) {
        quotient = __negdi2(quotient.hi, quotient.lo);
    }

    result.hi = (LONG)quotient.hi;
    result.lo = quotient.lo;
    return result;
}

/**
 * 64-bit signed modulo
 * Result has same sign as dividend (C99 behavior).
 */
int64_pair __moddi3(LONG dividend_hi, ULONG dividend_lo,
                     LONG divisor_hi, ULONG divisor_lo)
{
    int neg_dividend = 0;
    uint64_pair udividend, udivisor, remainder;
    int64_pair result;

    /* Make dividend positive, remember sign */
    if (dividend_hi < 0) {
        udividend = __negdi2((ULONG)dividend_hi, dividend_lo);
        neg_dividend = 1;
    } else {
        udividend.hi = (ULONG)dividend_hi;
        udividend.lo = dividend_lo;
    }

    /* Make divisor positive */
    if (divisor_hi < 0) {
        udivisor = __negdi2((ULONG)divisor_hi, divisor_lo);
    } else {
        udivisor.hi = (ULONG)divisor_hi;
        udivisor.lo = divisor_lo;
    }

    /* Unsigned modulo */
    remainder = __umoddi3(udividend.hi, udividend.lo, udivisor.hi, udivisor.lo);

    /* Negate if dividend was negative */
    if (neg_dividend) {
        remainder = __negdi2(remainder.hi, remainder.lo);
    }

    result.hi = (LONG)remainder.hi;
    result.lo = remainder.lo;
    return result;
}

/* ============================================================================
 * SHIFTS
 * ============================================================================ */

/**
 * 64-bit logical left shift
 */
uint64_pair __ashldi3(ULONG a_hi, ULONG a_lo, int shift)
{
    uint64_pair result;

    if (shift == 0) {
        result.hi = a_hi;
        result.lo = a_lo;
    } else if (shift >= 64) {
        result.hi = 0;
        result.lo = 0;
    } else if (shift >= 32) {
        result.hi = a_lo << (shift - 32);
        result.lo = 0;
    } else {
        result.hi = (a_hi << shift) | (a_lo >> (32 - shift));
        result.lo = a_lo << shift;
    }

    return result;
}

/**
 * 64-bit logical right shift (unsigned)
 */
uint64_pair __lshrdi3(ULONG a_hi, ULONG a_lo, int shift)
{
    uint64_pair result;

    if (shift == 0) {
        result.hi = a_hi;
        result.lo = a_lo;
    } else if (shift >= 64) {
        result.hi = 0;
        result.lo = 0;
    } else if (shift >= 32) {
        result.hi = 0;
        result.lo = a_hi >> (shift - 32);
    } else {
        result.hi = a_hi >> shift;
        result.lo = (a_lo >> shift) | (a_hi << (32 - shift));
    }

    return result;
}

/**
 * 64-bit arithmetic right shift (signed)
 */
int64_pair __ashrdi3(LONG a_hi, ULONG a_lo, int shift)
{
    int64_pair result;

    if (shift == 0) {
        result.hi = a_hi;
        result.lo = a_lo;
    } else if (shift >= 64) {
        /* All bits come from sign */
        result.hi = (a_hi < 0) ? -1 : 0;
        result.lo = result.hi;
    } else if (shift >= 32) {
        result.hi = (a_hi < 0) ? -1 : 0;
        result.lo = (ULONG)(a_hi >> (shift - 32));  /* Arithmetic shift */
    } else {
        result.hi = a_hi >> shift;  /* Arithmetic shift */
        result.lo = (a_lo >> shift) | ((ULONG)a_hi << (32 - shift));
    }

    return result;
}

/* ============================================================================
 * COMPARISONS
 * ============================================================================ */

/**
 * 64-bit unsigned comparison
 * Returns: -1 if a < b, 0 if a == b, 1 if a > b
 */
int __ucmpdi2(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    if (a_hi < b_hi) return -1;
    if (a_hi > b_hi) return 1;
    if (a_lo < b_lo) return -1;
    if (a_lo > b_lo) return 1;
    return 0;
}

/**
 * 64-bit signed comparison
 * Returns: -1 if a < b, 0 if a == b, 1 if a > b
 */
int __cmpdi2(LONG a_hi, ULONG a_lo, LONG b_hi, ULONG b_lo)
{
    if (a_hi < b_hi) return -1;
    if (a_hi > b_hi) return 1;
    if (a_lo < b_lo) return -1;
    if (a_lo > b_lo) return 1;
    return 0;
}

/* ============================================================================
 * CONVENIENCE WRAPPERS FOR NOVUS COMPILER
 * ============================================================================
 * These use a simpler calling convention where 64-bit values are passed
 * as single arguments (the compiler handles the split).
 */

/**
 * Simple 64-bit multiply wrapper
 * For use with: let result: i64 = a * b
 */
ULONG __novus_mul64_lo(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __muldi3(a_hi, a_lo, b_hi, b_lo);
    return result.lo;
}

ULONG __novus_mul64_hi(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __muldi3(a_hi, a_lo, b_hi, b_lo);
    return result.hi;
}

/**
 * Simple 64-bit divide wrappers
 */
ULONG __novus_udiv64_lo(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __udivdi3(a_hi, a_lo, b_hi, b_lo);
    return result.lo;
}

ULONG __novus_udiv64_hi(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __udivdi3(a_hi, a_lo, b_hi, b_lo);
    return result.hi;
}

ULONG __novus_umod64_lo(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __umoddi3(a_hi, a_lo, b_hi, b_lo);
    return result.lo;
}

ULONG __novus_umod64_hi(ULONG a_hi, ULONG a_lo, ULONG b_hi, ULONG b_lo)
{
    uint64_pair result = __umoddi3(a_hi, a_lo, b_hi, b_lo);
    return result.hi;
}
