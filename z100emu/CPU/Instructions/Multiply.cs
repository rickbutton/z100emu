using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Multiply : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = cpu.GetRegister(Register.AX);
            var value2 = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 &= 0xFF;
                value2 &= 0xFF;
            }

            uint result;
            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Multiply:
                    result = (uint)value1 * value2;
                    break;
                case OpCodeManager.InstructionType.SignedMultiply:
                    result = (uint)(value1 * value2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            InstructionHelper.CalculateAddFlags(cpu, instruction.Flag, value1, value2, (int)result);

            cpu.SetRegister(Register.AX, (ushort)result);
            if (!instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                cpu.SetRegister(Register.DX, (ushort)(result >> 16));
        }
    }
}
