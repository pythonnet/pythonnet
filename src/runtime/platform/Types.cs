namespace Python.Runtime.Platform
{
    public enum MachineType
    {
        i386,
        x86_64,
        armv7l,
        armv8,
        aarch64,
        Other
    };

    /// <summary>
    /// Operating system type as reported by Python.
    /// </summary>
    public enum OperatingSystemType
    {
        Windows,
        Darwin,
        Linux,
        Other
    }
}
