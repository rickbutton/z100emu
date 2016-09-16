using System;
using System.IO;

namespace z100emu.Ram
{
    public class ZenithRom : IRamBank
    {
        private byte[] _rom;

        private ZenithRom(byte[] rom)
        {
            _rom = rom;
        }

        public byte this[int pos]
        {
            get { return _rom[pos]; }
            set { _rom[pos] = value; }
        }

        public bool TryGet(int pos, out byte value)
        {
            if (RomConfig == RomConfig.Option0)
            {
                value = _rom[pos % _rom.Length];
                return true;
            }
            else if (RomConfig == RomConfig.Option2)
            {
                if (pos >= (1024*1024) - _rom.Length)
                {
                    var index = pos - ((1024*1024) - _rom.Length);

                    value = _rom[index];
                    return true;
                }

                value = 0;
                return false;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public bool TrySet(int pos, byte value)
        {
            return false;
        }

        public int Length => _rom.Length;

        public static ZenithRom GetRom(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return new ZenithRom(bytes);
        }

        public RomConfig RomConfig { get; set; }
    }
}
