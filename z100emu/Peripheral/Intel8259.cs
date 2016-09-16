using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using z100emu.Core;

namespace z100emu.Peripheral
{
    public class Intel8259 : IPortDevice
    {
        private const byte PIC1_COMMAND = 0xF2;
        private const byte PIC1_DATA = 0xF3;
        private const byte PIC2_COMMAND = 0xF0;
        private const byte PIC2_DATA = 0xF1;

        private byte _mask    = 0;
        private byte _service = 0;

        private bool[] _request = new bool[8];
        private List<Func<bool>>[] _requestFuncs = new List<Func<bool>>[8];
        private byte[] _icw = new byte[4];   // icw

        // ICW1
        private static int ICW1_NEED_ICW4  = (1 << 0);
        private static int ICW1_SINGLE     = (1 << 1);
        private static int ICW1_4B_VECS    = (1 << 2);
        private static int ICW1_LEVEL_TRIG = (1 << 3);
        private static int ICW1_IS_ICW1    = (1 << 4);

        // ICW2
        // NONE

        // ICW3
        private static int ICW3_SLAVE_0 = (1 << 0);
        private static int ICW3_SLAVE_1 = (1 << 1);
        private static int ICW3_SLAVE_2 = (1 << 2);
        private static int ICW3_SLAVE_3 = (1 << 3);
        private static int ICW3_SLAVE_4 = (1 << 4);
        private static int ICW3_SLAVE_5 = (1 << 5);
        private static int ICW3_SLAVE_6 = (1 << 6);
        private static int ICW3_SLAVE_7 = (1 << 7);

        // ICW4
        private static int ICW4_8086     = (1 << 0);
        private static int ICW4_AUTO_EOI = (1 << 1);
        private static int ICW4_NESTED   = (1 << 4);

        private static int OCW2_REQ_MASK = 0x7;
        private static int OCW2_EOI_MASK = 0xE0;

        private static int OCW3_READ_IRR  = (1 << 0);
        private static int OCW3_READ      = (1 << 1);
        private static int OCW3_POLL_CMD  = (1 << 1);
        private static int OCW_IS_OCW3    = (1 << 3);

        private byte _icwstep;
        private byte _readmode;
        private bool _initialized = false;

        private Intel8259 _slave;

        private int _slaveInt;

        private int _interruptVectorBase { get; set; } = 0;
        private int _interruptVectorSize { get; set; } = 4;

        public bool IsSingle8259 { get; private set; } = false;
        public bool LevelTriggered { get; private set; } = false;

        public bool AutoEOI { get; private set; } = false;

        public Intel8259(Intel8259 slave)
        {
            _readmode = (byte) OCW3_READ_IRR;
            _slave = slave;

            for (var i = 0; i < 8; i++)
                _requestFuncs[i] = new List<Func<bool>>();
        }

        public Intel8259() : this(null) { }

        public void Step()
        {
            for (var i = 0; i < 8; i++)
            {
                if (LevelTriggered)
                    _request[i] = GetInterrupt(i);
                else
                    _request[i] = _request[i] || GetInterrupt(i);
            }
        }

        public int GetInterruptVectorBase(int i)
        {
            if (i > 7 && _slave == null)
                throw new InvalidOperationException("Tried to get interrupt vector base > 7 on slave");

            if (i > 7)
                return _slave.GetInterruptVectorBase(i - 8);
            else
                return _interruptVectorBase;
        }

        public int GetInterruptVectorSize(int i)
        {
            if (i > 7 && _slave == null)
                throw new InvalidOperationException("Tried to get interrupt vector size > 7 on slave");

            if (i > 7)
                return _slave.GetInterruptVectorSize(i - 8);
            else
                return _interruptVectorSize;
        }

        private bool GetInterrupt(int i)
        {
            var req = false;
            foreach (var f in _requestFuncs[i])
            {
                req |= f();
            }
            return req;
        }

        public int? GetNextInterrupt()
        {
            if (!_initialized)
                return null;

            for (var i = 0; i < 8; i++)
            {
                for (var z = 0; z <= i; z++)
                    if ((_service & (1 << z)) != 0)
                        return null;

                var m = (_mask & (1 << i)) == 0;
                var r = _request[i];
                if (m && r)
                {
                    if (_slave != null && i == _slaveInt)
                    {
                        return _slave.GetNextInterrupt();
                    }
                    else
                    {
                        return i;
                    }
                }
            }
            return null;
        }

        public void RegisterInterrupt(int i, Func<bool> func)
        {
            _requestFuncs[i].Add(func);
        }

        public void AckInterrupt()
        {
            for (int i = 0; i < 8; i++)
            {
                if (_request[i])
                {
                    AckInterrupt(i);
                    break;
                }
            }
        }

        private void AckInterrupt(int i)
        {
            if (i > 7)
            {
                if (_slave == null)
                    throw new InvalidOperationException("Tried to ack interrupt > 7 on slave");
                _slave.AckInterrupt(i - 8);
            }
            else
            {
                _request[i] = false;
                RequestService(i);
                if (AutoEOI)
                    AckService(i);
            }
        }

        public void RequestService(int i)
        {
            if (i > 7)
            {
                if (_slave == null)
                    throw new InvalidOperationException("Tried to request service > 7 on slave");
                _slave.RequestService(i - 8);
                _service = (byte)(_service | (1 << _slaveInt));
            }
            else
            {
                _service = (byte)(_service | (1 << i));
            }
        }

        public void AckService(int i)
        {
            if (i > 7)
            {
                if (_slave == null)
                    throw new InvalidOperationException("Tried to ack service > 7 on slave");
                _slave.AckService(i - 8);
                _service = (byte)(_service & ~(1 << _slaveInt));
            }
            else
            {
                _service = (byte)(_service & ~(1 << i));
            }
        }

        public byte Read(int port)
        {
            if ((port == PIC1_DATA) || (port == PIC2_DATA))
            {
                return _mask;
            }
            else
            {
                if ((_readmode & OCW3_READ_IRR) == OCW3_READ_IRR)
                {
                    var req = 0;
                    for (var i = 0; i < 8; i++)
                        req += ((_request[i] ? 1 : 0) << i);
                    return (byte)req;
                }
                else
                {
                    return _service;
                }
            }
        }

        public ushort Read16(int port) { return Read(port); }

        public void Write(int port, byte data)
        {
            if ((port == PIC1_DATA) || (port == PIC2_DATA))
            {
                if ((_icwstep == 2) && ((byte) (_icw[1] & ICW1_SINGLE) == ICW1_SINGLE))
                {
                    // skip ICW3
                    _icwstep = 3;
                }
                if( _icwstep < 4 )
                {
                    if (_icwstep == 1) // ICW2
                    {
                        _interruptVectorBase = data;
                    }
                    else if (_icwstep == 2) // ICW3
                    {
                        if (_slave != null)
                        {
                            for (var i = 0; i < 8; i++)
                            {
                                if (((data >> i) & 1) == 1)
                                {
                                    _slaveInt = i;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            _slaveInt = data;
                        }
                    }
                    else if (_icwstep == 3) // ICW4
                    {
                        AutoEOI = (data & ICW4_AUTO_EOI) == ICW4_AUTO_EOI;
                    }

                    _icw[_icwstep++] = data;
                    return;
                }
                _initialized = true;
                _mask = data;
            }
            else
            {
                if ((data & ICW1_IS_ICW1) == ICW1_IS_ICW1)
                {
                    _initialized = false;
                    _icwstep = 0;
                    _mask = 0;
                    _icw[_icwstep++] = data;

                    IsSingle8259 = (data & ICW1_SINGLE) == ICW1_SINGLE;
                    LevelTriggered = (data & ICW1_LEVEL_TRIG) == ICW1_LEVEL_TRIG;

                } else if ((data & OCW_IS_OCW3) == OCW_IS_OCW3)
                {
                    if ((data & OCW3_READ) == OCW3_READ)
                    {
                        _readmode = (byte)(data & OCW3_READ_IRR);
                    }
                    else
                    {
                        _readmode = 0;
                    }
                }
                else
                {
                    // ocw2
                    var req = data & OCW2_REQ_MASK;
                    var eoi = (data & OCW2_EOI_MASK) >> 5;
                    if (eoi != 1)
                        throw new NotImplementedException();

                    for (int i = 0; i < 8; i++)
                    {
                        if (((_service >> i) & 1) == 1)
                        {
                            AckService(i);
                            break;
                        }
                    }
                }
            }
        }

        public void Write16(int port, ushort data) { Write(port, (byte)data); }

        public int[] Ports => _slave != null ? new int[] {0xF2, 0xF3} : new int[] {0xF0, 0xF1};
    }
}
