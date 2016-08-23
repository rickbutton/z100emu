using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithDIP : IPortDevice
    {
        private byte _dip = 0x0;

        public byte Read(int port) { return _dip; }
        public ushort Read16(int port) { return _dip; }
        public void Write(int port, byte value) { _dip = value; } 
        public void Write16(int port, ushort value) { _dip = (byte) value; }
        public int[] Ports => new int[] { 0xFF };
    }
}
