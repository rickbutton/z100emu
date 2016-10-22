using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class OneStringOperation : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Cmps:
                    new CompareString().Dispatch(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Lods:
                    new LoadString().Dispatch(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Movs:
                    new MoveString().Dispatch(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Stos:
                    new StoreString().Dispatch(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Scas:
                    new ScanString().Dispatch(cpu, instruction);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
