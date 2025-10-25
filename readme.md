# CH552G Mini Keyboard v2.0

PC-configurable USB mini keyboard with 3 buttons and rotary encoder, powered by the CH552G microcontroller.

![Keyboard](img/keyboard.jpeg)

## Features

### Hardware
- **3 mechanical buttons** with individual WS2812 RGB LEDs
- **Rotary encoder** with push button
- **CH552G microcontroller** (14KB Flash, 1.25KB RAM, 128B DataFlash)
- **USB HID** device (Keyboard + Mouse + Media Control)

### Firmware v2.0 Highlights
- **PC-configurable via USB HID** (no recompilation needed)
- **3 configuration slots** - Teams, Media Control, Visual Studio
- **Persistent storage** in DataFlash (survives power cycles)
- **Multi-action support**:
  - Keyboard shortcuts with modifiers (Ctrl, Shift, Alt, Win/Cmd)
  - Media keys (volume, play/pause)
  - Mouse actions (clicks, scroll)
  - Push-to-Talk (PTT) hold mode
- **WS2812 RGB LED feedback** with configurable colors
- **Atomic DataFlash writes** (power-loss protection)
- **USB bootloader** for easy firmware updates

## Quick Start

### Default Configuration

The keyboard comes with 3 pre-configured slots optimized for different workflows:

#### **Slot 0: Microsoft Teams** (Blue LEDs)
| Button | Action |
|--------|--------|
| BTN_1 (furthest) | Push-to-Talk: `Ctrl+Space` (hold) |
| BTN_2 (middle) | Mic Toggle: `Ctrl+Shift+M` |
| BTN_3 (closest) | Camera Toggle: `Ctrl+Shift+O` |
| Encoder CW | Volume Up |
| Encoder CCW | Volume Down |

#### **Slot 1: Media Control** (Red LEDs)
| Button | Action |
|--------|--------|
| BTN_1 (furthest) | Seek Forward 10s (YouTube `L` key) |
| BTN_2 (middle) | Seek Backward 10s (YouTube `J` key) |
| BTN_3 (closest) | Play/Pause (media key) |
| Encoder CW | Volume Up |
| Encoder CCW | Volume Down |

#### **Slot 2: Visual Studio 2022** (Green LEDs)
| Button | Action |
|--------|--------|
| BTN_1 (furthest) | Find in Files: `Ctrl+Shift+F` |
| BTN_2 (middle) | Collapse/Expand: `Ctrl+M` |
| BTN_3 (closest) | Build Solution: `Ctrl+Shift+B` |
| Encoder CW | Volume Up |
| Encoder CCW | Volume Down |

### Switching Slots
1. **Press and hold** encoder button (>500ms)
2. **Rotate encoder** to select slot (LEDs light up green)
3. **Release button** to activate
4. Selection is saved to DataFlash automatically

### Resetting to Defaults
1. **Press all 4 buttons simultaneously** (BTN1 + BTN2 + BTN3 + Encoder)
2. **LEDs flash white** (config erased)
3. **Device enters bootloader mode**
4. **On next boot**: Defaults are restored

## Building and Flashing

### Prerequisites
1. **Arduino IDE** with CH55xDuino board support
2. Add board manager URL in Preferences:
   ```
   https://raw.githubusercontent.com/DeqingSun/ch55xduino/ch55xduino/package_ch55xduino_mcs51_index.json
   ```
3. Install **CH55xDuino** board from Board Manager

### Board Settings (IMPORTANT!)
In Arduino IDE, configure:
- **Board**: CH55xDuino → CH552 Board
- **Clock**: 16MHz (internal) 3.5V or 5V
- **Upload Method**: USB
- **USB Settings**: USER CODE w/148B USB RAM ⚠️ **CRITICAL**
- **Bootloader**: P3.6 (D+) Pull up

### First-Time Flashing (Hardware Method)

**⚠️ FIRST TIME ONLY** - You need to short R12 to enter bootloader:

1. **Locate R12** on the bottom of the PCB (near the CH552G chip)

   ![Short R12](img/short.jpeg)

2. **Short R12** with a jumper wire or tweezers

3. **Connect USB** to your PC while holding the short

4. **Verify bootloader mode**:
   - On Linux: `lsusb` should show "WCH CH55x Bootloader"
   - On Windows: Device Manager shows "CH55x USB Device"

5. In Arduino IDE, click **Upload**

6. **Remove the short** from R12

7. **Disconnect and reconnect USB**

**✅ FROM NOW ON**: You can enter bootloader without shorting R12!

### Subsequent Updates (Software Method)

After the first flash, use one of these methods:

**Method 1: Encoder Button at Power-up**
1. **Hold encoder button**
2. **Connect USB** while holding
3. **LEDs show Cyan/Blue/Magenta** (bootloader mode)
4. Upload firmware from Arduino IDE

**Method 2: All Buttons Simultaneously**
1. Device powered and running
2. **Press all 4 buttons** (BTN1 + BTN2 + BTN3 + Encoder)
3. **LEDs flash white** (config erased, entering bootloader)
4. Upload firmware from Arduino IDE

### Compile and Upload
```bash
# Open in Arduino IDE
open ch552g_keyboard_v2.ino

# Or compile with arduino-cli
arduino-cli compile --fqbn CH55xDuino:mcs51:ch552 ch552g_keyboard_v2
arduino-cli upload --fqbn CH55xDuino:mcs51:ch552 ch552g_keyboard_v2
```

## Memory Usage

Current firmware footprint (as of 2025-10-25):
```
Flash:  12,448 / 14,336 bytes (86%)
RAM:       420 /    876 bytes (47%)
DataFlash: 128 /    128 bytes (100%)
```

Still has reasonable headroom for minor enhancements.

## PC Configuration Application

**⚠️ WORK IN PROGRESS**: A Windows configuration application has been implemented and is currently in testing!

**Location**: `CH552G_PadConfig_Win/`

### Features Implemented ✅

- ✅ **Visual configuration UI** with 3-slot tabbed interface
- ✅ **Real-time device detection** (no drivers needed)
- ✅ **Action editor** with full customization:
  - Action types: None, Keyboard, Media, Mouse, Scroll
  - Modifier keys: Ctrl, Shift, Alt, Win/Cmd
  - Hold mode (Push-to-Talk style)
  - LED color selection (8-color palette)
- ✅ **Profile management** (save/load as JSON files)
- ✅ **LED brightness control** (0-255 slider)
- ✅ **Write configuration to device** (no reflashing required)
- ✅ **Set active slot** from app
- ✅ **Status logging** for debugging

### Technology Stack

- **Platform**: Windows 10/11 (.NET 8.0 WPF)
- **USB Library**: HidSharp 2.6.4
- **Architecture**: MVVM pattern
- **Profiles**: JSON format

### Quick Start

1. **Build the app** (requires Visual Studio 2022 or .NET 8.0 SDK):
   ```bash
   cd CH552G_PadConfig_Win
   dotnet restore
   dotnet build
   dotnet run
   ```

2. **Connect keyboard** - App auto-detects device on launch

3. **Configure actions** - Click "Edit" button on any action row

4. **Adjust LED brightness** - Use slider at bottom

5. **Apply to Device** - Writes configuration to keyboard

6. **Save Profile** - Export configuration as JSON for backup

### Testing Status

✅ **USB Communication**: Working
✅ **Device Detection**: Working
✅ **Device Info Read**: Working
✅ **Configuration Write**: Working (colors confirmed on hardware!)
⚠️ **Pending Tests**: Full action testing, slot switching, various action types

### Known Limitations

- **Windows-only** (WPF is Windows-specific)
- **Write-only** (doesn't read current config from device)
- **No firmware updates** (use Arduino IDE for flashing)

### Screenshot

![Configuration App](img/app-screenshot.png)
*Screenshot coming soon*

### For More Details

See `CLAUDE.md` for complete implementation details, architecture, and USB protocol fixes.

## Architecture

### DataFlash Memory Layout (128 bytes)
```
┌──────────────────────────────────────────────────┐
│ Header (8 bytes)                                 │
├─────────┬────────┬─────────┬──────────┬─────────┤
│ Magic   │Version │ Active  │ Checksum │Reserved │
│ (0x55AA)│   (1)  │ Slot    │  (XOR)   │  (3B)   │
├─────────┴────────┴─────────┴──────────┴─────────┤
│ Slot 0: 5 actions × 8 bytes = 40 bytes           │
├───────────────────────────────────────────────────┤
│ Slot 1: 5 actions × 8 bytes = 40 bytes           │
├───────────────────────────────────────────────────┤
│ Slot 2: 5 actions × 8 bytes = 40 bytes           │
└───────────────────────────────────────────────────┘
Total: 128 bytes (100% utilization)
```

### Action Structure (8 bytes, Bit-Packed)
```
Byte 0: Control Byte
┌───────────────┬───┬───────────┐
│  Modifiers    │Hld│ ActionType│
│ (GUI|SFT|ALT| │   │  (0-4)    │
│     CTRL)     │   │           │
└───────────────┴───┴───────────┘

Byte 1: Primary value (key code, media code, etc.)
Byte 2: Secondary value (click count, scroll amount)
Byte 3: LED color (idle state)
Byte 4: LED color (active state)
Bytes 5-7: Reserved for future use
```

### Action Types
- `0x0` - None (disabled)
- `0x1` - Keyboard (with optional modifiers)
- `0x2` - Media keys (volume, play/pause)
- `0x3` - Mouse clicks (with optional modifiers)
- `0x4` - Mouse scroll (with optional modifiers)

### Four-Layer Validation
Configuration is validated on boot:
1. **Magic bytes** == `0x55AA`
2. **Version** == `1`
3. **Active slot** < `3`
4. **Checksum** matches (XOR of all bytes)

If any validation fails → load defaults and save to DataFlash.

### Atomic Write Protection
Power-loss protection during DataFlash writes:
1. Clear write marker → **write in progress**
2. Write all 127 bytes of config
3. Set write marker (`0xAA`) → **write complete**

On load: If marker ≠ `0xAA` → incomplete write → load defaults.

## USB HID Protocol

The device presents **4 HID collections**:
1. **Keyboard** (Report ID 0x01)
2. **Mouse** (Report ID 0x02)
3. **Consumer Control** (Report ID 0x03) - Media keys
4. **Vendor Configuration** (Report ID 0xF0) - Feature Reports

### Feature Report Commands (Report ID 0xF0)
| Command | Description |
|---------|-------------|
| `0x01` | READ_CONFIG - Read full configuration (3 packets) |
| `0x02` | WRITE_ACTION - Write single action |
| `0x03` | WRITE_ALL - Write complete configuration (3 packets) |
| `0x04` | GET_INFO - Get device info (FW version, capabilities) |
| `0x05` | SET_SLOT - Change active slot |
| `0x06` | FACTORY_RESET - Reset to defaults (requires magic bytes) |

All packets use **XOR checksum** in the last byte for data integrity.

## Critical Bug Fixes (2025-01-26)

After comprehensive code review, the following critical bugs were identified and fixed:

1. **ACTION_MEDIA missing modifier support** ✅ Fixed
2. **Multi-packet sequence not validated** ✅ Fixed (prevents memory corruption)
3. **Atomic DataFlash write missing** ✅ Fixed (power-loss protection)
4. **Report checksum accessed before validation** ✅ Fixed (security)

**Total impact**: +144 bytes flash, **production ready** ✅

## Pinout Reference

| Component | Pin | Note |
|-----------|-----|------|
| BTN_1 | P1.1 (pin 11) | Furthest from encoder |
| BTN_2 | P1.7 (pin 17) | Middle |
| BTN_3 | P1.6 (pin 16) | Closest to encoder |
| Encoder Button | P3.3 (pin 33) | Push switch |
| Encoder A | P3.1 (pin 31) | Quadrature A |
| Encoder B | P3.0 (pin 30) | Quadrature B |
| WS2812 LEDs | P3.4 (pin 34) | Data line |

## Troubleshooting

### Device Not Enumerating
- Check USB Settings: **USER CODE w/148B USB RAM** is required
- Verify bootloader setting: P3.6 (D+) Pull up
- Try different USB cable/port

### Configuration Not Persisting
- Check if DataFlash validation passes (magic bytes, checksum)
- Verify `saveConfigToDataFlash()` is called after changes
- Try factory reset (all 4 buttons)

### LEDs Not Working
- Check LED brightness setting (default: 10/255 = ~4%)
- Verify WS2812 wiring on P3.4
- Test with `WS2812_setPixel(0, 100, 100, 100)` in setup()

### Bootloader Not Accessible
- First time: **Must short R12** (hardware method)
- After first flash: Use encoder button or all-buttons method
- If stuck: Short R12 again to force bootloader

## Credits

This project is based on the original **[ch552g_mini_keyboard](https://github.com/eccherda/ch552g_mini_keyboard)** by eccherda.

**v2.0 enhancements**:
- Complete rewrite with USB HID Feature Report configuration
- Apollo-style bit-packed configuration for maximum efficiency
- WS2812 RGB LED support with configurable colors
- Atomic DataFlash writes with power-loss protection
- Multi-packet transfer protocol with sequence validation
- Production-ready firmware with comprehensive bug fixes

Special thanks to:
- **DeqingSun** for [CH55xDuino](https://github.com/DeqingSun/ch55xduino) Arduino framework
- **WCH** for the CH552G microcontroller
- **Original hardware design** from AliExpress vendors

## Resources

- [CH55xDuino on GitHub](https://github.com/DeqingSun/ch55xduino)
- [CH552G Datasheet](http://www.wch-ic.com/downloads/file/309.html)
- [RGB Macropad Custom Firmware](https://hackaday.io/project/189914-rgb-macropad-custom-firmware)
- [CH552G Macropad Plus](https://oshwlab.com/wagiminator/ch552g-macropad-plus)

## License

![Creative Commons BY-SA 3.0](https://i.creativecommons.org/l/by-sa/3.0/88x31.png)

This work is licensed under [Creative Commons Attribution-ShareAlike 3.0 Unported License](http://creativecommons.org/licenses/by-sa/3.0/).

Based on original work by eccherda, enhanced and expanded for v2.0.

---

**Firmware Version**: 2.0.3
**PC App Version**: 1.0.0 (WIP)
**Last Updated**: 2025-10-25
**Status**:
- Firmware: ✅ Production Ready & Tested
- PC App: ⚠️ Work in Progress (USB communication working, hardware tests ongoing)
