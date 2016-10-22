﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class JumpIfZero : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Zero))
                new JumpRelative().Dispatch(cpu, instruction);
        }
    }
}
