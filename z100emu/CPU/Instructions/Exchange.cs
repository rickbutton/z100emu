using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Exchange : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Argument1)
            {
                case (int)Register.AX:
                case (int)Register.CX:
                case (int)Register.DX:
                case (int)Register.BX:
                case (int)Register.SP:
                case (int)Register.BP:
                case (int)Register.SI:
                case (int)Register.DI:
                case (int)Register.IP:
                case (int)Register.CS:
                case (int)Register.DS:
                case (int)Register.ES:
                case (int)Register.SS:
                    cpu.SetRegister((Register)instruction.Argument1, ProcessExchangeSecond(cpu, instruction, cpu.GetRegister((Register)instruction.Argument1)));
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegister((Register)instruction.Argument1Value, (byte)ProcessExchangeSecond(cpu, instruction, cpu.GetRegisterU8((Register)instruction.Argument1Value)));
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private ushort ProcessExchangeSecond(Cpu8086 cpu, OpCodeManager.Instruction instruction, ushort value)
        {
            ushort tmp;
            switch (instruction.Argument2)
            {
                case (int)Register.AX:
                case (int)Register.CX:
                case (int)Register.DX:
                case (int)Register.BX:
                case (int)Register.SP:
                case (int)Register.BP:
                case (int)Register.SI:
                case (int)Register.DI:
                case (int)Register.IP:
                case (int)Register.CS:
                case (int)Register.DS:
                case (int)Register.ES:
                case (int)Register.SS:
                    tmp = cpu.GetRegister((Register)instruction.Argument2);
                    cpu.SetRegister((Register)instruction.Argument2, (byte)value);
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    tmp = cpu.GetRegisterU8((Register)instruction.Argument2Value);
                    cpu.SetRegisterU8((Register)instruction.Argument2Value, (byte)value);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
                    tmp = cpu.ReadU16(address);
                    cpu.WriteU16(address, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return tmp;
        }
    }
}
