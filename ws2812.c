// ===================================================================================
// WS2812 (NeoPixel) Implementation for CH552G Keyboard v2.0
// ===================================================================================

#include <Arduino.h>
#include "ws2812.h"

// Pin definition for assembly (P3.4 = bit 4 of port 3 at 0xB0)
__sbit __at (0xB0+4) WS2812_PIN_BIT;  // P3.4

// LED buffer (GRB format: 3 bytes per LED)
uint8_t WS2812_buffer[3 * WS2812_COUNT];
static uint8_t *buffer_ptr;

// ===================================================================================
// Send Single Byte to WS2812 (Timing-Critical Assembly)
// ===================================================================================
// Protocol timing for 16MHz clock:
// - Bit "1": HIGH for 10 cycles (625ns), LOW for 8 cycles (500ns)
// - Bit "0": HIGH for 4 cycles (250ns), LOW for 14 cycles (875ns)
// - Total: 18 cycles per bit (1.125μs)
//
// WS2812 spec:
// - T0H: 0.4μs ±150ns (0 = short pulse)
// - T1H: 0.8μs ±150ns (1 = long pulse)
// - T0L/T1L: remainder to 1.25μs ±600ns
void WS2812_sendByte(uint8_t data) {
  data;  // Suppress unreferenced parameter warning
  __asm
    .even
    mov  r7, #8           ; 2 CLK - 8 bits to transfer
    xch  a, dpl           ; 2 CLK - data byte -> accumulator
    01$:
    rlc  a                ; 1 CLK - shift MSB into carry
    setb _WS2812_PIN_BIT  ; 2 CLK - pin HIGH (start of bit)
    mov  _WS2812_PIN_BIT, c ; 2 CLK - if carry=0, pin goes LOW now (T0H=250ns)
    nop                   ; 1 CLK - \
    nop                   ; 1 CLK -  | T1H delay (total 10 cycles = 625ns)
    nop                   ; 1 CLK -  |
    nop                   ; 1 CLK -  |
    nop                   ; 1 CLK -  |
    nop                   ; 1 CLK - /
    clr  _WS2812_PIN_BIT  ; 2 CLK - if carry=1, pin goes LOW now (T1H=625ns)
    nop                   ; 1 CLK - TCT delay
    nop                   ; 1 CLK - TCT delay
    djnz r7, 01$          ; 4 CLK - loop (2 when done)
  __endasm;
}

// ===================================================================================
// Initialize WS2812 Pin
// ===================================================================================
void WS2812_init(void) {
  pinMode(WS2812_PIN, OUTPUT);
  digitalWrite(WS2812_PIN, LOW);

  // Clear buffer
  for(uint8_t i = 0; i < 3 * WS2812_COUNT; i++) {
    WS2812_buffer[i] = 0;
  }
}

// ===================================================================================
// Send Buffer to LEDs
// ===================================================================================
void WS2812_update(void) {
  buffer_ptr = WS2812_buffer;

  // Disable interrupts during timing-critical transmission
  noInterrupts();

  // Send all bytes (3 bytes × 3 LEDs = 9 bytes)
  for(uint8_t i = 0; i < 3 * WS2812_COUNT; i++) {
    WS2812_sendByte(*buffer_ptr++);
  }

  // Re-enable interrupts
  interrupts();

  // Latch delay (>50μs for older WS2812, >300μs for newer WS2812B)
  // Using 300μs to be safe with all variants
  delayMicroseconds(300);
}

// ===================================================================================
// Set Single LED Color (R, G, B)
// ===================================================================================
// WS2812 uses GRB byte order: Green, Red, Blue
void WS2812_setPixel(uint8_t pixel, uint8_t r, uint8_t g, uint8_t b) {
  if(pixel >= WS2812_COUNT) return;

  uint8_t *ptr = WS2812_buffer + (3 * pixel);
  *ptr++ = g;  // Green first
  *ptr++ = r;  // Red second
  *ptr   = b;  // Blue third
}

// ===================================================================================
// Clear All LEDs
// ===================================================================================
void WS2812_clear(void) {
  for(uint8_t i = 0; i < 3 * WS2812_COUNT; i++) {
    WS2812_buffer[i] = 0;
  }
  WS2812_update();
}

// ===================================================================================
// Alias for update()
// ===================================================================================
void WS2812_show(void) {
  WS2812_update();
}
