using System.Collections;

namespace z100emu.Peripheral.Floppy.Commands
{
    internal class ReadAddrCommand : ICommand
    {
        private WD1797 _w;
        private bool _updateSSO;

        private static int MICROSECS_PER_READ = 25;

        private byte[] _steps = new byte[6];
        private int _step = 0;
        private double _us;

        public ReadAddrCommand(WD1797 w, bool updateSSO)
        {
            _w = w;
            _updateSSO = updateSSO;
        }

        public CommandType Type => CommandType.Type3;

        public void Init()
        {
            byte head = (byte)(_updateSSO ? 1 : 0);
            var track = _w.Track;

            _w.HeadLoad = true;
            _w.RecordNotFound = false;
            _steps[0] = track;
            _steps[1] = head;
            _steps[2] = _w.Sector;
            _steps[3] = (byte)_w.Disk.GetSectorSize(head, track).AltSizeField;

            var crc16 = new Crc16(InitialCrcValue.NonZero1);
            var crc = crc16.ComputeChecksumBytes(_steps, 4);
            _steps[4] = crc[0];
            _steps[5] = crc[1];
        }

        public bool Step(double us)
        {
            _us += us;

            if (!_w.StatusPort.Ready)
            {
                if (_step == 0)
                    _w.Sector = _steps[0];

                _w.Data = _steps[_step];
                _w.StatusPort.Ready = true;

                _step++;
            }

            if (_step == 6)
            {
                _w.Interrupt();
                return true;
            }

            return false;
        }
    }
}