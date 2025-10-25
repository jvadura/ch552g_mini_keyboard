namespace CH552G_PadConfig_Win.Models;

/// <summary>
/// Device information retrieved from CMD_GET_INFO (0x04)
/// </summary>
public class DeviceInfo
{
    public byte FirmwareMajor { get; set; }
    public byte FirmwareMinor { get; set; }
    public byte FirmwarePatch { get; set; }
    public byte ConfigVersion { get; set; }
    public ushort BuildNumber { get; set; }
    public byte Capabilities { get; set; }
    public byte MaxSlots { get; set; }
    public byte MaxInputs { get; set; }
    public byte TotalActions { get; set; }
    public string BuildDate { get; set; } = string.Empty;
    public string GitHash { get; set; } = string.Empty;

    public string FirmwareVersion => $"{FirmwareMajor}.{FirmwareMinor}.{FirmwarePatch}";

    public override string ToString()
    {
        return $"Firmware v{FirmwareVersion}, Config v{ConfigVersion}, Build {BuildNumber}";
    }
}
