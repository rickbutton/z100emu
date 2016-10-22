using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Threading;
using z100emu.Core;
using z100emu.Ram;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithVideo : IPortDevice, IRamBank
    {
        private static int PORT_IO         = 0xD8;
        private static int PORT_CONTROL_A  = 0xD9;
        private static int PORT_ADDR_LATCH = 0xDA;
        private static int PORT_CONTROL_B  = 0xDB;

        private static int PORT_ADDR_REG   = 0xDC;
        private static int PORT_REG        = 0xDD;

        private static int REG_V_DIS        = 0x6;
        private static int REG_MAX_RAS_ADDR = 0x9;
        private static int REG_CUR_ST_RAS   = 0xA;
        private static int REG_CUR_EN_RAS   = 0xB;
        private static int REG_START_HIGH   = 0xC;
        private static int REG_START_LOW    = 0xD;

        private static int REG_CUR_ST_HI    = 0xE;
        private static int REG_CUR_ST_LO    = 0xF;

        private static int CTRL_A_CLRSCR = (1 << 3);
        private static int CTRL_B_SET    = (1 << 3);

        private static int RED_START   = 0xD0000;
        private static int BLUE_START  = 0xC0000;
        private static int GREEN_START = 0xE0000;
        private static int VRAM_BANK_SIZE = 0x10000;

        private static int WIDTH = 80;
        private static int HEIGHT = 25;
        private static int CHAR_HEIGHT = 9;

        private static int REAL_WIDTH  = 80 * 8;
        private static int REAL_HEIGHT = 25 * 9;

        private static double CLK_US = 16666.666666667;

        private byte _io;
        private byte _controlA;
        private byte _controlB;
        private byte _addrLatch;

        private bool _redEnabled = true;
        private bool _greenEnabled = true;
        private bool _blueEnabled = true;
        private bool _flashEnabled = true;

        private bool _redCopy = true;
        private bool _greenCopy = true;
        private bool _blueCopy = true;

        private bool _vramEnabled = true;

        private IntPtr _texture;

        private Intel8259 _pic;
        private bool _interrupt = false;

        private double _us;

        private readonly byte[] _red   = new byte[VRAM_BANK_SIZE];
        private readonly byte[] _green = new byte[VRAM_BANK_SIZE];
        private readonly byte[] _blue  = new byte[VRAM_BANK_SIZE];

        private readonly byte[] _regs = new byte[18];
        private byte _regPointer = 0;

        public ZenithVideo(Intel8259 pic)
        {
            this._pic = pic;

            this._pic.RegisterInterrupt(6, () => _interrupt);

            _us = 0;
        }

        public byte[] Render()
        {
            byte[] pixels = new byte[REAL_WIDTH * REAL_HEIGHT * 3];

            var realY = 0;

            var skip = false;
            var yCounter = 0;
            for (var y = 0; y < 400; y++)
            {
                if (!skip && yCounter == 9)
                {
                    yCounter = 0;
                    skip = true;
                }
                else if (skip && yCounter == 7)
                {
                    yCounter = 0;
                    skip = false;
                }

                yCounter++;
                if (skip)
                    continue;

                for (var x = 0; x < 80; x++)
                {
                    var i = x + (y << 7);

                    for (var bit = 0; bit < 8; bit++)
                    {
                        var r = ((((_red[i] >> bit) & 1) != 0) && _redEnabled) || _flashEnabled ? 0xFF : 0;
                        var g = ((((_green[i] >> bit) & 1) != 0) && _greenEnabled) || _flashEnabled ? 0xFF : 0;
                        var b = ((((_blue[i] >> bit) & 1) != 0) && _blueEnabled) || _flashEnabled ? 0xFF : 0;
                        pixels[((realY*REAL_WIDTH) + ((x*8) + (7 - bit))) * 3] = (byte)r;
                        pixels[((realY*REAL_WIDTH) + ((x*8) + (7 - bit))) * 3 + 1] = (byte)g;
                        pixels[((realY*REAL_WIDTH) + ((x*8) + (7 - bit))) * 3 + 2] = (byte)b;
                    }
                }
                realY++;
            }
            return pixels;
        }

        public byte[] Step(double us)
        {
            _us += us;
            _interrupt = false;
            if (_us >= CLK_US)
            {
                _interrupt = true;
                _us -= CLK_US;
                return Render();
            }
            return null;
        }

        #region Ports
        public byte Read(int port)
        {
            if (port == PORT_IO)
            {
                return _io;
            }
            else if (port == PORT_ADDR_REG)
            {
                return _regPointer;
            }
            else if (port == PORT_REG)
            {
                return _regs[_regPointer];
            }
            else if (port == PORT_CONTROL_A)
            {
                return _controlA;
            }
            else if (port == PORT_CONTROL_B)
            {
                return _controlB;
            }
            else if (port == PORT_ADDR_LATCH)
            {
                return _addrLatch;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == PORT_IO)
            {
                _io = value;

                _redEnabled   = (value & (1 << 0)) == 0;
                _greenEnabled = (value & (1 << 1)) == 0;
                _blueEnabled  = (value & (1 << 2)) == 0;
                _flashEnabled = (value & (1 << 3)) == 0;
                _redCopy      = (value & (1 << 4)) == 0;
                _greenCopy    = (value & (1 << 5)) == 0;
                _blueCopy     = (value & (1 << 6)) == 0;
                _vramEnabled  = (value & (1 << 7)) == 0;
            }
            else if (port == PORT_ADDR_REG)
            {
                //if (value >= _regs.Length)
                    //throw new InvalidOperationException($"Tried to set 6845 Address register to invalid value [{value}]");
                if (value < _regs.Length)
                    _regPointer = value;
            }
            else if (port == PORT_REG)
            {
                _regs[_regPointer] = value;

                if (_regPointer == REG_V_DIS)
                {
                    
                }
                else if (_regPointer == REG_MAX_RAS_ADDR)
                {
                    
                }
                else if (_regPointer == REG_CUR_ST_RAS)
                {
                    
                }
                else if (_regPointer == REG_CUR_EN_RAS)
                {
                    
                }
                else if (_regPointer == REG_START_HIGH)
                {
                    
                }
                else if (_regPointer == REG_START_LOW)
                {

                }
                else
                {
                    
                }
            }
            else if (port == PORT_CONTROL_A)
            {
                _controlA = value;
                if ((_controlA & CTRL_A_CLRSCR) == 0)
                {
                    Clear((byte)(((_controlB & CTRL_B_SET) == 0) ? 0 : 0xFF));
                }
            }
            else if (port == PORT_CONTROL_B)
            {
                _controlB = value;
            }
            else if (port == PORT_ADDR_LATCH)
            {
                _addrLatch = value;
            }
        }

        public void Write16(int port, ushort value)
        {
            Write(port, (byte)value); 
        }

        public int[] Ports => new [] { PORT_IO, PORT_CONTROL_A, PORT_ADDR_LATCH, PORT_CONTROL_B, PORT_ADDR_REG, PORT_REG };
        #endregion

        #region Ram Bank
        public bool TryGet(int pos, out byte value)
        {
            if (pos >= RED_START && pos < RED_START + VRAM_BANK_SIZE)
            {
                value = GetBits(_red, pos - RED_START);
                return true;
            }
            if (pos >= GREEN_START && pos < GREEN_START + VRAM_BANK_SIZE)
            {
                value = GetBits(_green, pos - GREEN_START);
                return true;
            }
            if (pos >= BLUE_START && pos < BLUE_START + VRAM_BANK_SIZE)
            {
                value = GetBits(_blue, pos - BLUE_START);
                return true;
            }

            value = 0;
            return false;
        }

        public bool TrySet(int pos, byte value)
        {
            if (pos >= RED_START && pos < RED_START + VRAM_BANK_SIZE)
            {
                if (!_vramEnabled)
                    return true;

                CopyRed(pos - RED_START, value, true);
                CopyGreen(pos - RED_START, value);
                CopyBlue(pos - RED_START, value);
                return true;
            }
            if (pos >= GREEN_START && pos < GREEN_START + VRAM_BANK_SIZE)
            {
                if (!_vramEnabled)
                    return true;
                CopyRed(pos - GREEN_START, value);
                CopyGreen(pos - GREEN_START, value, true);
                CopyBlue(pos - GREEN_START, value);
                return true;
            }
            if (pos >= BLUE_START && pos < BLUE_START + VRAM_BANK_SIZE)
            {
                if (!_vramEnabled)
                    return true;
                CopyRed(pos - BLUE_START, value);
                CopyGreen(pos - BLUE_START, value);
                CopyBlue(pos - BLUE_START, value, true);
                return true;
            }
            return false;
        }

        private void SetBits(byte[] arr, int offset, byte value)
        {
            arr[offset] = value;
        }

        private byte GetBits(byte[] arr, int offset)
        {
            return arr[offset];
        }

        private void CopyRed(int offset, byte value, bool force = false) { if (_redCopy || force) SetBits(_red, offset, value); }
        private void CopyGreen(int offset, byte value, bool force = false) { if (_greenCopy || force) SetBits(_green, offset, value); }
        private void CopyBlue(int offset, byte value, bool force = false) { if (_blueCopy || force) SetBits(_blue, offset, value); }

        private void Clear(byte value)
        {
            for (var i = 0; i < _red.Length; i++)
            {
                _red[i] = value;
                _green[i] = value;
                _blue[i] = value;
            }
        }

        public int Length => VRAM_BANK_SIZE*3;
        #endregion
    }
}
