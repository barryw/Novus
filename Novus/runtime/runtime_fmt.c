// Novus Runtime - String Formatting
// Integer to string conversion functions for std::fmt_primitives

#include "novus_runtime.h"

// Helper to copy from temp buffer to output buffer with bounds checking
static uint32_t copy_to_buffer(const char* temp, uint8_t* buffer, uint32_t buffer_size) {
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

static void uint_to_str(char* buf, uint32_t num) {
    char temp[12];
    int i = 0;

    if (num == 0) {
        buf[0] = '0';
        buf[1] = '\0';
        return;
    }

    while (num > 0) {
        temp[i++] = '0' + (num % 10);
        num /= 10;
    }

    // Reverse the digits
    int j = 0;
    while (i > 0) {
        buf[j++] = temp[--i];
    }
    buf[j] = '\0';
}

uint32_t i8_to_string(int8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[5]; // -128 to 127 requires at most 4 chars + null
    int_to_str(temp, (int32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i16_to_string(int16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[7]; // -32768 to 32767 requires at most 6 chars + null
    int_to_str(temp, (int32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i32_to_string(int32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // -2147483648 to 2147483647 requires at most 11 chars + null
    int_to_str(temp, value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i64_to_string(int64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to i32 range. Full i64 support would require more complex implementation
    if (value > 2147483647LL) value = 2147483647LL;
    if (value < -2147483648LL) value = -2147483648LL;
    return i32_to_string((int32_t)value, buffer, buffer_size);
}

uint32_t u8_to_string(uint8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[4]; // 0 to 255 requires at most 3 chars + null
    uint_to_str(temp, (uint32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u16_to_string(uint16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[6]; // 0 to 65535 requires at most 5 chars + null
    uint_to_str(temp, (uint32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u32_to_string(uint32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // 0 to 4294967295 requires at most 10 chars + null
    uint_to_str(temp, value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u64_to_string(uint64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to u32 range. Full u64 support would require more complex implementation
    if (value > 4294967295ULL) value = 4294967295ULL;
    return u32_to_string((uint32_t)value, buffer, buffer_size);
}
