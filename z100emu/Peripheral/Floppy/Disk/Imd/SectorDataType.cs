using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public enum SectorDataType
    {
        Unavailable, Normal, Compressed,
        NormalDeleted, CompressedDeleted,
        NormalError, CompressedError,
        DeletedError, CompressedDeletedError
    }
}
