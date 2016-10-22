using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU
{
    public static class InstructionStringHelper
    {
        public static string OutputInstruction(OpCodeManager.Instruction instruction)
        {
            var output = instruction.Type.ToString();
            var arg1 = OutputArgument(instruction.SegmentPrefix, instruction.Flag, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            var arg2 = OutputArgument(instruction.SegmentPrefix, instruction.Flag, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

            if (arg1 == null)
                return output;
            return arg2 == null ? $"{output} {arg1}" : $"{output} {arg1}, {arg2}";
        }

        public static string OutputArgument(Register segmentPrefix, OpCodeManager.OpCodeFlag flag, int argument, int argumentValue, int argumentDisplacement)
        {
            if (argument == OpCodeManager.ARG_NONE)
                return null;
            switch (argument)
            {
                case (int)Register.AX:
                    return "AX";
                case (int)Register.CX:
                    return "CX";
                case (int)Register.DX:
                    return "DX";
                case (int)Register.BX:
                    return "BX";
                case (int)Register.SP:
                    return "SP";
                case (int)Register.BP:
                    return "BP";
                case (int)Register.SI:
                    return "SI";
                case (int)Register.DI:
                    return "DI";
                case (int)Register.IP:
                    return "IP";
                case (int)Register.CS:
                    return "CS";
                case (int)Register.DS:
                    return "DS";
                case (int)Register.ES:
                    return "ES";
                case (int)Register.SS:
                    return "SS";
                case unchecked((int)Register.FLAGS):
                    return "FLAGS";

                case OpCodeManager.ARG_BYTE_REGISTER:
                    switch ((Register)argumentValue)
                    {
                        case Register.AL:
                            return "AL";
                        case Register.CL:
                            return "CL";
                        case Register.DL:
                            return "DL";
                        case Register.BL:
                            return "BL";
                        case Register.AH:
                            return "AH";
                        case Register.CH:
                            return "CH";
                        case Register.DH:
                            return "DH";
                        case Register.BH:
                            return "BH";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_DEREFERENCE:
                    string value;
                    switch (argumentValue)
                    {
                        case 0:
                            value = "BX+SI";
                            break;
                        case 1:
                            value = "BX+DI";
                            break;
                        case 2:
                            value = "BP+SI";
                            break;
                        case 3:
                            value = "BP+DI";
                            break;
                        case 4:
                            value = "SI";
                            break;
                        case 5:
                            value = "DI";
                            break;
                        case 6:
                            value = "BP";
                            break;
                        case 7:
                            value = "BX";
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    switch (segmentPrefix)
                    {
                        case Register.Invalid:
                            return argumentDisplacement < 0 ? $"[{value}{argumentDisplacement}]" : $"[{value}+{argumentDisplacement}]";
                        case Register.ES:
                            return argumentDisplacement < 0 ? $"[ES:{value}{argumentDisplacement}]" : $"[ES:{value}+{argumentDisplacement}]";
                        case Register.CS:
                            return argumentDisplacement < 0 ? $"[CS:{value}{argumentDisplacement}]" : $"[CS:{value}+{argumentDisplacement}]";
                        case Register.SS:
                            return argumentDisplacement < 0 ? $"[SS:{value}{argumentDisplacement}]" : $"[SS:{value}+{argumentDisplacement}]";
                        case Register.DS:
                            return argumentDisplacement < 0 ? $"[DS:{value}{argumentDisplacement}]" : $"[DS:{value}+{argumentDisplacement}]";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_MEMORY:
                    switch (segmentPrefix)
                    {
                        case Register.Invalid:
                            return $"[{argumentValue:X4}]";
                        case Register.ES:
                            return $"[ES:{argumentValue:X4}]";
                        case Register.CS:
                            return $"[CS:{argumentValue:X4}]";
                        case Register.SS:
                            return $"[SS:{argumentValue:X4}]";
                        case Register.DS:
                            return $"[DS:{argumentValue:X4}]";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_FAR_MEMORY:
                    var segment = (uint)argumentValue >> 16;
                    var address = argumentValue & 0xFFFF;
                    return $"[{segment:X4}:{address:X4}]";
                case OpCodeManager.ARG_CONSTANT:
                    return flag.Has(OpCodeManager.OpCodeFlag.Size8) ? $"{argumentValue:X2}" : $"{argumentValue:X4}";
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
