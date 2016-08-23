using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class Zenith8041a : IPortDevice
    {
        public byte Read(int port) { return 0; }
        public ushort Read16(int port) { return 0; }
        public void Write(int port, byte value) { }
        public void Write16(int port, ushort value) { }

        public int[] Ports => new int[] { 0xF4, 0xF5 };
    }
}
