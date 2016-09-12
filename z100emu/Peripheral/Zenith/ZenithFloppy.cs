using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using z100emu.Core;

namespace z100emu.Peripheral.Zenith
{
    public class ZenithFloppy : IPortDevice
    {
        private class ControlLatch
        {
            internal enum DriveSelect { Drive0, Drive1, Drive2, Drive3 }
            internal enum DriveType { Type525, Type8 }

            private static readonly int CTL_LAT_D_SEL_MASK = 0x3;
            private static readonly int CTL_LAT_D_SEL_SHFT = 0x0;
            private static readonly int CTL_LAT_D_TYP_MASK = 0x1;
            private static readonly int CTL_LAT_D_TYP_SHFT = 0x2;
            private static readonly int CTL_LAT_D_SEN_MASK = 0x1;
            private static readonly int CTL_LAT_D_SEN_SHFT = 0x3;
            private static readonly int CTL_LAT_PRECO_MASK = 0x1;
            private static readonly int CTL_LAT_PRECO_SHFT = 0x4;
            private static readonly int CTL_LAT_OVER8_MASK = 0x1;
            private static readonly int CTL_LAT_OVER8_SHFT = 0x5;
            private static readonly int CTL_LAT_WAITS_MASK = 0x1;
            private static readonly int CTL_LAT_WAITS_SHFT = 0x6;
            private static readonly int CTL_LAT_DOUBD_MASK = 0x1;
            private static readonly int CTL_LAT_DOUBD_SHFT = 0x7;

            public byte Value { get; set; }
            public DriveSelect Select => (DriveSelect) ((Value >> CTL_LAT_D_SEL_SHFT) & CTL_LAT_D_SEL_MASK);
            public DriveType Type => (DriveType)((Value >> CTL_LAT_D_TYP_SHFT) & CTL_LAT_D_TYP_MASK);
            public bool SelectDrives => ((Value >> CTL_LAT_D_SEN_SHFT) & CTL_LAT_D_SEN_MASK) != 0;
            public bool Precomp  => ((Value >> CTL_LAT_PRECO_SHFT) & CTL_LAT_PRECO_MASK) == 0;
            public bool Override8 => ((Value >> CTL_LAT_OVER8_SHFT) & CTL_LAT_OVER8_MASK) != 0;
            public bool WaitState => ((Value >> CTL_LAT_WAITS_SHFT) & CTL_LAT_WAITS_MASK) != 0;
            public bool SingleDensity => ((Value >> CTL_LAT_DOUBD_SHFT) & CTL_LAT_DOUBD_MASK) != 0;
        }

        private class StatusPort
        {
            private static readonly int STAT_INT_SHFT = 0;
            private static readonly int STAT_MTR_SHFT = 1;
            private static readonly int STAT_96T_SHFT = 3;
            private static readonly int STAT_TWO_SHFT = 6;
            private static readonly int STAT_RDY_SHFT = 7;

            public bool Interrupt => false;
            public bool MotorOn => false;
            public bool TPI96 => false;
            public bool TwoSided => false;
            public bool Ready => false;

            public byte Value => (byte)
                    (
                        ((Interrupt ? 1 : 0) << STAT_INT_SHFT) +
                        ((MotorOn   ? 1 : 0) << STAT_MTR_SHFT) +
                        ((TPI96     ? 1 : 0) << STAT_96T_SHFT) +
                        ((TwoSided  ? 1 : 0) << STAT_TWO_SHFT) +
                        ((Ready     ? 1 : 0) << STAT_RDY_SHFT)
                    );
        }

        private class WD1797
        {
            private static int CMD_RESTORE        = 0x00;
            private static int CMD_SEEK           = 0x10;
            private static int CMD_STEP           = 0x20;
            private static int CMD_STEP_IN        = 0x40;
            private static int CMD_STEP_OUT       = 0x60;
            private static int CMD_READ_SECTOR    = 0x80;
            private static int CMD_WRITE_SECTOR   = 0xA0;
            private static int CMD_READ_ADDRESS   = 0xC0;
            private static int CMD_READ_TRACK     = 0xE0;
            private static int CMD_WRITE_TRACK    = 0xF0;
            private static int CMD_FORCE_INT      = 0xD0;

            private static int FLAG_R0 = 1;
            private static int FLAG_A0 = 1;

            private static int FLAG_R1 = 2;
            private static int FLAG_U  = 2;

            private static int FLAG_V  = 4;
            private static int FLAG_E  = 4;

            private static int FLAG_H  = 8;
            private static int FLAG_L  = 8;

            private static int FLAG_T  = 16;
            private static int FLAG_M  = 16;

            #region CMD: FORCE INT
            private static int CMD_FORCE_INT_NR_R = 1;
            private static int CMD_FORCE_INT_R_NR = 2;
            private static int CMD_FORCE_INT_PULSE = 4;
            private static int CMD_FORCE_INT_IMM = 8;
            #endregion

            private static int STATUS_1_BUSY      = 0;
            private static int STATUS_1_INDEX     = 1;
            private static int STATUS_1_TRACK00   = 2;
            private static int STATUS_1_CRC_ERR   = 3;
            private static int STATUS_1_SEEK_ERR  = 4;
            private static int STATUS_1_HEAD_LOAD = 5;
            private static int STATUS_1_PROTECTED = 6;
            private static int STATUS_1_NOT_READY = 7;

            private static int STATUS_2_BUSY      = 0;
            private static int STATUS_2_DRQ       = 1;
            private static int STATUS_2_LOST      = 2;
            private static int STATUS_2_CRC_ERR   = 3;
            private static int STATUS_2_RNF_ERR   = 4;
            private static int STATUS_2_RCRD_TYPE = 5;
            private static int STATUS_2_WRI_FAULT = 5;
            private static int STATUS_2_PROTECTED = 6;
            private static int STATUS_2_NOT_READY = 7;

            private Intel8259 _pic;
            private byte[] _disk;

            public WD1797(Intel8259 pic, byte[] disk)
            {
                _pic = pic;
                _disk = disk;
            }

            private byte _cmd;
            public byte Command
            {
                get
                {
                    return _cmd;
                }
                set
                {
                    _cmd = value;
                    ReadingSector = false;

                    if ((_cmd >> 4) == 0)
                    {
                        Restore();
                        SetTypeOneStatus();
                    }
                    else if ((_cmd >> 4) == 1)
                    {
                        Seek();
                        SetTypeOneStatus();
                    }
                    else if ((_cmd >> 5) == 1)
                    {
                        Step((_cmd & FLAG_T) == FLAG_T);
                        SetTypeOneStatus();
                    }
                    else if ((_cmd >> 5) == 2)
                    {
                        StepIn((_cmd & FLAG_T) == FLAG_T);
                        SetTypeOneStatus();
                    }
                    else if ((_cmd >> 5) == 3)
                    {
                        StepOut((_cmd & FLAG_T) == FLAG_T);
                        SetTypeOneStatus();
                    }
                    else if ((_cmd >> 5) == 4)
                    {
                        var more = ReadSector(true);
                        SetTypeTwoStatus(more);
                    }
                    else if ((_cmd >> 5) == 5)
                    {
                        
                    }
                    else if ((_cmd >> 3) == 24)
                    {
                        
                    }
                    else if ((_cmd >> 3) == 32)
                    {
                        
                    }
                    else if ((_cmd >> 3) == 34)
                    {
                        
                    }
                    else if ((_cmd >> 4) == 13)
                    {
                        _lastInterruptCmd = value;
                        if (InterruptImmediate)
                            _pic.RequestInterrupt(INTERRUPT);
                    }
                }
            }

            public bool Index = false;

            public byte Status = 0;

            private byte _realTrack = 0;
            public byte Track = 0;
            public byte Sector = 1;
            public int SectorIndex = 0;
            public byte Data = 0;

            public bool ReadingSector = false;
            public bool ReadingTrack  = false;
            public bool ReadingAddress = false;

            private byte _lastInterruptCmd = 0;
            private bool InterruptNotReadyToReady => (_lastInterruptCmd & CMD_FORCE_INT_NR_R) == CMD_FORCE_INT_NR_R;
            private bool InterruptReadyToNotReady => (_lastInterruptCmd & CMD_FORCE_INT_R_NR) == CMD_FORCE_INT_R_NR;
            private bool InterruptIndexPulse => (_lastInterruptCmd & CMD_FORCE_INT_PULSE) == CMD_FORCE_INT_PULSE;
            private bool InterruptImmediate => (_lastInterruptCmd & CMD_FORCE_INT_IMM) == CMD_FORCE_INT_IMM;

            private bool _stepIn = true;

            private void Restore()
            {
                _realTrack = 0;
                UpdateTrack();
                _pic.RequestInterrupt(INTERRUPT);
            }

            private void Seek()
            {
                _realTrack = Data;
                UpdateTrack();
                _pic.RequestInterrupt(INTERRUPT);
            }

            private void Step(bool updateTrack)
            {
                if (_stepIn)
                    StepIn(updateTrack);
                else
                    StepOut(updateTrack);
            }

            private void StepIn(bool updateTrack)
            {
                _realTrack++;
                if (updateTrack) UpdateTrack();
                _stepIn = true;
                _pic.RequestInterrupt(INTERRUPT);
            }

            private void StepOut(bool updateTrack)
            {
                _realTrack--;
                if (updateTrack) UpdateTrack();
                _stepIn = false;
                _pic.RequestInterrupt(INTERRUPT);
            }

            public bool ReadSector(bool first = false)
            {
                if (first)
                {
                    ReadingSector = true;
                    ReadingTrack = false;
                    SectorIndex = 0;
                }
                
                var trackIndex = (_realTrack)*9*512;
                var sectorIndex = (Sector-1)*512;
                var index = trackIndex + sectorIndex + SectorIndex;
                var dat = _disk[index];
                Data = dat;
                SectorIndex++;
                return SectorIndex == 512;
            }

            private void UpdateTrack()
            {
                Track = _realTrack;
            }

            public void SetTypeOneStatus()
            {
                bool busy = false;
                bool track00 = _realTrack == 0;
                bool crcError = false;
                bool seekError = false;
                bool headLoaded = true;
                bool protect = false;
                bool notReady = false;

                Status =
                    (byte)(
                        ((busy ? 1 : 0) << STATUS_1_BUSY) +
                        ((Index ? 1 : 0) << STATUS_1_INDEX) +
                        ((track00 ? 1 : 0) << STATUS_1_TRACK00) +
                        ((crcError ? 1 : 0) << STATUS_1_CRC_ERR) +
                        ((seekError ? 1 : 0) << STATUS_1_SEEK_ERR) +
                        ((headLoaded ? 1 : 0) << STATUS_1_HEAD_LOAD) +
                        ((protect ? 1 : 0) << STATUS_1_PROTECTED) +
                        ((notReady ? 1 : 0) << STATUS_1_NOT_READY)
                    );
            }

            public void SetTypeTwoStatus(bool dataRequest)
            {
                bool busy = false;

                bool lostData = false;
                bool crcError = false;
                bool recordNotFound = true;
                bool recordType = false;
                bool protect = false;
                bool notReady = false;

                Status =
                    (byte)(
                        ((busy ? 1 : 0) << STATUS_2_BUSY) +
                        ((dataRequest ? 1 : 0) << STATUS_2_DRQ) +
                        ((lostData ? 1 : 0) << STATUS_2_LOST) +
                        ((crcError ? 1 : 0) << STATUS_2_CRC_ERR) +
                        ((recordNotFound ? 1 : 0) << STATUS_2_RNF_ERR) +
                        ((recordType ? 1 : 0) << STATUS_2_RCRD_TYPE) +
                        ((protect ? 1 : 0) << STATUS_2_PROTECTED) +
                        ((notReady ? 1 : 0) << STATUS_2_NOT_READY)
                    );
            }
        }

        private Intel8259 _pic;

        private static int INTERRUPT = 8;

        private int _portBase;
        private readonly int _port1797Status;
        private readonly int _port1797Command;

        private readonly int _port1797Track;
        private readonly int _port1797Sector;
        private readonly int _port1797Data;

        private readonly int _portControl;
        private readonly int _portStatus;


        private static readonly double RPM = 300;

        private WD1797 _wd;
        private ControlLatch _controlLatch;
        private StatusPort _status;


        public ZenithFloppy(Intel8259 pic, int portBase, byte[] disk)
        {
            _pic = pic;

            _portBase     = portBase;
            _port1797Status = portBase + 0;
            _port1797Command  = portBase + 0;
            _port1797Track    = portBase + 1;
            _port1797Sector   = portBase + 2;
            _port1797Data     = portBase + 3;
            _portControl  = portBase + 4;
            _portStatus   = portBase + 5;

            _wd = new WD1797(pic, disk);
            _controlLatch = new ControlLatch();
            _status = new StatusPort();
        }

        public void Step(double us)
        {
        }

        public byte Read(int port)
        {
            if (port == _port1797Status)
            {
                _wd.Index = !_wd.Index;
                _wd.SetTypeOneStatus();
                return _wd.Status;
            }
            if (port == _port1797Track)
            {
                return _wd.Track;
            }
            if (port == _port1797Sector)
            {
                return _wd.Sector;
            }
            if (port == _port1797Data)
            {
                var dat = _wd.Data;
                if (_wd.ReadingSector)
                {
                    var more = _wd.ReadSector();
                    _wd.SetTypeTwoStatus(more);
                }
                return dat;
            }
            if (port == _portControl)
            {
                return _controlLatch.Value; 
            }
            if (port == _portStatus)
            {
                return _status.Value;
            }

            throw new InvalidOperationException();
        }

        public ushort Read16(int port)
        {
            return Read(port);
        }

        public void Write(int port, byte value)
        {
            if (port == _port1797Command)
            {
                _wd.Command = value;
                return;
            }
            if (port == _port1797Track)
            {
                _wd.Track = value;
                return;
            }
            if (port == _port1797Sector)
            {
                _wd.Sector = value;
                return;
            }
            if (port == _port1797Data)
            {
                _wd.Data = value;
                return;
            }
            if (port == _portControl)
            {
                _controlLatch.Value =value;
                return;
            }
            if (port == _portStatus)
            {
                throw new NotImplementedException();
            }

            throw new InvalidOperationException();
        }

        public void Write16(int port, ushort value)
        {
            Write(port, (byte)value);
        }

        public int[] Ports
            => new[] { _port1797Status, _port1797Track, _port1797Sector, _port1797Data, _portControl, _portStatus};
    }
}
