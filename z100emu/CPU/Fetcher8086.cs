using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU
{
    public class Fetcher8086 : IInstructionFetcher
    {
        private Cpu8086 _cpu;
        private int _fetched;

        public Fetcher8086(Cpu8086 cpu)
        {
            _cpu = cpu;
            _fetched = 0;
        }

        byte IInstructionFetcher.FetchU8()
        {
            var value = _cpu.ReadU8(InstructionHelper.SegmentToAddress(
                                    _cpu.GetRegister(Register.CS), 
                                    (ushort)(_cpu.GetRegister(Register.IP) + _fetched)));
            _fetched += 1;
            return value;
        }
        ushort IInstructionFetcher.FetchU16()
        {
            var value = _cpu.ReadU16(InstructionHelper.SegmentToAddress(
                                    _cpu.GetRegister(Register.CS), 
                                    (ushort)(_cpu.GetRegister(Register.IP) + _fetched)));
            _fetched += 2;
            return value;
        }

        public int GetBytesFetched()
        {
            return _fetched;
        }
    }
}
