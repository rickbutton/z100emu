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

        private static int CMD_FORCE_INT_MASK = 0xD0;
        private static int CMD_FORCE_INT_NR_R   = 1;
        private static int CMD_FORCE_INT_R_NR   = 2;
        private static int CMD_FORCE_INT_PULSE  = 4;
        private static int CMD_FORCE_INT_IMM    = 8;

        private int _portBase;

        private readonly int _port1797Status;
        private readonly int _port1797Command;

        private readonly int _port1797Track;
        private readonly int _port1797Sector;
        private readonly int _port1797Data;

        private readonly int _portControl;
        private readonly int _portStatus;

        private byte _1797status  = 0;
        private byte _1797command = 0;
        private byte _1797track   = 0;
        private byte _1797sector  = 0;
        private byte _1797data    = 0;

        private byte _controlLatch;
        private byte _status;

        private bool InterruptNotReadyToReady => (_1797command & CMD_FORCE_INT_NR_R) == CMD_FORCE_INT_NR_R;
        private bool InterruptReadyToNotReady => (_1797command & CMD_FORCE_INT_R_NR) == CMD_FORCE_INT_R_NR;
        private bool InterruptIndexPulse      => (_1797command & CMD_FORCE_INT_PULSE) == CMD_FORCE_INT_PULSE;
        private bool InterruptImmediate       => (_1797command & CMD_FORCE_INT_IMM) == CMD_FORCE_INT_IMM;
        

        public ZenithFloppy(Intel8259 pic, int portBase)
        {
            _pic = pic;

            _portBase     = portBase;
            _port1797Status = portBase + 0;
            _port1797Command  = portBase + 0;
            _port1797Track    = portBase + 1;
            _port1797Sector   = portBase + 2;
            _port1797Data     = portBase + 3;
            _portControl  = portBase + 4;
            _portStatus   = portBase + 5;
        }

        public byte Read(int port)
        {
            if (port == _port1797Status)
            {
                return _1797status;
            }
            if (port == _port1797Track)
            {
                return _1797track;
            }
            if (port == _port1797Sector)
            {
                return _1797sector;
            }
            if (port == _port1797Data)
            {
                return _1797data;
            }
            if (port == _portControl)
            {
                return _controlLatch; 
            }
            if (port == _portStatus)
            {
                return _status;
            }

            throw new InvalidOperationException();
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == _port1797Command)
            {
                _1797command = value;
                return;
            }
            if (port == _port1797Track)
            {
                _1797track = value;
                return;
            }
            if (port == _port1797Sector)
            {
                _1797sector = value;
                return;
            }
            if (port == _port1797Data)
            {
                _1797data = value;
                return;
            }
            if (port == _portControl)
            {
                _controlLatch = value;
                return;
            }
            if (port == _portStatus)
            {
                _status = value;
                return;
            }

            throw new InvalidOperationException();
        }

        public void Write16(int port, ushort value)
        {
            Write(port, (byte)value);
        }

        public int[] Ports
            => new[] { _port1797Status, _port1797Track, _port1797Sector, _port1797Data, _portControl, _portStatus};
    }
}
