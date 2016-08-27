using System;
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
        private byte _request = 0;
        private byte _service = 0;
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
        private static int OCW2_EOI_MASK = 0x70;

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

        public bool AutoEOI { get; private set; } = false;

        public Intel8259(Intel8259 slave)
        {
            _readmode = (byte) OCW3_READ_IRR;
            _slave = slave;
        }

        public Intel8259() : this(null) { }

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

        public int? GetNextInterrupt()
        {
            if (!_initialized)
                return null;

            for (var i = 0; i < 8; i++)
            {
                for (var z = 0; z <= i; z++)
                    if ((_service & (1 << i)) == 1)
                        return null;

                var m = (_mask & (1 << i)) == 0;
                var r = (_request & (1 << i)) != 0;
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

        public void RequestInterrupt(int i)
        {
            if (i > 7)
            {
                if (_slave == null)
                    throw new InvalidOperationException("Tried to request interrupt > 7 on slave");
                _slave.RequestInterrupt(i - 8);
                _request = (byte)(_request | (1 << _slaveInt));
            }
            else
            {
                _request = (byte)(_request | (1 << i));
            }
        }

        public void AckInterrupt(int i)
        {
            if (i > 7)
            {
                if (_slave == null)
                    throw new InvalidOperationException("Tried to ack interrupt > 7 on slave");
                _slave.AckInterrupt(i - 8);
                _request = (byte)(_request & ~(1 << _slaveInt));
            }
            else
            {
                _request = (byte)(_request & ~(1 << i));
                if (!AutoEOI)
                    RequestService(i);    
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
                    return _request;
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
                    var eoi = (data & OCW2_EOI_MASK) >> 4;
                    if (eoi != 1)
                        throw new NotImplementedException();

                    AckService(req);
                }
            }
        }

        public void Write16(int port, ushort data) { Write(port, (byte)data); }

        public int[] Ports => _slave != null ? new int[] {0xF2, 0xF3} : new int[] {0xF0, 0xF1};
    }
}
