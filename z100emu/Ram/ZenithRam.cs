using System;
using System.Collections.Generic;
using SDL2;
using z100emu.Peripheral;

namespace z100emu.Ram
{
    public class ZenithRam : IRam
    {
        private byte[] _memory;
        private IList<IRamBank> _banks;
        private Intel8259 _pic;

        private bool _interrupt;

        public ZenithRam(int size, Intel8259 pic)
        {
            _memory = new byte[size];
            _banks = new List<IRamBank>();
            _pic = pic;
            _pic.RegisterInterrupt(0, () => _interrupt);
        }

        public byte this[int pos]
        {
            get
            {
                if (RamConfig != RamConfig.Option0)
                    throw new NotImplementedException();

                foreach (var bank in _banks)
                {
                    byte value;
                    bool success = bank.TryGet(pos, out value);
                    if (success)
                        return value;
                }

                var mem = pos >= _memory.Length ? (byte)0 : _memory[pos];
                int parity;

                parity = CountOnes(mem)%2 == 0 ? 0 : 1;

                if (ZeroParity && parity == 1 && !KillParity)
                {
                    _interrupt = true;
                }

                return mem;
            }
            set
            {
                foreach (var bank in _banks)
                {
                    bool success = bank.TrySet(pos, value);
                    if (success)
                        return;
                }

                if (pos < _memory.Length)
                    _memory[pos] = value;
            }
        }


        int CountOnes(int x)
        {
            x = (x & (0x55555555)) + ((x >> 1) & (0x55555555));
            x = (x & (0x33333333)) + ((x >> 2) & (0x33333333));
            x = (x & (0x0f0f0f0f)) + ((x >> 4) & (0x0f0f0f0f));
            x = (x & (0x00ff00ff)) + ((x >> 8) & (0x00ff00ff));
            x = (x & (0x0000ffff)) + ((x >> 16) & (0x0000ffff));
            return x;
        }

        public int Length => _memory.Length;

        public void MapBank(IRamBank bank)
        {
            _banks.Add(bank);
        }

        public RamConfig RamConfig { get; set; }

        public bool ZeroParity { get; set; } = false;
        public bool KillParity { get; set; } = false;

        public void ClearParityError()
        {
            _interrupt = false;
        }
    }
}
