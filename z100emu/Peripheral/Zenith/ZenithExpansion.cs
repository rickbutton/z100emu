using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithExpansion : IPortDevice
    {
        public byte Read(int port)
        {
            return 0;
        }
        public ushort Read16(int port) { return 0; }

        public void Write(int port, byte value)
        {
            
        } 
        public void Write16(int port, ushort value) { }
        public int[] Ports => new int[] { 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F };
    }
}
