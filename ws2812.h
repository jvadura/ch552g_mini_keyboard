// ===================================================================================
// WS2812 (NeoPixel) Functions for CH552G Keyboard v2.0
// ===================================================================================
//
// Adapted from neo.c by Stefan Wagner for CH552G Arduino environment.
// Supports 3 WS2812 LEDs in GRB format at 16MHz system clock.
//
// Pin: P3.4 (Arduino pin 34) - WS2812 DATA line
// LEDs: 3 × WS2812 addressable RGB LEDs
// ===================================================================================

#pragma once
#include <stdint.h>

// Configuration
#define WS2812_COUNT     3       // Number of LEDs
#define WS2812_PIN       34      // P3.4 (matches PIN_LED in main code)

// Public Functions
void WS2812_init(void);                                               // Initialize WS2812 output pin
void WS2812_update(void);                                             // Send buffer to LEDs
void WS2812_setPixel(uint8_t pixel, uint8_t r, uint8_t g, uint8_t b);// Set single LED color
void WS2812_clear(void);                                              // Turn off all LEDs
void WS2812_show(void);                                               // Alias for update()

// Internal buffer (exposed for advanced use)
extern uint8_t WS2812_buffer[3 * WS2812_COUNT];
