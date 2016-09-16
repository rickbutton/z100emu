using System;
using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithReserved : IPortDevice
    {
        public byte Read(int port)
        {
            if (port == 0xF6)
                return 1;
            else
            {
                return 0;
            }
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == 0x40)
            {
                Console.WriteLine("MultiPort");
            }
        } 
        public void Write16(int port, ushort value) { }
        public int[] Ports => new int[] { 0xF6, 0xA8, 0xA9, 0xAA, 0xAB, 0x40 };
    }
}
