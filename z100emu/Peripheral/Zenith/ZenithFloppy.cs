using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithFloppy : IPortDevice
    {
        private Intel8259 _pic;

        private static int INTERRUPT = 8;

        public ZenithFloppy(Intel8259 pic)
        {
            _pic = pic;
        }

        public byte Read(int port)
        {
            return 0;
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == 0xB0 && value == 208)
                _pic.RequestInterrupt(INTERRUPT);

        }

        public void Write16(int port, ushort value)
        {
        }

        public int[] Ports
            => new[] {0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF};
    }
}
