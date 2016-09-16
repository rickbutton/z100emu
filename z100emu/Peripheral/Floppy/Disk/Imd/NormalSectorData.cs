using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public class NormalSectorData : ISectorData
    {
        private readonly byte[] _data;

        public NormalSectorData(byte[] data, bool deleted)
        {
            _data = data;
            Deleted = deleted;
        }

        public bool Deleted { get; }
        public byte this[int i] => _data[i];
    }
}
