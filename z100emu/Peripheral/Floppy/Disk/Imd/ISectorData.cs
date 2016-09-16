namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public interface ISectorData
    {
        byte this[int i] { get; }
        bool Deleted { get; }
    }
}