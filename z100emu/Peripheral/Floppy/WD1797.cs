using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SDL2;
using z100emu.Core;
using z100emu.Peripheral.Floppy.Commands;
using z100emu.Peripheral.Floppy.Disk;

namespace z100emu.Peripheral.Floppy
{
    public class WD1797 : IPortDevice
    {
        private static int MILLISECS_PER_SEC = 1000;
        private static int MICROSECS_PER_MILLISEC = 1000;

        private static int FLAG_R0 = 1;
        private static int FLAG_A0 = 1;

        private static int FLAG_R1 = 2;
        private static int FLAG_U = 2;

        private static int FLAG_V = 4;
        private static int FLAG_E = 4;

        private static int FLAG_H = 8;
        private static int FLAG_L = 8;

        private static int FLAG_T = 16;
        private static int FLAG_M = 16;

        private static int INTERRUPT = 8;

        private static int HEAD_LOAD_TIMING = 50; // milliseconds

        private static int RPM = 300;

        private static int ONE_ROTATION_MICROSECS = (MILLISECS_PER_SEC * MICROSECS_PER_MILLISEC) / (RPM / 60);
        private static int TIME_BETWEEN_INDEXS = ONE_ROTATION_MICROSECS/9;
        private static int INDEX_MARK_TIME = 500; // microseconds

        #region CMD: FORCE INT
        private static int CMD_FORCE_INT_NR_R = 1;
        private static int CMD_FORCE_INT_R_NR = 2;
        private static int CMD_FORCE_INT_PULSE = 4;
        private static int CMD_FORCE_INT_IMM = 8;
        #endregion

        #region Status 1 Flags
        private static int STATUS_1_BUSY = 0;
        private static int STATUS_1_INDEX = 1;
        private static int STATUS_1_TRACK00 = 2;
        private static int STATUS_1_CRC_ERR = 3;
        private static int STATUS_1_SEEK_ERR = 4;
        private static int STATUS_1_HEAD_LOAD = 5;
        private static int STATUS_1_PROTECTED = 6;
        private static int STATUS_1_NOT_READY = 7;
        #endregion

        #region Status 2 Flags
        private static int STATUS_2_BUSY = 0;
        private static int STATUS_2_DRQ = 1;
        private static int STATUS_2_LOST = 2;
        private static int STATUS_2_CRC_ERR = 3;
        private static int STATUS_2_RNF_ERR = 4;
        private static int STATUS_2_RCRD_TYPE = 5;
        private static int STATUS_2_WRI_FAULT = 5;
        private static int STATUS_2_PROTECTED = 6;
        private static int STATUS_2_NOT_READY = 7;
        #endregion

        public readonly IDisk Disk;

        public readonly StatusPort StatusPort;

        private readonly int _portBase;
        private readonly int _port1797Status;
        private readonly int _port1797Command;

        private readonly int _port1797Track;
        private readonly int _port1797Sector;
        private readonly int _port1797Data;

        private byte _cmd;
        private byte _status = 0;

        public byte Track = 0;
        public byte TrackRegister = 0;
        public byte Sector = 1;
        public byte Data;

        private bool _commandDone = true;
        private ICommand _command;

        public StepDirection StepDir = StepDirection.In;

        private double _indexTime = 0;
        public bool Index = false;

        public bool HeadLoad = false;
        public bool HeadLoadTiming = false;
        public bool RecordNotFound = false;
        public bool RecordType = false;
        public bool LostData = false;

        public bool InterruptNRR = false;
        public bool InterruptRNR = false;
        public bool InterruptIndexPulse = false;
        public bool InterruptImmediate = false;

        public WD1797(StatusPort statusPort, int portBase, IDisk disk)
        {
            StatusPort = statusPort;

            _portBase     = portBase;
            _port1797Status = portBase + 0;
            _port1797Command  = portBase + 0;
            _port1797Track    = portBase + 1;
            _port1797Sector   = portBase + 2;
            _port1797Data     = portBase + 3;

            Disk = disk;

            _command = new RestoreCommand(this, StepRate.Rate0, false, false);
        }

        public byte Read(int port)
        {
            if (port == _port1797Status)
            {
                return _status;
            }
            if (port == _port1797Track)
            {
                return TrackRegister;
            }
            if (port == _port1797Sector)
            {
                return Sector;
            }
            if (port == _port1797Data)
            {
                LostData = false;
                StatusPort.Ready = false;
                return Data;
            }
            throw new InvalidOperationException();
        }
        public void Write(int port, byte value)
        {
            if (port == _port1797Command)
            {
                _cmd = value;
                if (!_commandDone)
                    throw new InvalidOperationException("ERROR: sent command while command still running");
                StatusPort.Interrupt = false;
                ExecuteCommand();
                return;
            }
            if (port == _port1797Track)
            {
                if (!_commandDone)
                    throw new InvalidOperationException("ERROR: sent track while cmd still on");
                TrackRegister = value;
                return;
            }
            if (port == _port1797Sector)
            {
                if (!_commandDone)
                    throw new InvalidOperationException("ERROR: sent sector while cmd still on");
                Sector = value;
                return;
            }
            if (port == _port1797Data)
            {
                if (!_commandDone)
                    throw new InvalidOperationException("ERROR: sent data while cmd still on");
                Data = value;
                return;
            }
            throw new NotImplementedException();
        }

        public void Step(double us)
        {
            _indexTime += us;
            if (Index && _indexTime >= INDEX_MARK_TIME)
            {
                Index = false;
                _indexTime = 0;
            }
            else if (!Index && _indexTime >= TIME_BETWEEN_INDEXS)
            {
                Index = true;
                _indexTime = 0;
            }

            if (!_commandDone)
            {
                _commandDone = _command.Step(us);
            }

            if (_command.Type == CommandType.Type1 || _command.Type == CommandType.Type4)
                UpdateStatusOne();
            else if (_command.Type == CommandType.Type2 || _command.Type == CommandType.Type3)
                UpdateStatusTwo();
        }

        internal void Interrupt()
        {
            StatusPort.Interrupt = true;
        }

        private void ExecuteCommand()
        {
            _commandDone = false;

            if ((_cmd >> 4) == 0)
            {
                var rate = StepRate.Get(_cmd & (FLAG_R0 + FLAG_R1));
                var hld = (_cmd & FLAG_H) == FLAG_H;
                var verify = (_cmd & FLAG_V) == FLAG_V;
                _command = new RestoreCommand(this, rate, hld, verify);
                _command.Init();
            }
            else if ((_cmd >> 4) == 1)
            {
                var rate = StepRate.Get(_cmd & (FLAG_R0 + FLAG_R1));
                var hld = (_cmd & FLAG_H) == FLAG_H;
                var verify = (_cmd & FLAG_V) == FLAG_V;
                _command = new SeekCommand(this, rate, hld, verify);
                _command.Init();
            }
            else if ((_cmd >> 5) == 1)
            {
                var rate = StepRate.Get(_cmd & (FLAG_R0 + FLAG_R1));
                var hld = (_cmd & FLAG_H) == FLAG_H;
                var verify = (_cmd & FLAG_V) == FLAG_V;

                var updateReg = (_cmd & FLAG_T) == FLAG_T;
                _command = new StepCommand(this, rate, hld, verify, updateReg, StepDir);
                _command.Init();
            }
            else if ((_cmd >> 5) == 2)
            {
                var rate = StepRate.Get(_cmd & (FLAG_R0 + FLAG_R1));
                var hld = (_cmd & FLAG_H) == FLAG_H;
                var verify = (_cmd & FLAG_V) == FLAG_V;

                var updateReg = (_cmd & FLAG_T) == FLAG_T;
                _command = new StepCommand(this, rate, hld, verify, updateReg, StepDirection.In);
                _command.Init();
            }
            else if ((_cmd >> 5) == 3)
            {
                var rate = StepRate.Get(_cmd & (FLAG_R0 + FLAG_R1));
                var hld = (_cmd & FLAG_H) == FLAG_H;
                var verify = (_cmd & FLAG_V) == FLAG_V;

                var updateReg = (_cmd & FLAG_T) == FLAG_T;
                _command = new StepCommand(this, rate, hld, verify, updateReg, StepDirection.Out);
                _command.Init();
            }
            else if ((_cmd >> 5) == 4)
            {
                var updateSSO = (_cmd & FLAG_U) == FLAG_U;
                var delay = (_cmd & FLAG_E) == FLAG_E;
                var swapSectorLength = (_cmd & FLAG_L) == FLAG_L;
                var multipleRecords = (_cmd & FLAG_M) == FLAG_M;
                _command = new ReadSectorCommand(this, updateSSO, delay, swapSectorLength, multipleRecords);
                _command.Init();
            }
            else if ((_cmd >> 5) == 5)
            {
                throw new NotImplementedException();
            }
            else if ((_cmd >> 3) == 24)
            {
                var updateSSO = (_cmd & FLAG_U) == FLAG_U;
                _command = new ReadAddrCommand(this, updateSSO);
                _command.Init();
            }
            else if ((_cmd >> 3) == 32)
            {
                throw new NotImplementedException();
            }
            else if ((_cmd >> 3) == 34)
            {
                throw new NotImplementedException();
            }
            else if ((_cmd >> 4) == 13)
            {
                InterruptNRR = (_cmd & CMD_FORCE_INT_NR_R) == CMD_FORCE_INT_NR_R;
                InterruptRNR = (_cmd & CMD_FORCE_INT_R_NR) == CMD_FORCE_INT_R_NR;
                InterruptIndexPulse = (_cmd & CMD_FORCE_INT_PULSE) == CMD_FORCE_INT_PULSE;
                InterruptImmediate = (_cmd & CMD_FORCE_INT_IMM) == CMD_FORCE_INT_IMM;
                _command = new InterruptCommand(this);

                if (InterruptImmediate)
                    StatusPort.Interrupt = true;
            }
            Console.WriteLine(_command.GetType().Name);
        }

        private void UpdateStatusOne()
        {
            var busy = !_commandDone;
            var index = Index;
            var track00 = Track == 0;
            var crcError = false;
            var seekError = false;
            var headLoaded = HeadLoad;
            var protect = false;
            var notReady = false;

            _status = (byte)(
                ((busy       ? 1 : 0) << STATUS_1_BUSY) +
                ((index      ? 1 : 0) << STATUS_1_INDEX) +
                ((track00    ? 1 : 0) << STATUS_1_TRACK00) +
                ((crcError   ? 1 : 0) << STATUS_1_CRC_ERR) +
                ((seekError  ? 1 : 0) << STATUS_1_SEEK_ERR) +
                ((headLoaded ? 1 : 0) << STATUS_1_HEAD_LOAD) +
                ((protect    ? 1 : 0) << STATUS_1_PROTECTED) +
                ((notReady   ? 1 : 0) << STATUS_1_NOT_READY)
            );
        }

        private void UpdateStatusTwo()
        {
            var busy = !_commandDone;
            var dataRequest = StatusPort.Ready;
            var lostData = LostData;
            var crcError = false;
            var rnfError = RecordNotFound;
            var recordType = RecordType;
            var protect = false;
            var notReady = false;

            _status = (byte)(
                ((busy        ? 1 : 0) << STATUS_2_BUSY) +
                ((dataRequest ? 1 : 0) << STATUS_2_DRQ) +
                ((lostData    ? 1 : 0) << STATUS_2_LOST) +
                ((crcError    ? 1 : 0) << STATUS_2_CRC_ERR) +
                ((rnfError    ? 1 : 0) << STATUS_2_RNF_ERR) +
                ((recordType  ? 1 : 0) << STATUS_2_WRI_FAULT) +
                ((protect     ? 1 : 0) << STATUS_2_PROTECTED) +
                ((notReady    ? 1 : 0) << STATUS_2_NOT_READY)
            );
        }

        public ushort Read16(int port) { return Read(port); }
        public void Write16(int port, ushort value) { }
        public int[] Ports
            => new[] { _port1797Status, _port1797Track, _port1797Sector, _port1797Data };
    }
}
