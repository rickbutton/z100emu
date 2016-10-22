using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class FarJump : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Argument1)
            {
                case OpCodeManager.ARG_FAR_MEMORY:
                    cpu.SetRegister(Register.CS, (ushort)((uint)instruction.Argument1Value >> 16));
                    cpu.SetRegister(Register.IP, (ushort)(instruction.Argument1Value & 0xFFFF));
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    cpu.SetRegister(Register.CS, cpu.ReadU16(address + 2));
                    cpu.SetRegister(Register.IP, cpu.ReadU16(address));
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
