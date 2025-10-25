using HidSharp;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.Services;

/// <summary>
/// USB HID communication layer for CH552G keyboard
/// Handles all Feature Report communication via HidSharp
/// </summary>
public class HidCommunicator
{
    private const int VID = 0x1209;
    private const int PID = 0xC55D;
    private const byte REPORT_ID = 0xF0;
    private const int REPORT_SIZE = 64; // Total size including Report ID at byte 0

    // Commands matching firmware protocol
    private const byte CMD_READ_CONFIG = 0x01;
    private const byte CMD_WRITE_ACTION = 0x02;
    private const byte CMD_WRITE_ALL = 0x03;
    private const byte CMD_GET_INFO = 0x04;
    private const byte CMD_SET_SLOT = 0x05;
    private const byte CMD_FACTORY_RESET = 0x06;

    private readonly DebugLogger? _logger;
    private HidDevice? _device;

    public bool IsConnected => _device != null;

    public HidCommunicator(DebugLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Find and connect to CH552G keyboard (vendor configuration collection)
    /// </summary>
    public bool FindDevice()
    {
        try
        {
            _logger?.Log("Searching for CH552G keyboard...");

            var loader = new HidDeviceLoader();
            var allDevices = loader.GetDevices(VID, PID).ToArray();

            if (allDevices.Length == 0)
            {
                _logger?.LogError("Device not found (VID:0x1209 PID:0xC55D)");
                return false;
            }

            _logger?.Log($"Found {allDevices.Length} HID collection(s)");

            // Find the vendor configuration collection (the one with Feature Report support)
            // The vendor collection should have MaxFeatureReportLength >= 64
            foreach (var device in allDevices)
            {
                try
                {
                    var maxFeatureLength = device.GetMaxFeatureReportLength();
                    var path = device.DevicePath;

                    _logger?.Log($"  Collection: {path.Substring(path.LastIndexOf("col", StringComparison.OrdinalIgnoreCase))}");
                    _logger?.Log($"    MaxFeatureReportLength: {maxFeatureLength}");

                    // Vendor collection should support 64-byte feature reports
                    if (maxFeatureLength >= 64)
                    {
                        _device = device;
                        _logger?.LogSuccess($"Selected vendor configuration collection");
                        _logger?.Log($"  Product: {_device.GetProductName()}");
                        _logger?.Log($"  Manufacturer: {_device.GetManufacturer()}");
                        _logger?.Log($"  Serial: {_device.GetSerialNumber()}");
                        _logger?.Log($"  Path: {_device.DevicePath}");
                        return true;
                    }
                }
                catch
                {
                    // Skip collections that don't support feature reports
                    continue;
                }
            }

            _logger?.LogError("Vendor configuration collection not found");
            _logger?.LogError("Device may not support PC configuration");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Device enumeration failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get device information (firmware version, capabilities)
    /// </summary>
    public DeviceInfo? GetDeviceInfo()
    {
        if (_device == null)
        {
            _logger?.LogError("No device connected");
            return null;
        }

        try
        {
            _logger?.Log("Requesting device info...");

            // Prepare request (64 bytes: Report ID at byte 0, data at bytes 1-62, checksum at byte 63)
            byte[] request = new byte[REPORT_SIZE];
            request[0] = REPORT_ID;
            request[1] = CMD_GET_INFO;
            request[63] = CalculateChecksum(request);

            // Send and receive
            byte[] response = new byte[REPORT_SIZE];
            if (!SendFeatureReport(request, response))
                return null;

            // Parse response
            var info = new DeviceInfo
            {
                FirmwareMajor = response[2],
                FirmwareMinor = response[3],
                FirmwarePatch = response[4],
                ConfigVersion = response[5],
                BuildNumber = (ushort)(response[6] | (response[7] << 8)),
                Capabilities = response[8],
                MaxSlots = response[9],
                MaxInputs = response[10],
                TotalActions = response[11],
                BuildDate = System.Text.Encoding.ASCII.GetString(response, 12, 8).Trim('\0'),
                GitHash = System.Text.Encoding.ASCII.GetString(response, 20, 16).Trim('\0')
            };

            _logger?.LogSuccess($"Device info: {info}");
            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get device info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Write a single action to the device
    /// </summary>
    public bool WriteAction(byte slot, byte input, ActionConfig action)
    {
        if (_device == null)
        {
            _logger?.LogError("No device connected");
            return false;
        }

        if (slot >= 3)
        {
            _logger?.LogError($"Invalid slot {slot} (must be 0-2)");
            return false;
        }

        if (input >= 5)
        {
            _logger?.LogError($"Invalid input {input} (must be 0-4)");
            return false;
        }

        try
        {
            // Prepare request (64 bytes: Report ID at byte 0, data at bytes 1-62, checksum at byte 63)
            byte[] request = new byte[REPORT_SIZE];
            request[0] = REPORT_ID;
            request[1] = CMD_WRITE_ACTION;
            request[2] = slot;
            request[3] = input;

            // Copy action bytes
            var actionBytes = action.ToBytes();
            Array.Copy(actionBytes, 0, request, 4, 8);

            // Checksum
            request[63] = CalculateChecksum(request);

            // Send
            byte[] response = new byte[REPORT_SIZE];
            if (!SendFeatureReport(request, response))
                return false;

            // Check response status
            if (response[2] != 0x00)
            {
                _logger?.LogError($"Device returned error code: 0x{response[3]:X2}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to write action: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Write complete configuration to device (all 15 actions)
    /// Uses 3-packet transfer protocol
    /// </summary>
    public bool WriteAllConfig(DeviceConfiguration config)
    {
        if (_device == null)
        {
            _logger?.LogError("No device connected");
            return false;
        }

        try
        {
            _logger?.Log("Writing full configuration (15 actions)...");

            var allActions = config.GetAllActions();

            // Write all 15 actions individually (simpler than multi-packet)
            for (byte slot = 0; slot < 3; slot++)
            {
                for (byte input = 0; input < 5; input++)
                {
                    int actionIndex = (slot * 5) + input;
                    var action = allActions[actionIndex];

                    _logger?.Log($"  Writing Slot {slot}, {SlotConfig.GetInputName(input)}: {action.GetDescription()}");

                    if (!WriteAction(slot, input, action))
                    {
                        _logger?.LogError($"Failed at Slot {slot}, Input {input}");
                        return false;
                    }

                    // Small delay to avoid overwhelming device
                    Thread.Sleep(20);
                }
            }

            _logger?.LogSuccess("Configuration written successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to write configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set the active slot on the device
    /// </summary>
    public bool SetActiveSlot(byte slotIndex)
    {
        if (_device == null)
        {
            _logger?.LogError("No device connected");
            return false;
        }

        if (slotIndex >= 3)
        {
            _logger?.LogError($"Invalid slot {slotIndex} (must be 0-2)");
            return false;
        }

        try
        {
            _logger?.Log($"Setting active slot to {slotIndex}...");

            byte[] request = new byte[REPORT_SIZE];
            request[0] = REPORT_ID;
            request[1] = CMD_SET_SLOT;
            request[2] = slotIndex;
            request[3] = 0x01; // Save to DataFlash
            request[63] = CalculateChecksum(request);

            byte[] response = new byte[REPORT_SIZE];
            if (!SendFeatureReport(request, response))
                return false;

            if (response[2] != 0x00)
            {
                _logger?.LogError($"Failed to set slot: error 0x{response[3]:X2}");
                return false;
            }

            _logger?.LogSuccess($"Active slot set to {slotIndex}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to set active slot: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Factory reset the device (requires confirmation)
    /// </summary>
    public bool FactoryReset()
    {
        if (_device == null)
        {
            _logger?.LogError("No device connected");
            return false;
        }

        try
        {
            _logger?.LogWarning("Performing factory reset...");

            byte[] request = new byte[REPORT_SIZE];
            request[0] = REPORT_ID;
            request[1] = CMD_FACTORY_RESET;
            // Magic bytes 0xDEADBEEF (little-endian)
            request[2] = 0xEF;
            request[3] = 0xBE;
            request[4] = 0xAD;
            request[5] = 0xDE;
            request[63] = CalculateChecksum(request);

            byte[] response = new byte[REPORT_SIZE];
            if (!SendFeatureReport(request, response))
                return false;

            if (response[2] != 0x00)
            {
                _logger?.LogError("Factory reset failed");
                return false;
            }

            _logger?.LogSuccess("Factory reset completed");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Factory reset failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send Feature Report and receive response
    /// </summary>
    private bool SendFeatureReport(byte[] request, byte[] response)
    {
        if (_device == null)
            return false;

        HidStream? stream = null;
        try
        {
            if (!_device.TryOpen(out stream))
            {
                _logger?.LogError("Failed to open device");
                return false;
            }

            // Send request
            stream.SetFeature(request, 0, REPORT_SIZE);

            // Receive response
            response[0] = REPORT_ID;
            stream.GetFeature(response, 0, REPORT_SIZE);

            // Verify checksum
            byte calcChecksum = CalculateChecksum(response);
            if (calcChecksum != response[63])
            {
                _logger?.LogError($"Response checksum mismatch: expected 0x{calcChecksum:X2}, got 0x{response[63]:X2}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"USB communication error: {ex.Message}");
            return false;
        }
        finally
        {
            stream?.Close();
        }
    }

    /// <summary>
    /// Calculate XOR checksum for Feature Report (bytes 0-62, excluding checksum at byte 63)
    /// Matches firmware: calcReportChecksum(data, 63) which XORs bytes 0-62
    /// </summary>
    private byte CalculateChecksum(byte[] data)
    {
        byte checksum = 0;
        for (int i = 0; i < 63; i++) // XOR bytes 0-62 (63 bytes total)
        {
            checksum ^= data[i];
        }
        return checksum;
    }
}
