// Novus Runtime - Semaphore Wrappers
// Thin wrappers around Exec semaphore functions for type safety

#include "novus_runtime.h"

// Semaphore wrapper functions that take void pointers
// These avoid FFI type issues in generated C code
void __novus_init_semaphore(void* sigSem) {
    InitSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_obtain_semaphore(void* sigSem) {
    ObtainSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_release_semaphore(void* sigSem) {
    ReleaseSemaphore((struct SignalSemaphore*)sigSem);
}

int32_t __novus_attempt_semaphore(void* sigSem) {
    return AttemptSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_obtain_semaphore_shared(void* sigSem) {
    ObtainSemaphoreShared((struct SignalSemaphore*)sigSem);
}

int32_t __novus_attempt_semaphore_shared(void* sigSem) {
    return AttemptSemaphoreShared((struct SignalSemaphore*)sigSem);
}

void __novus_add_semaphore(void* sigSem) {
    AddSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_rem_semaphore(void* sigSem) {
    RemSemaphore((struct SignalSemaphore*)sigSem);
}

void* __novus_find_semaphore(uint8_t* name) {
    return FindSemaphore((char*)name);
}
