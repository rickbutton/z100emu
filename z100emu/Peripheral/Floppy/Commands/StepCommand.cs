namespace z100emu.Peripheral.Floppy.Commands
{
    internal class StepCommand : ICommand
    {
        private WD1797 _w;
        private StepRate _rate;
        private bool _hld;
        private bool _verify;

        private bool _updateReg;
        private StepDirection _stepDir;

        private double _us;

        public StepCommand(WD1797 w, StepRate rate, bool hld, bool verify, bool updateReg, StepDirection stepDir)
        {
            _w = w;
            _rate = rate;
            _hld = hld;
            _verify = verify;
            _updateReg = updateReg;
            _stepDir = stepDir;
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

            _w.StepDir = _stepDir;
        }

        public bool Step(double us)
        {
            _us += us;

            if (_us >= _rate.Rate)
            {
                _us -= _rate.Rate;

                if (_stepDir == StepDirection.In)
                {
                    _w.Track++;
                }
                else
                {
                    _w.Track--;
                }
                if (_updateReg)
                    _w.TrackRegister = _w.Track;
                return true;
            }

            return false;
        }
    }
}