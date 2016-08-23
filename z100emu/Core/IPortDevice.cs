namespace z100emu.Core
{
    public interface IPortDevice
    {
        byte Read(int port);
        ushort Read16(int port);
        void Write(int port, byte value);
        void Write16(int port, ushort value);

        int[] Ports { get; }
    }
}
