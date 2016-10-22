using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class LoadFarPointer : IInstruction
    {
        private Register _register;

        public LoadFarPointer(Register register)
        {
            _register = register;
        }

        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
            var memory = cpu.ReadU16(address);
            var segment = cpu.ReadU16(address + 2);

            cpu.SetRegister(_register, segment);
            switch ((Register)instruction.Argument1)
            {
                case Register.AX:
                case Register.CX:
                case Register.DX:
                case Register.BX:
                case Register.SP:
                case Register.BP:
                case Register.SI:
                case Register.DI:
                case Register.IP:
                case Register.CS:
                case Register.DS:
                case Register.ES:
                case Register.SS:
                    cpu.SetRegister((Register)instruction.Argument1, memory);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
