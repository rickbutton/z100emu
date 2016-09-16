using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy.Disk
{
    public interface IDisk
    {
        int TotalHeads { get; }
        int TotalCylinders { get; }

        byte Get(int cylinder, int head, int sector, int sectorIndex);
        SectorSize GetSectorSize(int head, int cylinder);
        int GetNumSectors(int head, int cylinder);
        bool GetDeleted(int cylinder, int head, int sector);
    }
}
