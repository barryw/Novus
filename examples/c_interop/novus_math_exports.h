#ifndef NOVUS_EXPORTS_H
#define NOVUS_EXPORTS_H

#include <stdint.h>
#include <stdbool.h>

#include "novus_types.h"

#ifdef __cplusplus
extern "C" {
#endif

// Exported Novus functions

int32_t add(int32_t a, int32_t b);
int32_t subtract(int32_t a, int32_t b);
int32_t multiply(int32_t a, int32_t b);
int32_t divide(int32_t a, int32_t b);

#ifdef __cplusplus
}
#endif

#endif // NOVUS_EXPORTS_H
