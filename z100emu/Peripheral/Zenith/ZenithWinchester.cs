using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithWinchester : IPortDevice
    {
        private static int STATUS = 0xAE;
        private static int COMMAND = 0xAF;

        private byte _status = 0;

        public byte Read(int port)
        {
            if (port == STATUS)
            {
                return _status;
            }
            else
            {
                return 0;
            }
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == COMMAND)
            {
                 
            }
            else if (port == STATUS)
            {
                
            }
        }

        public void Write16(int port, ushort value)
        {
            
        }
        public int[] Ports => new int[]
        {
            0xAE,
            0xAF
        };
    }
}
