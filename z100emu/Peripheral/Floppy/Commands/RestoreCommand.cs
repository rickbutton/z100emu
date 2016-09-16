namespace z100emu.Peripheral.Floppy.Commands
{
    internal class RestoreCommand : ICommand
    {
        private WD1797 _w;
        private StepRate _rate;
        private bool _hld;
        private bool _verify;

        private double _us;

        public RestoreCommand(WD1797 w, StepRate rate, bool hld, bool verify)
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
        }

        public bool Step(double us)
        {
            _us += us;

            if (_w.Track != 0)
            {
                if (_us >= _rate.Rate)
                {
                    _us -= _rate.Rate;
                    _w.Track--;
                }
            }

            if (_w.Track == 0)
            {
                _w.TrackRegister = 0;
                _w.Interrupt();
                return true;
            }
            return false;
        }
    }
}