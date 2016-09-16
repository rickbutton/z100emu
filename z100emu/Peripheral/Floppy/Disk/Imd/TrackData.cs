namespace z100emu.Peripheral.Floppy.Disk.Imd
{
    public class TrackData
    {
        public TrackData(ImdMode mode, int cylinder, bool headOne, int numSectors, SectorSize sectorSize,
            ISectorData[] sectors)
        {
            Mode = mode;
            Cylinder = cylinder;
            HeadOne = headOne;
            NumSectors = numSectors;
            SectorSize = sectorSize;
            Sectors = sectors;
        }


        public ImdMode Mode { get; private set; }
        public int Cylinder { get; private set; }
        public bool HeadOne { get; private set; }
        public int NumSectors { get; private set; }
        public SectorSize SectorSize { get; private set; }
        public ISectorData[] Sectors { get; private set; }
    }
}