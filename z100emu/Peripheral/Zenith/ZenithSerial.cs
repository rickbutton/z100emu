using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithSerial : IPortDevice
    {
        private int _portBase;

        public ZenithSerial(int portBase)
        {
            _portBase = portBase;
        }

        public byte Read(int port)
        {
            return 0;
        }
        public ushort Read16(int port) { return 0; }

        public void Write(int port, byte value)
        {
            
        } 
        public void Write16(int port, ushort value) { }
        public int[] Ports => new int[] { _portBase, _portBase+1, _portBase+2, _portBase+3 };
    }
}
