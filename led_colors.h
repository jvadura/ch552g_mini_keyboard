// ===================================================================================
// LED Color Definitions for CH552G Keyboard v2.0
// ===================================================================================
//
// Pre-defined RGB color palette for the 8 color indices (0-7).
// These are base colors at medium brightness. The global brightness setting
// in the configuration will scale these values.
//
// Color indices match the firmware color constants:
//   0: COLOR_OFF     - Off (black)
//   1: COLOR_RED     - Red
//   2: COLOR_GREEN   - Green
//   3: COLOR_BLUE    - Blue
//   4: COLOR_YELLOW  - Yellow
//   5: COLOR_CYAN    - Cyan
//   6: COLOR_MAGENTA - Magenta
//   7: COLOR_WHITE   - White
//
// ===================================================================================

#pragma once
#include <stdint.h>

// RGB structure for color lookup
typedef struct {
    uint8_t r;
    uint8_t g;
    uint8_t b;
} RGB_Color;

// Pre-defined color palette (8 colors)
// Values are at ~40% brightness to avoid excessive power draw
// You can adjust these RGB values to fine-tune colors and brightness
static const RGB_Color COLOR_PALETTE[8] = {
    {0,   0,   0},    // 0: COLOR_OFF     - Off (black)
    {100, 0,   0},    // 1: COLOR_RED     - Red
    {0,   100, 0},    // 2: COLOR_GREEN   - Green
    {0,   0,   100},  // 3: COLOR_BLUE    - Blue
    {100, 80,  0},    // 4: COLOR_YELLOW  - Yellow (slightly less green for better color)
    {0,   100, 100},  // 5: COLOR_CYAN    - Cyan
    {100, 0,   100},  // 6: COLOR_MAGENTA - Magenta
    {100, 100, 100}   // 7: COLOR_WHITE   - White
};

// Default global brightness (0-255, where 255 = 100%)
#define DEFAULT_LED_BRIGHTNESS  10    // ~4% brightness (10/255) - minimal value

// Apply brightness scaling to a color
inline void applyBrightness(uint8_t *r, uint8_t *g, uint8_t *b, uint8_t brightness) {
    // Scale RGB values by brightness (0-255)
    // brightness=0 → all off, brightness=255 → full color
    *r = (*r * brightness) / 255;
    *g = (*g * brightness) / 255;
    *b = (*b * brightness) / 255;
}

// Get color from palette with brightness applied
inline void getColor(uint8_t color_index, uint8_t brightness, uint8_t *r, uint8_t *g, uint8_t *b) {
    if(color_index > 7) color_index = 0;  // Safety: default to off

    // Get base color from palette
    *r = COLOR_PALETTE[color_index].r;
    *g = COLOR_PALETTE[color_index].g;
    *b = COLOR_PALETTE[color_index].b;

    // Apply brightness scaling
    applyBrightness(r, g, b, brightness);
}
