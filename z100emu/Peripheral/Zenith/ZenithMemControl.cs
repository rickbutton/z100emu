using z100emu.Core;
using z100emu.Ram;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithMemControl : IPortDevice
    {
        private byte _byte = 0;
        private IRam _ram;
        private ZenithRom _rom;

        public ZenithMemControl(IRam ram, ZenithRom rom)
        {
            _ram = ram;
            _rom = rom;
        }

        public byte Read(int port) { return _byte; }
        public ushort Read16(int port) { return Read(port); }

        public void Write16(int port, ushort value) { Write(port, (byte) value); }

        public void Write(int port, byte value)
        {
            _byte = value;

            var ramConfig = value & 2;
            var romConfig = (value >> 2) & 2;

            _ram.ZeroParity = (value & (1 << 4)) == 0;
            var kill = (value & (1 << 5)) == 0;

            if (_ram.KillParity && !kill)
            {
                _ram.ClearParityError(); 
            }
            _ram.KillParity = kill;

            switch (ramConfig)
            {
                case 0:
                    _ram.RamConfig = RamConfig.Option0;
                    break;
                case 1:
                    _ram.RamConfig = RamConfig.Option1;
                    break;
                case 2:
                    _ram.RamConfig = RamConfig.Option2;
                    break;
                case 3:
                    _ram.RamConfig = RamConfig.Option3;
                    break;
            }

            switch (romConfig)
            {
                case 0:
                    _rom.RomConfig = RomConfig.Option0;
                    break;
                case 1:
                    _rom.RomConfig = RomConfig.Option1;
                    break;
                case 2:
                    _rom.RomConfig = RomConfig.Option2;
                    break;
                case 3:
                    _rom.RomConfig = RomConfig.Option3;
                    break;
            }
        }

        public int[] Ports => new int[] { 0xFC };
    }
}
