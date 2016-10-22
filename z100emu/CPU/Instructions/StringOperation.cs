using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class StringOperation : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            ushort counter;
            switch (instruction.OpcodePrefix)
            {
                case 0:
                    new OneStringOperation().Dispatch(cpu, instruction);
                    break;

                case 0xF2:
                    counter = cpu.GetRegister(Register.CX);
                    if (instruction.Type == OpCodeManager.InstructionType.Cmps || instruction.Type == OpCodeManager.InstructionType.Scas)
                    {
                        while (counter != 0)
                        {
                            new OneStringOperation().Dispatch(cpu, instruction);
                            counter--;
                            if (cpu.GetFlags().Has(FlagsRegister.Zero))
                                break;
                        }
                    }
                    else
                    {
                        while (counter != 0)
                        {
                            new OneStringOperation().Dispatch(cpu, instruction);
                            counter--;
                        }
                    }
                    cpu.SetRegister(Register.CX, counter);
                    break;

                case 0xF3:
                    counter = cpu.GetRegister(Register.CX);
                    if (instruction.Type == OpCodeManager.InstructionType.Cmps || instruction.Type == OpCodeManager.InstructionType.Scas)
                    {
                        while (counter != 0)
                        {
                            new OneStringOperation().Dispatch(cpu, instruction);
                            counter--;
                            if (!cpu.GetFlags().Has(FlagsRegister.Zero))
                                break;
                        }
                    }
                    else
                    {
                        while (counter != 0)
                        {
                            new OneStringOperation().Dispatch(cpu, instruction);
                            counter--;
                        }
                    }
                    cpu.SetRegister(Register.CX, counter);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
