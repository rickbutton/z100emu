using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDL2;

namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public class ImdFloppy : IDisk
    {
        private static readonly int HEAD_MARK_CYL_MAP = 0x80;
        private static readonly int HEAD_MARK_HEA_MAP = 0x40;

        private List<TrackData> _tracks;

        public ImdFloppy(byte[] data)
        {
            if (!(data[0] == 'I' && data[1] == 'M' && data[2] == 'D'))
                throw new ArgumentException("Supplied disk is not actually in IMD format");

            int i = 4; // start of version
            int versionEnd = 4;
            while (data[versionEnd] != ':') versionEnd++;
            byte[] versionBytes = new byte[versionEnd - i];
            Array.Copy(data, i, versionBytes, 0, versionEnd - i);
            ImdVersion = Encoding.ASCII.GetString(versionBytes);

            i = versionEnd + 3;
            int dateEnd = i + 18;
            byte[] dateBytes = new byte[dateEnd - i];
            Array.Copy(data, i, dateBytes, 0, dateEnd - i);
            ImdDate = Encoding.ASCII.GetString(dateBytes);

            i = dateEnd;
            int commentEnd = i;
            while (data[commentEnd] != 0x1A) commentEnd++;
            byte[] commentBytes = new byte[commentEnd - i];
            Array.Copy(data, i, commentBytes, 0, commentEnd - i);
            ImdComment = Encoding.ASCII.GetString(commentBytes);

            _tracks = new List<TrackData>();

            i = commentEnd + 1;
            while (i < data.Length)
            {
                ImdMode mode = (ImdMode) data[i];
                i++;
                int cylinder = data[i];
                i++;
                int head = data[i];
                i++;
                int numSectors = data[i];
                i++;
                int sectorSizeFlag = data[i];
                i++;

                if (sectorSizeFlag != 2)
                    throw new InvalidOperationException("Only IMD disks with sector size of 512 are supported");
                SectorSize sectorSize = SectorSize.Size512;

                if ((head & HEAD_MARK_CYL_MAP) == HEAD_MARK_CYL_MAP)
                    throw new InvalidOperationException("Unsupported field: Sector Cylinder Map");
                if ((head & HEAD_MARK_HEA_MAP) == HEAD_MARK_HEA_MAP)
                    throw new InvalidOperationException("Unsupported field: Sector Head Map");

                byte[] sectorNumMap = new byte[numSectors];
                Array.Copy(data, i, sectorNumMap, 0, numSectors);
                i += numSectors;

                ISectorData[] sectors = new ISectorData[numSectors];
                for (var si = 0; si < numSectors; si++)
                {
                    SectorDataType type = (SectorDataType) data[i];
                    i++;

                    ISectorData sector;
                    switch (type)
                    {
                        case SectorDataType.Normal:
                        case SectorDataType.NormalDeleted:
                            byte[] sectorData = new byte[sectorSize.Size];
                            Array.Copy(data, i, sectorData, 0, sectorSize.Size);
                            i += sectorSize.Size;
                            sector = new NormalSectorData(sectorData, type == SectorDataType.NormalDeleted);
                            break;
                        case SectorDataType.Compressed:
                        case SectorDataType.CompressedDeleted:
                            sector = new CompressedSectorData(data[i], type == SectorDataType.CompressedDeleted);
                            i++;
                            break;
                        case SectorDataType.Unavailable:
                        case SectorDataType.NormalError:
                        case SectorDataType.CompressedError:
                        case SectorDataType.DeletedError:
                        case SectorDataType.CompressedDeletedError:
                            throw new InvalidOperationException("Only IMD disks with no errors are supported");
                        default:
                            throw new InvalidOperationException("Unknown sector data type: " + type);
                    }
                    sectors[sectorNumMap[si] - 1] = sector;
                }
                TrackData track = new TrackData(mode, cylinder, (head & 1) == 1, numSectors, sectorSize, sectors);
                _tracks.Add(track);
            }

        }

        public string ImdVersion { get; private set; }
        public string ImdDate { get; private set; }
        public string ImdComment { get; private set; }

        public int TotalHeads => 2;
        public int TotalCylinders => _tracks.Count/2;

        public byte Get(int cylinder, int head, int sector, int sectorIndex)
        {
            var track = _tracks.FirstOrDefault(t =>
                    t.Cylinder == cylinder &&
                    t.HeadOne == (head == 1)
                );

            if (track == null)
                throw new ArgumentException("Invalid cylinder or head");

            if (sector < 0 || sector > track.NumSectors)
                throw new ArgumentException(nameof(sector));

            if (sectorIndex < 0 || sectorIndex >= track.SectorSize.Size)
                throw new ArgumentException(nameof(sectorIndex));

            return track.Sectors[sector - 1][sectorIndex];
        }

        public SectorSize GetSectorSize(int head, int cylinder)
        {
            var track = _tracks.FirstOrDefault(t =>
                    t.Cylinder == cylinder &&
                    t.HeadOne == (head == 1)
                );

            if (track == null)
                throw new ArgumentException("Invalid cylinder or head");

            return track.SectorSize;
        }

        public int GetNumSectors(int head, int cylinder)
        {
            var track = _tracks.FirstOrDefault(t =>
                    t.Cylinder == cylinder &&
                    t.HeadOne == (head == 1)
                );

            if (track == null)
                throw new ArgumentException("Invalid cylinder or head");

            return track.NumSectors;
        }

        public bool GetDeleted(int cylinder, int head, int sector)
        {
            var track = _tracks.FirstOrDefault(t =>
                    t.Cylinder == cylinder &&
                    t.HeadOne == (head == 1)
                );

            if (track == null)
                throw new ArgumentException("Invalid cylinder or head");

            if (sector < 0 || sector > track.NumSectors)
                throw new ArgumentException(nameof(sector));

            return track.Sectors[sector - 1].Deleted;
        }
    }
}
