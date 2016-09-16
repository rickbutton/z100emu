using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public class CompressedSectorData : ISectorData
    {
        private readonly byte _data;

        public CompressedSectorData(byte data, bool deleted)
        {
            _data = data;
            Deleted = deleted;
        }

        public byte this[int i] => _data;
        public bool Deleted { get; }
    }
}
