using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using z100emu.Core;

namespace z100emu.Peripheral.Floppy
{
    public class StatusPort : IPortDevice
    {
        private static readonly int STAT_INT_SHFT = 0;
        private static readonly int STAT_MTR_SHFT = 1;
        private static readonly int STAT_96T_SHFT = 3;
        private static readonly int STAT_TWO_SHFT = 6;
        private static readonly int STAT_RDY_SHFT = 7;

        private Intel8259 _pic;

        public bool Interrupt = false;
        public bool MotorOn = false;
        public bool TPI96 = false;
        public bool TwoSided = true;
        public bool Ready = false;

        public byte Value => (byte)
                (
                    ((Interrupt ? 1 : 0) << STAT_INT_SHFT) +
                    ((MotorOn ? 1 : 0) << STAT_MTR_SHFT) +
                    ((TPI96 ? 1 : 0) << STAT_96T_SHFT) +
                    ((TwoSided ? 1 : 0) << STAT_TWO_SHFT) +
                    ((Ready ? 1 : 0) << STAT_RDY_SHFT)
                );

        private int _port;

        public StatusPort(Intel8259 pic, int port)
        {
            _pic = pic;
            _port = port;

            _pic.RegisterInterrupt(0, () => Interrupt);
        }

        public byte Read(int port)
        {
            var v = Value;
            Interrupt = false;
            return v;
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value) { throw new NotImplementedException(); }
        public void Write16(int port, ushort value) { throw new NotImplementedException(); }

        public int[] Ports => new[] { _port };
    }
}
