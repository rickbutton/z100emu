using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace z100emu.CPU
{
    public interface IInstruction
    {
        void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction);
    }
}
