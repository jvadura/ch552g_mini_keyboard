/**
 * CH552G Mini Keyboard v2.0 - Arduino Version
 *
 * PC-configurable USB mini keyboard with 3 buttons and rotary encoder.
 *
 * Board: CH55xDuino CH552
 * Settings:
 *   - Clock: 16MHz (internal) 3.5V or 5V
 *   - Upload Method: USB
 *   - USB Settings: USER CODE w/148B USB RAM
 */

#include <Arduino.h>
#include "src/usb/userUsbHidKeyboardMouse/USBHIDKeyboardMouse.h"
#include "ws2812.h"
#include "led_colors.h"

// ============================================================================
// Pin Definitions
// ============================================================================

#define PIN_BTN_1       11  // P1.1
#define PIN_BTN_2       17  // P1.7
#define PIN_BTN_3       16  // P1.6
#define PIN_ENC_BTN     33  // P3.3
#define PIN_ENC_A       31  // P3.1
#define PIN_ENC_B       30  // P3.0
#define PIN_LED         34  // P3.4 - WS2812 LEDs

// ============================================================================
// Configuration Constants
// ============================================================================

#define MAX_SLOTS       3
#define MAX_INPUTS      5
#define TOTAL_ACTIONS   15

// Input indices
#define INPUT_BTN_1     0
#define INPUT_BTN_2     1
#define INPUT_BTN_3     2
#define INPUT_ENC_CW    3
#define INPUT_ENC_CCW   4

// Action types
#define ACTION_NONE     0x0
#define ACTION_KEYBOARD 0x1
#define ACTION_MEDIA    0x2
#define ACTION_MOUSE    0x3
#define ACTION_SCROLL   0x4

// Modifiers
#define MOD_CTRL        0x10
#define MOD_SHIFT       0x40
#define MOD_ALT         0x20
#define MOD_GUI         0x80
#define HOLD_FLAG       0x08

// Colors (simplified palette)
#define COLOR_OFF       0
#define COLOR_RED       1
#define COLOR_GREEN     2
#define COLOR_BLUE      3
#define COLOR_YELLOW    4
#define COLOR_CYAN      5
#define COLOR_MAGENTA   6
#define COLOR_WHITE     7

// ============================================================================
// Action Structure (8 bytes)
// ============================================================================

typedef struct {
    uint8_t control;      // Modifiers|Hold|Type
    uint8_t primary;      // Primary value
    uint8_t secondary;    // Secondary value
    uint8_t color_idle;   // LED color when idle
    uint8_t color_active; // LED color when active
    uint8_t reserved[3];  // Reserved
} Action;

// ============================================================================
// Configuration Structure (128 bytes for DataFlash)
// ============================================================================

typedef struct {
    // Header (8 bytes)
    uint16_t magic;
    uint8_t version;
    uint8_t active_slot;
    uint8_t checksum;
    uint8_t reserved_hdr[3];  // [0]=write marker, [1]=led_brightness, [2]=reserved

    // Actions (120 bytes)
    Action slots[MAX_SLOTS][MAX_INPUTS];
} Configuration;

// Helper macro to access LED brightness in configuration
#define config_led_brightness  (config.reserved_hdr[1])

// ============================================================================
// DataFlash Write Protection
// ============================================================================

#define WRITE_COMPLETE_MARKER  0xAA  // Marker indicating successful write

// ============================================================================
// USB Feature Report Protocol
// ============================================================================

#define REPORT_ID_CONFIG    0xF0
#define REPORT_SIZE         64

// Commands
#define CMD_READ_CONFIG     0x01
#define CMD_WRITE_ACTION    0x02
#define CMD_WRITE_ALL       0x03
#define CMD_GET_INFO        0x04
#define CMD_SET_SLOT        0x05
#define CMD_FACTORY_RESET   0x06

// Error codes
#define ERR_SUCCESS         0x00
#define ERR_INVALID_CMD     0x01
#define ERR_INVALID_SLOT    0x02
#define ERR_INVALID_INPUT   0x03
#define ERR_CHECKSUM        0x05

// ============================================================================
// Global Variables
// ============================================================================

Configuration config;
uint8_t current_slot = 0;
bool slot_switch_mode = false;
uint8_t selected_slot = 0;

// Button states
bool btn_states[3] = {false, false, false};
bool btn_last[3] = {false, false, false};
uint32_t btn_press_time[3] = {0, 0, 0};

// Encoder state
uint8_t enc_state = 0;
uint8_t enc_last = 0;
int8_t enc_position = 0;
bool enc_btn_pressed = false;
uint32_t enc_btn_press_time = 0;

// LED buffer (simplified - no WS2812 for now)
uint8_t led_colors[3] = {0, 0, 0};

// USB Feature Report state
uint8_t usb_response[REPORT_SIZE];
uint8_t usb_response_ready = 0;  // 0=not ready, 1=ready
uint8_t transfer_sequence = 0;

// ============================================================================
// Forward Declarations
// ============================================================================

uint8_t calcChecksum(const Configuration* cfg);
void saveConfigToDataFlash();
void loadDefaultConfig();

// ============================================================================
// USB Feature Report Handler
// ============================================================================
//
// NOTE: To fully enable PC configuration, this handler must be called from
// the USB stack when a SET_REPORT request is received with Report ID 0xF0.
//
// Required modification in src/usb/userUsbHidKeyboardMouse/USBhandler.c:
//   In USB_REQ_TYP_CLASS handler (line 56), add:
//     case 0x09:  // HID SET_REPORT
//       if(Ep0Buffer[2] == 0xF0) {  // Report ID 0xF0
//         handleUSBFeatureReport(Ep0Buffer, REPORT_SIZE);
//       }
//       break;
//
// For now, this handler is implemented and ready but not wired to USB stack.
// ============================================================================

uint8_t calcReportChecksum(const uint8_t* data, uint8_t len) {
    uint8_t checksum = 0;
    for(uint8_t i = 0; i < len; i++) {
        checksum ^= data[i];
    }
    return checksum;
}

bool verifyReportChecksum(const uint8_t* data, uint8_t len) {
    if(len < 2) return false;
    uint8_t calc = calcReportChecksum(data, len - 1);
    return (calc == data[len - 1]);
}

void buildResponse(uint8_t command, uint8_t status) {
    memset(usb_response, 0, REPORT_SIZE);
    usb_response[0] = REPORT_ID_CONFIG;
    usb_response[1] = command;
    usb_response[2] = status;
}

void finalizeResponse() {
    usb_response[REPORT_SIZE - 1] = calcReportChecksum(usb_response, REPORT_SIZE - 1);
    usb_response_ready = 1;  // Mark response as ready
}

void handleUSBFeatureReport(uint8_t* report, uint8_t len) {
    // Verify report ID and size first
    if(report[0] != REPORT_ID_CONFIG || len != REPORT_SIZE) {
        return;
    }

    // Verify checksum BEFORE accessing any other fields
    if(!verifyReportChecksum(report, len)) {
        // Use safe default command value (0xFF) in error response
        buildResponse(0xFF, ERR_CHECKSUM);
        finalizeResponse();
        return;
    }

    // NOW safe to access command byte
    uint8_t command = report[1];

    switch(command) {
        case CMD_WRITE_ACTION: {
            // Write single action
            uint8_t slot = report[2];
            uint8_t input = report[3];

            // Validate parameters
            if(slot >= MAX_SLOTS) {
                buildResponse(command, ERR_INVALID_SLOT);
                finalizeResponse();
                return;
            }
            if(input >= MAX_INPUTS) {
                buildResponse(command, ERR_INVALID_INPUT);
                finalizeResponse();
                return;
            }

            // Copy action data (8 bytes at offset 4)
            memcpy(&config.slots[slot][input], &report[4], sizeof(Action));

            // Recalculate checksum
            config.checksum = calcChecksum(&config);

            // Save to DataFlash
            saveConfigToDataFlash();

            // Build success response
            buildResponse(command, ERR_SUCCESS);
            finalizeResponse();
            break;
        }

        case CMD_WRITE_ALL: {
            // Write full configuration (multi-packet transfer)
            uint8_t sequence = report[2];
            uint8_t total = report[3];

            // Validate sequence matches expected state
            if(sequence == 0) {
                // First packet: Actions 0-6 (56 bytes)
                memcpy(&config.slots[0][0], &report[4], 56);
                transfer_sequence = 1;
            }
            else if(sequence == 1 && transfer_sequence == 1) {
                // Second packet: Actions 7-13 (56 bytes) - ONLY if we got packet 0
                uint8_t* dest = (uint8_t*)&config.slots[0][0];
                memcpy(dest + 56, &report[4], 56);
                transfer_sequence = 2;
            }
            else if(sequence == 2 && transfer_sequence == 2) {
                // Third packet: Action 14 (8 bytes) + commit - ONLY if we got packets 0 & 1
                uint8_t* dest = (uint8_t*)&config.slots[0][0];
                memcpy(dest + 112, &report[4], 8);

                // Get active slot and commit flag
                uint8_t new_slot = report[12];
                uint8_t commit = report[13];

                if(new_slot < MAX_SLOTS) {
                    config.active_slot = new_slot;
                    current_slot = new_slot;
                }

                // Recalculate checksum
                config.checksum = calcChecksum(&config);

                // Save to DataFlash if commit flag set
                if(commit) {
                    saveConfigToDataFlash();
                }

                transfer_sequence = 0;
            }
            else {
                // Invalid sequence (out of order or duplicate)
                transfer_sequence = 0;  // Reset state
                buildResponse(command, ERR_INVALID_CMD);
                finalizeResponse();
                return;
            }

            // Build success response
            buildResponse(command, ERR_SUCCESS);
            usb_response[3] = sequence;
            finalizeResponse();
            break;
        }

        case CMD_READ_CONFIG: {
            // Read configuration (multi-packet response)
            // Spec format: [0xF0][cmd][seq][total][data...][checksum]
            memset(usb_response, 0, REPORT_SIZE);
            usb_response[0] = REPORT_ID_CONFIG;
            usb_response[1] = command;
            usb_response[2] = transfer_sequence;
            usb_response[3] = 3; // Total packets

            if(transfer_sequence == 0) {
                // Packet 0: Actions 0-6 (56 bytes) + status
                memcpy(&usb_response[4], &config.slots[0][0], 56);
                usb_response[60] = config.active_slot;
                usb_response[61] = config.version;
                usb_response[62] = 0x00; // Device status (placeholder)
                transfer_sequence = 1;
            }
            else if(transfer_sequence == 1) {
                // Packet 1: Actions 7-13 (56 bytes)
                uint8_t* src = (uint8_t*)&config.slots[0][0];
                memcpy(&usb_response[4], src + 56, 56);
                transfer_sequence = 2;
            }
            else if(transfer_sequence == 2) {
                // Packet 2: Action 14 (8 bytes)
                uint8_t* src = (uint8_t*)&config.slots[0][0];
                memcpy(&usb_response[4], src + 112, 8);
                transfer_sequence = 0;
            }

            finalizeResponse();
            break;
        }

        case CMD_SET_SLOT: {
            // Change active slot
            uint8_t new_slot = report[2];
            uint8_t save = report[3];

            if(new_slot >= MAX_SLOTS) {
                buildResponse(command, ERR_INVALID_SLOT);
                finalizeResponse();
                return;
            }

            current_slot = new_slot;
            config.active_slot = new_slot;

            if(save) {
                config.checksum = calcChecksum(&config);
                saveConfigToDataFlash();
            }

            buildResponse(command, ERR_SUCCESS);
            finalizeResponse();
            break;
        }

        case CMD_FACTORY_RESET: {
            // Reset to factory defaults (requires magic bytes for safety)
            // Magic: 0xDEADBEEF at bytes 2-5 (little-endian)
            if(report[2] != 0xEF || report[3] != 0xBE ||
               report[4] != 0xAD || report[5] != 0xDE) {
                // Wrong magic bytes - reject for safety
                buildResponse(command, ERR_INVALID_CMD);
                finalizeResponse();
                return;
            }

            loadDefaultConfig();
            saveConfigToDataFlash();

            buildResponse(command, ERR_SUCCESS);
            finalizeResponse();
            break;
        }

        case CMD_GET_INFO: {
            // Get device information
            memset(usb_response, 0, REPORT_SIZE);
            usb_response[0] = REPORT_ID_CONFIG;
            usb_response[1] = command;
            usb_response[2] = 2;   // FW major
            usb_response[3] = 0;   // FW minor
            usb_response[4] = 0;   // FW patch
            usb_response[5] = 1;   // Config version
            usb_response[6] = 0;   // Build number low
            usb_response[7] = 0;   // Build number high
            usb_response[8] = 0x00; // Capabilities (none yet)
            usb_response[9] = MAX_SLOTS;
            usb_response[10] = MAX_INPUTS;
            usb_response[11] = TOTAL_ACTIONS; // Max actions (15)
            // Build date: YYYYMMDD (20250126)
            memcpy(&usb_response[12], "20250126", 8);
            // Git hash: 16 chars (placeholder)
            memcpy(&usb_response[20], "v2_arduino______", 16);
            finalizeResponse();
            break;
        }

        default:
            buildResponse(command, ERR_INVALID_CMD);
            finalizeResponse();
            break;
    }
}

// ============================================================================
// Helper Functions
// ============================================================================

uint8_t getActionType(uint8_t control) {
    return control & 0x07;
}

uint8_t getModifiers(uint8_t control) {
    return control & 0xF0;
}

bool getHoldFlag(uint8_t control) {
    return (control & HOLD_FLAG) != 0;
}

// ============================================================================
// DataFlash (EEPROM) Functions
// ============================================================================

// CH552 has 128 bytes of DataFlash for persistent storage
// Addresses: 0-127 (only even addresses are valid)
// Functions provided by CH55xduino: eeprom_read_byte(), eeprom_write_byte()

uint8_t calcChecksum(const Configuration* cfg) {
    uint8_t checksum = 0;
    const uint8_t* data = (const uint8_t*)cfg;

    // XOR all 128 bytes except the checksum byte itself (offset 4)
    for(uint8_t i = 0; i < sizeof(Configuration); i++) {
        if(i != 4) {  // Skip checksum byte at offset 4
            checksum ^= data[i];
        }
    }
    return checksum;
}

void saveConfigToDataFlash() {
    // Calculate checksum
    config.checksum = calcChecksum(&config);

    // ATOMIC WRITE PROTECTION:
    // 1. Clear write complete marker first (reserved_hdr[0] at offset 5)
    //    If power is lost during write, marker will remain cleared
    eeprom_write_byte(5, 0xFF);  // Mark write in progress
    delay(1);  // Ensure write completes

    // 2. Write all configuration data (including magic, checksum, etc.)
    const uint8_t* data = (const uint8_t*)&config;
    for(uint8_t addr = 0; addr < sizeof(Configuration) && addr < 128; addr++) {
        if(addr != 5) {  // Skip write complete marker (written separately)
            eeprom_write_byte(addr, data[addr]);
        }
    }
    delay(1);  // Ensure all writes complete

    // 3. Write complete marker LAST - only written if all data written successfully
    eeprom_write_byte(5, WRITE_COMPLETE_MARKER);  // 0xAA = write complete
}

bool loadConfigFromDataFlash() {
    // Read configuration from DataFlash
    uint8_t* data = (uint8_t*)&config;
    for(uint8_t addr = 0; addr < sizeof(Configuration) && addr < 128; addr++) {
        data[addr] = eeprom_read_byte(addr);
    }

    // FIRST: Check write complete marker (atomic write protection)
    // reserved_hdr[0] is at offset 5 in the structure
    if(config.reserved_hdr[0] != WRITE_COMPLETE_MARKER) {
        return false;  // Incomplete write detected (power loss during save)
    }

    // Validate magic bytes
    if(config.magic != 0x55AA) {
        return false;  // Invalid config
    }

    // Validate version
    if(config.version != 1) {
        return false;  // Incompatible version
    }

    // Validate active slot range
    if(config.active_slot >= MAX_SLOTS) {
        return false;  // Invalid slot number
    }

    // Validate checksum
    uint8_t calc = calcChecksum(&config);
    if(calc != config.checksum) {
        return false;  // Checksum mismatch
    }

    return true;  // Valid config loaded
}

// ============================================================================
// Configuration Management
// ============================================================================

void loadDefaultConfig() {
    // Clear config
    memset(&config, 0, sizeof(config));

    // Set header
    config.magic = 0x55AA;
    config.version = 1;
    config.active_slot = 0;
    config_led_brightness = DEFAULT_LED_BRIGHTNESS;  // Default LED brightness

    // Slot 0: Microsoft Teams
    // BTN_1: Ctrl+Space (PTT - Push to Talk, Hold mode)
    config.slots[0][INPUT_BTN_1].control = ACTION_KEYBOARD | MOD_CTRL | HOLD_FLAG;
    config.slots[0][INPUT_BTN_1].primary = ' '; // Space (ASCII)
    config.slots[0][INPUT_BTN_1].color_idle = COLOR_BLUE;
    config.slots[0][INPUT_BTN_1].color_active = COLOR_BLUE;

    // BTN_2: Ctrl+Shift+M (Mic toggle)
    config.slots[0][INPUT_BTN_2].control = ACTION_KEYBOARD | MOD_CTRL | MOD_SHIFT;
    config.slots[0][INPUT_BTN_2].primary = 'm'; // M key (ASCII)
    config.slots[0][INPUT_BTN_2].color_idle = COLOR_BLUE;
    config.slots[0][INPUT_BTN_2].color_active = COLOR_BLUE;

    // BTN_3: Ctrl+Shift+O (Camera toggle)
    config.slots[0][INPUT_BTN_3].control = ACTION_KEYBOARD | MOD_CTRL | MOD_SHIFT;
    config.slots[0][INPUT_BTN_3].primary = 'o'; // O key (ASCII)
    config.slots[0][INPUT_BTN_3].color_idle = COLOR_BLUE;
    config.slots[0][INPUT_BTN_3].color_active = COLOR_BLUE;

    // ENC_CW: Volume Up (REVERSED - physical CW now increases volume)
    config.slots[0][INPUT_ENC_CW].control = ACTION_MEDIA;
    config.slots[0][INPUT_ENC_CW].primary = 0xE9; // Volume Up low byte
    config.slots[0][INPUT_ENC_CW].secondary = 0x00; // Volume Up high byte

    // ENC_CCW: Volume Down (REVERSED - physical CCW now decreases volume)
    config.slots[0][INPUT_ENC_CCW].control = ACTION_MEDIA;
    config.slots[0][INPUT_ENC_CCW].primary = 0xEA; // Volume Down low byte
    config.slots[0][INPUT_ENC_CCW].secondary = 0x00; // Volume Down high byte

    // Slot 1: Media Control (YouTube/Media Player)
    // BTN_1: 'l' key (YouTube seek forward 10s)
    config.slots[1][INPUT_BTN_1].control = ACTION_KEYBOARD;
    config.slots[1][INPUT_BTN_1].primary = 'l'; // L key (ASCII)
    config.slots[1][INPUT_BTN_1].color_idle = COLOR_RED;
    config.slots[1][INPUT_BTN_1].color_active = COLOR_RED;

    // BTN_2: 'j' key (YouTube seek backward 10s)
    config.slots[1][INPUT_BTN_2].control = ACTION_KEYBOARD;
    config.slots[1][INPUT_BTN_2].primary = 'j'; // J key (ASCII)
    config.slots[1][INPUT_BTN_2].color_idle = COLOR_RED;
    config.slots[1][INPUT_BTN_2].color_active = COLOR_RED;

    // BTN_3: Play/Pause
    config.slots[1][INPUT_BTN_3].control = ACTION_MEDIA;
    config.slots[1][INPUT_BTN_3].primary = 0xCD; // Play/Pause
    config.slots[1][INPUT_BTN_3].secondary = 0x00;
    config.slots[1][INPUT_BTN_3].color_idle = COLOR_RED;
    config.slots[1][INPUT_BTN_3].color_active = COLOR_RED;

    // Copy volume controls from slot 0
    config.slots[1][INPUT_ENC_CW] = config.slots[0][INPUT_ENC_CW];
    config.slots[1][INPUT_ENC_CCW] = config.slots[0][INPUT_ENC_CCW];

    // Slot 2: Visual Studio 2022
    // BTN_1: Ctrl+Shift+F (Find in Files)
    config.slots[2][INPUT_BTN_1].control = ACTION_KEYBOARD | MOD_CTRL | MOD_SHIFT;
    config.slots[2][INPUT_BTN_1].primary = 'f'; // F key (ASCII)
    config.slots[2][INPUT_BTN_1].color_idle = COLOR_GREEN;
    config.slots[2][INPUT_BTN_1].color_active = COLOR_GREEN;

    // BTN_2: Ctrl+M (Collapse/Expand - simplified from v1's Ctrl+M,Ctrl+O sequence)
    config.slots[2][INPUT_BTN_2].control = ACTION_KEYBOARD | MOD_CTRL;
    config.slots[2][INPUT_BTN_2].primary = 'm'; // M key (ASCII)
    config.slots[2][INPUT_BTN_2].color_idle = COLOR_GREEN;
    config.slots[2][INPUT_BTN_2].color_active = COLOR_GREEN;

    // BTN_3: Ctrl+Shift+B (Build Solution)
    config.slots[2][INPUT_BTN_3].control = ACTION_KEYBOARD | MOD_CTRL | MOD_SHIFT;
    config.slots[2][INPUT_BTN_3].primary = 'b'; // B key (ASCII)
    config.slots[2][INPUT_BTN_3].color_idle = COLOR_GREEN;
    config.slots[2][INPUT_BTN_3].color_active = COLOR_GREEN;

    // Copy volume controls
    config.slots[2][INPUT_ENC_CW] = config.slots[0][INPUT_ENC_CW];
    config.slots[2][INPUT_ENC_CCW] = config.slots[0][INPUT_ENC_CCW];

    // Set write complete marker (atomic write protection)
    config.reserved_hdr[0] = WRITE_COMPLETE_MARKER;
}

// ============================================================================
// Action Execution
// ============================================================================

void executeAction(const Action* action, bool press) {
    uint8_t type = getActionType(action->control);
    uint8_t modifiers = getModifiers(action->control);
    bool hold = getHoldFlag(action->control);

    switch(type) {
        case ACTION_KEYBOARD:
            // Apply modifiers
            if(modifiers & MOD_CTRL)  Keyboard_press(KEY_LEFT_CTRL);
            if(modifiers & MOD_SHIFT) Keyboard_press(KEY_LEFT_SHIFT);
            if(modifiers & MOD_ALT)   Keyboard_press(KEY_LEFT_ALT);
            if(modifiers & MOD_GUI)   Keyboard_press(KEY_LEFT_GUI);

            if(press) {
                // Press the key
                if(action->primary != 0) {
                    Keyboard_press(action->primary);
                }

                if(!hold) {
                    // Normal key: release immediately
                    delay(1);
                    Keyboard_releaseAll();
                }
            } else if(hold) {
                // Hold mode: release on button release
                Keyboard_releaseAll();
            }
            break;

        case ACTION_MEDIA:
            {
                uint16_t media_code = (action->secondary << 8) | action->primary;

                if(press) {
                    // Apply modifiers before media key
                    if(modifiers & MOD_CTRL)  Keyboard_press(KEY_LEFT_CTRL);
                    if(modifiers & MOD_SHIFT) Keyboard_press(KEY_LEFT_SHIFT);
                    if(modifiers & MOD_ALT)   Keyboard_press(KEY_LEFT_ALT);
                    if(modifiers & MOD_GUI)   Keyboard_press(KEY_LEFT_GUI);

                    Consumer_press(media_code);
                    if(!hold) {
                        delay(10);
                        Consumer_release(media_code);
                        // Release modifiers after media key
                        Keyboard_releaseAll();
                    }
                } else if(hold) {
                    Consumer_release(media_code);
                    // Release modifiers on button release
                    Keyboard_releaseAll();
                }
            }
            break;

        case ACTION_MOUSE:
            if(press) {
                // Apply modifiers before mouse operations
                if(modifiers & MOD_CTRL)  Keyboard_press(KEY_LEFT_CTRL);
                if(modifiers & MOD_SHIFT) Keyboard_press(KEY_LEFT_SHIFT);
                if(modifiers & MOD_ALT)   Keyboard_press(KEY_LEFT_ALT);
                if(modifiers & MOD_GUI)   Keyboard_press(KEY_LEFT_GUI);

                if(!hold) {
                    // Normal mode: Support multiple clicks (double-click, triple-click, etc.)
                    uint8_t clicks = (action->secondary == 0) ? 1 : action->secondary;

                    for(uint8_t c = 0; c < clicks; c++) {
                        // Press buttons
                        if(action->primary & 0x01) Mouse_press(MOUSE_LEFT);
                        if(action->primary & 0x02) Mouse_press(MOUSE_RIGHT);
                        if(action->primary & 0x04) Mouse_press(MOUSE_MIDDLE);

                        delay(50);

                        // Release buttons
                        if(action->primary & 0x01) Mouse_release(MOUSE_LEFT);
                        if(action->primary & 0x02) Mouse_release(MOUSE_RIGHT);
                        if(action->primary & 0x04) Mouse_release(MOUSE_MIDDLE);

                        // Delay between clicks (not after last click)
                        if(c < clicks - 1) {
                            delay(100);
                        }
                    }

                    // Release modifiers after mouse operations
                    Keyboard_releaseAll();
                } else {
                    // Hold mode: Press and hold buttons while button is pressed
                    if(action->primary & 0x01) Mouse_press(MOUSE_LEFT);
                    if(action->primary & 0x02) Mouse_press(MOUSE_RIGHT);
                    if(action->primary & 0x04) Mouse_press(MOUSE_MIDDLE);
                }
            } else if(hold) {
                // Hold mode: Release on button release
                if(action->primary & 0x01) Mouse_release(MOUSE_LEFT);
                if(action->primary & 0x02) Mouse_release(MOUSE_RIGHT);
                if(action->primary & 0x04) Mouse_release(MOUSE_MIDDLE);

                // Release modifiers
                Keyboard_releaseAll();
            }
            break;

        case ACTION_SCROLL:
            {
                // Spec: primary=0 means scroll up, primary=1 means scroll down
                // Mouse_scroll: positive values scroll up, negative scroll down
                int8_t direction = (action->primary == 0) ? 1 : -1;
                uint8_t lines = action->secondary;
                if(lines == 0) lines = 1;  // Default to 1 line

                if(press) {
                    // Apply modifiers before scroll operations
                    if(modifiers & MOD_CTRL)  Keyboard_press(KEY_LEFT_CTRL);
                    if(modifiers & MOD_SHIFT) Keyboard_press(KEY_LEFT_SHIFT);
                    if(modifiers & MOD_ALT)   Keyboard_press(KEY_LEFT_ALT);
                    if(modifiers & MOD_GUI)   Keyboard_press(KEY_LEFT_GUI);

                    if(!hold) {
                        // Normal mode: Scroll specified number of lines
                        for(uint8_t i = 0; i < lines; i++) {
                            Mouse_scroll(direction);
                            delay(10);  // Small delay between scroll events
                        }

                        // Release modifiers after scroll
                        Keyboard_releaseAll();
                    } else {
                        // Hold mode: Single scroll on press, continue in loop
                        Mouse_scroll(direction);
                    }
                } else if(hold) {
                    // Hold mode: Release modifiers on button release
                    Keyboard_releaseAll();
                }
            }
            break;
    }
}

// ============================================================================
// Input Handling
// ============================================================================

void readButtons() {
    // Read button states
    btn_states[0] = !digitalRead(PIN_BTN_1); // Active low
    btn_states[1] = !digitalRead(PIN_BTN_2);
    btn_states[2] = !digitalRead(PIN_BTN_3);

    // Process each button
    for(uint8_t i = 0; i < 3; i++) {
        if(btn_states[i] && !btn_last[i]) {
            // Button pressed
            btn_press_time[i] = millis();

            const Action* action = &config.slots[current_slot][i];
            executeAction(action, true);

            // Update LED
            led_colors[i] = action->color_active;
        }
        else if(!btn_states[i] && btn_last[i]) {
            // Button released
            const Action* action = &config.slots[current_slot][i];

            if(getHoldFlag(action->control)) {
                executeAction(action, false);
            }

            // Update LED
            led_colors[i] = action->color_idle;
        }

        btn_last[i] = btn_states[i];
    }
}

void readEncoder() {
    // Read encoder pins
    bool enc_a = digitalRead(PIN_ENC_A);
    bool enc_b = digitalRead(PIN_ENC_B);

    // Gray code state
    uint8_t new_state = (enc_a ? 2 : 0) | (enc_b ? 1 : 0);

    // State transition table for quadrature decoding
    const int8_t enc_table[] = {0,-1,1,0,1,0,0,-1,-1,0,0,1,0,1,-1,0};
    uint8_t index = (enc_last << 2) | new_state;
    int8_t direction = enc_table[index];

    enc_position += direction;

    // Generate events on full detent (4 transitions)
    if(enc_position >= 4) {
        // Clockwise
        if(slot_switch_mode) {
            selected_slot = (selected_slot + 1) % MAX_SLOTS;
        } else {
            const Action* action = &config.slots[current_slot][INPUT_ENC_CW];
            executeAction(action, true);
        }
        enc_position = 0;
    }
    else if(enc_position <= -4) {
        // Counter-clockwise
        if(slot_switch_mode) {
            selected_slot = (selected_slot + MAX_SLOTS - 1) % MAX_SLOTS;
        } else {
            const Action* action = &config.slots[current_slot][INPUT_ENC_CCW];
            executeAction(action, true);
        }
        enc_position = 0;
    }

    enc_last = new_state;

    // Encoder button
    bool enc_btn = !digitalRead(PIN_ENC_BTN);

    if(enc_btn && !enc_btn_pressed) {
        // Button pressed
        enc_btn_press_time = millis();
        enc_btn_pressed = true;
    }
    else if(!enc_btn && enc_btn_pressed) {
        // Button released
        uint32_t press_duration = millis() - enc_btn_press_time;

        if(press_duration > 500) {
            // Long press - exit slot switch mode
            if(slot_switch_mode) {
                slot_switch_mode = false;
                current_slot = selected_slot;
                config.active_slot = current_slot;

                // Save slot change to DataFlash
                saveConfigToDataFlash();

                // Update LED colors for new slot
                for(uint8_t i = 0; i < 3; i++) {
                    led_colors[i] = config.slots[current_slot][i].color_idle;
                }
            }
        }

        enc_btn_pressed = false;
    }
    else if(enc_btn_pressed) {
        // Button still held
        uint32_t press_duration = millis() - enc_btn_press_time;

        if(press_duration > 500 && !slot_switch_mode) {
            // Enter slot switch mode
            slot_switch_mode = true;
            selected_slot = current_slot;
        }
    }
}

// ============================================================================
// LED Control - WS2812 Output
// ============================================================================

void updateLEDs() {
    uint8_t r, g, b;
    uint8_t brightness = config_led_brightness;

    if(slot_switch_mode) {
        // Show slot selection (all green to avoid confusion)
        led_colors[0] = (selected_slot == 0) ? COLOR_GREEN : COLOR_OFF;
        led_colors[1] = (selected_slot == 1) ? COLOR_GREEN : COLOR_OFF;
        led_colors[2] = (selected_slot == 2) ? COLOR_GREEN : COLOR_OFF;
    } else {
        // Show configured colors for idle buttons
        for(uint8_t i = 0; i < 3; i++) {
            if(!btn_states[i]) {
                led_colors[i] = config.slots[current_slot][i].color_idle;
            }
        }
    }

    // Update WS2812 LEDs with actual colors
    for(uint8_t i = 0; i < 3; i++) {
        getColor(led_colors[i], brightness, &r, &g, &b);
        WS2812_setPixel(i, r, g, b);
    }

    WS2812_update();
}

// ============================================================================
// Setup and Loop
// ============================================================================

void setup() {
    // Check for bootloader entry on power-up (BEFORE USB init)
    pinMode(PIN_ENC_BTN, INPUT_PULLUP);

    if(!digitalRead(PIN_ENC_BTN)) {
        // Encoder button held down on power-up - enter bootloader mode

        // Visual feedback: Set LEDs to Cyan/Blue/Magenta
        WS2812_init();
        WS2812_setPixel(0, 0, 255, 255);   // Cyan
        WS2812_setPixel(1, 0, 0, 255);     // Blue
        WS2812_setPixel(2, 255, 0, 255);   // Magenta
        WS2812_update();
        delay(100);  // Brief display

        // Jump to bootloader at 0x3800
        USB_CTRL = 0;    // Disable USB
        EA = 0;          // Disable interrupts
        TMOD = 0;        // Reset timer mode
        __asm__ ("lcall #0x3800");  // Jump to bootloader
    }

    // Initialize USB
    USBInit();

    // Configure remaining pins (PIN_ENC_BTN already configured above)
    pinMode(PIN_BTN_1, INPUT_PULLUP);
    pinMode(PIN_BTN_2, INPUT_PULLUP);
    pinMode(PIN_BTN_3, INPUT_PULLUP);
    pinMode(PIN_ENC_A, INPUT_PULLUP);
    pinMode(PIN_ENC_B, INPUT_PULLUP);

    // Initialize WS2812 LEDs
    WS2812_init();

    // Read initial encoder state
    enc_last = (digitalRead(PIN_ENC_A) ? 2 : 0) | (digitalRead(PIN_ENC_B) ? 1 : 0);

    // Load configuration from DataFlash
    if(!loadConfigFromDataFlash()) {
        // No valid config - load and save defaults
        loadDefaultConfig();
        saveConfigToDataFlash();
    }
    current_slot = config.active_slot;

    // Initialize LED colors
    for(uint8_t i = 0; i < 3; i++) {
        led_colors[i] = config.slots[current_slot][i].color_idle;
    }

    // Show initial LED state
    updateLEDs();
}

void loop() {
    // Read inputs
    readButtons();
    readEncoder();

    // Check for bootloader entry (all 4 buttons pressed simultaneously)
    bool enc_btn = !digitalRead(PIN_ENC_BTN);
    if(btn_states[0] && btn_states[1] && btn_states[2] && enc_btn) {
        // All buttons pressed - erase config and enter bootloader mode

        // ERASE CONFIG: Invalidate magic bytes to force defaults on next boot
        eeprom_write_byte(0, 0x00);  // Clear magic byte low
        eeprom_write_byte(1, 0x00);  // Clear magic byte high
        eeprom_write_byte(5, 0xFF);  // Clear write complete marker
        delay(10);  // Ensure writes complete

        // Visual feedback: Flash all LEDs white to indicate config erased
        WS2812_setPixel(0, 100, 100, 100);  // White
        WS2812_setPixel(1, 100, 100, 100);  // White
        WS2812_setPixel(2, 100, 100, 100);  // White
        WS2812_update();
        delay(200);  // Brief flash

        // Enter bootloader
        USB_CTRL = 0;    // Disable USB
        EA = 0;          // Disable interrupts
        TMOD = 0;        // Reset timer mode
        __asm__ ("lcall #0x3800");  // Jump to bootloader
    }

    // Update LEDs
    updateLEDs();

    // USB polling
    delay(5); // Simple 5ms loop for now
}