using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU
{
    public class Breakpoint
    {
        public int Address { get; }
        public bool IsInternal { get; }

        public Breakpoint(int addr, bool isInternal)
        {
            Address = addr;
            IsInternal = isInternal;
        }
    }
}
