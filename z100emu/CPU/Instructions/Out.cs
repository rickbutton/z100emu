using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Out : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var port = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            var device = cpu.GetPortDevice(port);
            if (device == null)
            {
                throw new InvalidOperationException($"Tried to write to port 0x{port.ToString("X")}");
            }

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                //Console.WriteLine($"OUT {port.ToString("X")}:{cpu.GetRegister(Register.AL).ToString("X")}");
                device.Write(port, (byte) cpu.GetRegister(Register.AL));
            }
            else
            {
                device.Write16(port, cpu.GetRegister(Register.AX));
            }
        }
    }
}
