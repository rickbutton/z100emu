using System;

namespace z100emu.Peripheral.Floppy
{
    public class StepRate
    {
        public static StepRate Rate0 = new StepRate(3);
        public static StepRate Rate1 = new StepRate(6);
        public static StepRate Rate2 = new StepRate(10);
        public static StepRate Rate3 = new StepRate(15);

        public StepRate(int rate)
        {
            Rate = rate;
        }

        public static StepRate Get(int rateNum)
        {
            switch (rateNum)
            {
                case 0: return Rate0;
                case 1: return Rate1;
                case 2: return Rate2;
                case 3: return Rate3;
            }
            throw new ArgumentException();
        }

        public int Rate { get; private set; }
    }
}