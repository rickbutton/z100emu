using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy
{
    public class SectorSize
    {
        public static SectorSize Size128  = new SectorSize(128,  3, 0);
        public static SectorSize Size256  = new SectorSize(256,  0, 1);
        public static SectorSize Size512  = new SectorSize(512,  1, 2);
        public static SectorSize Size1024 = new SectorSize(1024, 2, 3);

        public SectorSize(int size, int sizeField, int altSizeField)
        {
            Size = size;
            SizeField = sizeField;
            AltSizeField = altSizeField;
        }

        public int Size { get; private set; }
        public int SizeField { get; private set; }
        public int AltSizeField { get; private set; }
    }
}
