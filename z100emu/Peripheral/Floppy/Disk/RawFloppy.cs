using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.Peripheral.Floppy.Disk
{
    public class RawFloppy : IDisk
    {
        private byte[] _data;

        public RawFloppy(byte[] data)
        {
            _data = data;

            if (data.Length == 1024*320)
            {
                SectorsPerTrack = 8;
            }
            else if (data.Length == 1024*360)
            {
                SectorsPerTrack = 9;
            }
            SectorSize = SectorSize.Size512;
        }

        public int SectorsPerTrack { get; }
        public SectorSize SectorSize { get; }

        public byte Get(int cylinder, int head, int sector, int sectorIndex)
        {
            if (cylinder < 0 || cylinder >= TotalCylinders)
                throw new ArgumentOutOfRangeException(nameof(cylinder));
            if (head < 0 || head >= TotalHeads)
                throw new ArgumentOutOfRangeException(nameof(head));
            if (sector < 1 || sector > SectorsPerTrack)
                throw new ArgumentOutOfRangeException(nameof(sector));
            if (sectorIndex < 0 || sectorIndex >= SectorSize.Size)
                throw new ArgumentOutOfRangeException(nameof(sectorIndex));

            var lba = (cylinder*TotalHeads+head)*SectorsPerTrack + sector - 1;
            var index = lba * SectorSize.Size + sectorIndex;
            return _data[index];
        }

        public SectorSize GetSectorSize(int head, int cylinder)
        {
            return SectorSize;
        }

        public int GetNumSectors(int head, int cylinder)
        {
            return SectorsPerTrack;
        }

        public bool GetDeleted(int cylinder, int head, int sector)
        {
            return false;
        }

        public int TotalCylinders => 40;
        public int TotalHeads => 2;
    }
}
