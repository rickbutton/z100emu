using System;
using SDL2;

namespace z100emu.Peripheral.Floppy.Commands
{
    internal class SeekCommand : ICommand
    {
        private WD1797 _w;
        private StepRate _rate;
        private bool _hld;
        private bool _verify;

        private byte _dest;
        private byte _start;

        private double _us;

        private static int SEEK_TIME = 2;

        public SeekCommand(WD1797 w, StepRate rate, bool hld, bool verify)
        {
            _w = w;
            _rate = rate;
            _hld = hld;
            _verify = verify;
        }

        public CommandType Type => CommandType.Type1;

        public void Init()
        {
            if (!_hld && !_verify)
                _w.HeadLoad = false;
            else if (_hld && !_verify)
                _w.HeadLoad = true; 
            else if (!_hld && _verify) { }
            else
                _w.HeadLoad = true;

            _dest = _w.Data;
            _start = _w.TrackRegister;
        }

        public bool Step(double us)
        {
            _us += us;

            if (_us >= SEEK_TIME)
            {
                //Console.WriteLine($"Seek from {_start} to {_dest}, real track:{_w.Track}");
                if (_dest > _start)
                {
                    _start++;
                    _w.Track++;
                }
                else if (_dest < _start)
                {
                    _start--;
                    _w.Track--;
                }
                //_w.Track = _start;
                _w.TrackRegister = _start;
                _us -= SEEK_TIME;
            }

            if (_w.TrackRegister == _w.Data)
            {
                Console.WriteLine($"Seek done from {_start} to {_dest}, real track:{_w.Track}");
                _w.Interrupt();
                return true;
            }

            return false;
        }
    }
}