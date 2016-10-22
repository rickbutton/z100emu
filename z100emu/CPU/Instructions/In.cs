using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class In : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var port = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
            var device = cpu.GetPortDevice(port);
            if (device == null)
                throw new InvalidOperationException($"Tried to read from port 0x{port.ToString("X")}");

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.SetRegister(Register.AL, device.Read(port));
                //Console.WriteLine($"IN {port.ToString("X")}:{cpu.GetRegister(Register.AL).ToString("X")}");
            }
            else
            {
                cpu.SetRegister(Register.AX, device.Read16(port));
            }
        }
    }
}
