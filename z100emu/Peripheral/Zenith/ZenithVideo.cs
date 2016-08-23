using System;
using System.Diagnostics;
using SDL2;
using z100emu.Core;
using z100emu.Ram;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithVideo : IPortDevice, IRamBank, IDisposable
    {
        private static int PORT_IO         = 0xD8;
        private static int PORT_CONTROL_A  = 0xD9;
        private static int PORT_ADDR_LATCH = 0xDA;
        private static int PORT_CONTROL_B  = 0xDB;

        private static int PORT_ADDR_REG   = 0xDC;
        private static int PORT_REG        = 0xDD;

        private static int RED_START   = 0xD0000;
        private static int BLUE_START  = 0xC0000;
        private static int GREEN_START = 0xE0000;
        private static int VRAM_BANK_SIZE = 0x10000;

        private static int WIDTH = 80;
        private static int HEIGHT = 25;
        private static int CHAR_HEIGHT = 9;

        private static int REAL_WIDTH  = 80 * 8;
        private static int REAL_HEIGHT = 25 * 9;

        private static uint SYNC_NS = 16666666;
        private static int NS_PER_MS = 1000000;

        private byte _io;

        private bool _redEnabled = true;
        private bool _greenEnabled = true;
        private bool _blueEnabled = true;
        private bool _flashEnabled = true;

        private bool _redCopy = true;
        private bool _greenCopy = true;
        private bool _blueCopy = true;

        private bool _vramEnabled = true;

        private readonly IntPtr _window;
        private readonly IntPtr _renderer;
        private IntPtr _texture;

        private Intel8259 _pic;

        private Stopwatch _syncCounter;

        private readonly byte[] _red   = new byte[VRAM_BANK_SIZE];
        private readonly byte[] _green = new byte[VRAM_BANK_SIZE];
        private readonly byte[] _blue  = new byte[VRAM_BANK_SIZE];

        private readonly byte[] _regs = new byte[18];
        private byte _regPointer = 0;

        public ZenithVideo(IntPtr window, IntPtr renderer, Intel8259 pic)
        {
            this._window = window;
            this._renderer = renderer;
            this._pic = pic;

            _syncCounter = Stopwatch.StartNew();

            ChangeResolution();
        }

        #region SDL
        private void ChangeResolution()
        {
            if (_texture != IntPtr.Zero)    
                SDL.SDL_DestroyTexture(_texture);

            SDL.SDL_SetWindowSize(_window, REAL_WIDTH * 2, REAL_HEIGHT * 4);
            SDL.SDL_SetWindowPosition(_window, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);

            _texture = SDL.SDL_CreateTexture(_renderer, SDL.SDL_PIXELFORMAT_RGBX8888,
                SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, REAL_WIDTH, REAL_HEIGHT);

            if (_texture == IntPtr.Zero)
                throw new InvalidOperationException();
        }

        public unsafe void Draw()
        {
            int pitch;
            IntPtr pixels;
            if (SDL.SDL_LockTexture(_texture, IntPtr.Zero, out pixels, out pitch) != 0)
                throw new InvalidOperationException();

            /*uint* pixelPtr = (uint*) pixels.ToPointer();
            for (var i = 0; i < VRAM_BANK_SIZE; i++)
            {
                var x = i & 0x7F;
                var y = (i >> 7) & 0x1FF;

                if (y >= 255)
                    continue;

                for (var bit = 0; bit < 8; bit++)
                {
                    var r = (((_red[i] >> bit) & 1) != 0) ? 0xFF : 0;
                    var g = (((_green[i] >> bit) & 1) != 0) ? 0xFF : 0;
                    var b = (((_blue[i] >> bit) & 1) != 0) ? 0xFF : 0;
                    uint pix = (uint)((r << 24) + (g << 16) + (b << 8));
                    pixelPtr[(y*REAL_WIDTH) + ((x*8) + (7 - bit))] = pix;
                }
            }*/

            uint* pixelPtr = (uint*) pixels.ToPointer();
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
                        var r = (((_red[i] >> bit) & 1) != 0) ? 0xFF : 0;
                        var g = (((_green[i] >> bit) & 1) != 0) ? 0xFF : 0;
                        var b = (((_blue[i] >> bit) & 1) != 0) ? 0xFF : 0;
                        uint pix = (uint) ((r << 24) + (g << 16) + (b << 8));
                        pixelPtr[(realY*REAL_WIDTH) + ((x*8) + (7 - bit))] = pix;
                    }
                }
                realY++;
            }

            SDL.SDL_UnlockTexture(_texture);
            if (SDL.SDL_RenderClear(_renderer) != 0)
                throw new InvalidOperationException();
            if (SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero) != 0)
                throw new InvalidOperationException();

            SDL.SDL_RenderPresent(_renderer);
        }
        #endregion

        public void Step()
        {
            if (_syncCounter.ElapsedMilliseconds * NS_PER_MS >= SYNC_NS)
            //if (_syncCounter.ElapsedMilliseconds >= 1000)
            {
                Draw();
                _syncCounter.Restart();
                _pic.RequestInterrupt(6);
            }
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
            else
                throw new NotImplementedException();
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
                Console.WriteLine($"Trying to write {value.ToString("X")} to port {port.ToString("X")}");
                /*if (value >= _regs.Length)
                    throw new InvalidOperationException($"Tried to set 6845 Address register to invalid value [{value}]");
                _regPointer = value;*/
            }
            else if (port == PORT_REG)
            {
                Console.WriteLine($"Trying to write {value.ToString("X")} to port {port.ToString("X")}");
                //_regs[_regPointer] = value;
            }
            else if (port == PORT_CONTROL_A)
            {
            }
            else if (port == PORT_CONTROL_B)
            {
                
            }
            else if (port == PORT_ADDR_LATCH)
            {
                
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
                CopyRed(pos - RED_START, value, true);
                CopyGreen(pos - RED_START, value);
                CopyBlue(pos - RED_START, value);
                return true;
            }
            if (pos >= GREEN_START && pos < GREEN_START + VRAM_BANK_SIZE)
            {
                CopyRed(pos - GREEN_START, value);
                CopyGreen(pos - GREEN_START, value, true);
                CopyBlue(pos - GREEN_START, value);
                return true;
            }
            if (pos >= BLUE_START && pos < BLUE_START + VRAM_BANK_SIZE)
            {
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

        public int Length => VRAM_BANK_SIZE*3;
        #endregion

        public void Dispose()
        {
            if (_texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_texture);
        }
    }
}
