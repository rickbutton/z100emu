using System;
using System.Collections;
using System.Collections.Generic;
using System.Media;
using System.Threading;
using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class Zenith8041a : IPortDevice
    {
        private static int PORT_DAT = 0xF4;
        private static int PORT_CMD = 0xF5;
        private static int PORT_STA = 0xF5;

        private static int CMD_RESET    = 0x0;
        private static int CMD_AUTO_ON  = 0x1;
        private static int CMD_AUTO_OFF = 0x2;
        private static int CMD_KC_ON    = 0x3;
        private static int CMD_KC_OFF   = 0x4;
        private static int CMD_CLR_FIFO = 0x5;
        private static int CMD_CLICK    = 0x6;
        private static int CMD_BEEP     = 0x7;
        private static int CMD_EN_KB    = 0x8;
        private static int CMD_DIS_KB   = 0x9;
        private static int CMD_EVENT_M  = 0xA;
        private static int CMD_ASCII_M  = 0xB;
        private static int CMD_EN_INT   = 0xC;
        private static int CMD_DIS_INT  = 0xD;

        private Queue<byte> _buffer;
        private bool _keyClick = false;
        private bool _interruptsEnabled = false;
        private Intel8259 _pic;

        public Zenith8041a(Intel8259 pic)
        {
            _pic = pic;
            Reset();
        }

        public void Input(byte b)
        {
            _buffer.Enqueue(b);

            if (_interruptsEnabled)
                _pic.RequestInterrupt(6);
            if (_keyClick)
                Beep();
        }

        private void Reset()
        {
            _buffer = new Queue<byte>();
        }

        private void Beep()
        {
            Action action = Console.Beep;
            action.BeginInvoke(null, null);
        }

        public byte Read(int port)
        {
            if (port == PORT_STA)
            {
                return (byte)(_buffer.Count > 0 ? 1 : 0);
            }
            else if (port == PORT_DAT)
            {
                if (_buffer.Count > 0)
                {
                    if (_buffer.Count == 1)
                        _pic.AckInterrupt(6);
                    return _buffer.Dequeue();
                }
            }

            throw new InvalidOperationException();
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == PORT_CMD)
            {
                if (value == CMD_RESET)
                    Reset();
                else if (value == CMD_CLICK)
                    Beep();
                else if (value == CMD_BEEP)
                    Beep();
                else if (value == CMD_EN_INT)
                    _interruptsEnabled = true;
                else if (value == CMD_DIS_INT)
                    _interruptsEnabled = false;
                else
                    throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        public void Write16(int port, ushort value) { Write(port, (byte)value); }

        public int[] Ports => new int[] { 0xF4, 0xF5 };
    }
}
