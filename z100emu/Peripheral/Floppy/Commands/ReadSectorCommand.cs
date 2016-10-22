using System;
using System.Collections;

namespace z100emu.Peripheral.Floppy.Commands
{
    internal class ReadSectorCommand : ICommand
    {
        private WD1797 _w;
        private bool _updateSSO;
        private bool _delay;
        private bool _swapSectorLength;
        private bool _multipleRecords;

        private static int DELAY_MILLISECS = 15;

        private static int MICROSECS_PER_READ = 25;

        private int _sectorIdx = 0;
        private double _us;

        public ReadSectorCommand(WD1797 w, bool updateSSO, bool delay, bool swapSectorLength, bool multipleRecords)
        {
            _w = w;
            _updateSSO = updateSSO;
            _delay = delay;
            _swapSectorLength = swapSectorLength;
            _multipleRecords = multipleRecords;
        }

        public CommandType Type => CommandType.Type2;

        public void Init()
        {
            _w.HeadLoad = true;
            _w.RecordNotFound = false;
            _w.RecordType = false;
        }

        public bool Step(double us)
        {
            _us += us;

            /*if (_delay)
            {
                if (_us >= DELAY_MILLISECS)
                {
                    _us -= DELAY_MILLISECS;
                    _delay = false;
                }
                else
                {
                    return false;
                }
            }*/

            var numSectors = _w.Disk.GetNumSectors(_updateSSO ? 1 : 0, _w.Track);

            if (_w.Sector > numSectors)
            {
                _w.RecordNotFound = true;
                _w.Interrupt();
                return true;
            }
            var cylinder = _w.Track;
            var head = _updateSSO ? 1 : 0;
            var sector = _w.Sector;

            if (!_w.StatusPort.Ready)
            {
                _w.Data = _w.Disk.Get(cylinder, head, sector, _sectorIdx);
                _w.StatusPort.Ready = true;
                _sectorIdx++;
            }

            if (_sectorIdx == _w.Disk.GetSectorSize(head, cylinder).Size)
            {
                if (_multipleRecords && _w.Sector <= numSectors)
                {
                    _w.Sector++;
                    _sectorIdx = 0;
                }
                else
                {
                    if (_w.Disk.GetDeleted(cylinder, head, _w.Sector))
                        _w.RecordType = true;

                    _w.Interrupt();
                    return true;
                }
            }
            return false;
        }
    }
}