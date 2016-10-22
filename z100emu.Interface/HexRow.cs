using System;
using System.Collections;
using System.Collections.Generic;

namespace z100emu.Interface
{
    public class HexRowEnumerator : IEnumerator<HexRow>
    {
        private ZenithSystem _system;
        private int _offset = -16;

        public HexRowEnumerator(ZenithSystem system)
        {
            _system = system;
        }

        public void Dispose() { }
        public bool MoveNext()
        {
            if (_system.Ram == null)
                return false;

            if (_offset + 16 > 1024 * 1024)
                return false;
            _offset += 16;
            return true;
        }

        public void Reset()
        {
            _offset = -16;
        }

        public HexRow Current => new HexRow(_system, _offset);
        object IEnumerator.Current => Current;
    }

    public class HexRowEnumerable : IEnumerable<HexRow>
    {
        private HexRowEnumerator _e;

        public HexRowEnumerable(ZenithSystem _system)
        {
            _e = new HexRowEnumerator(_system);
        }

        public IEnumerator<HexRow> GetEnumerator() { return _e; }
        IEnumerator IEnumerable.GetEnumerator() { return _e; }
    }

    public class HexRow
    {
        private ZenithSystem _system;
        private int _offset;
        private string[] _cols;

        public HexRow(ZenithSystem system, int offset)
        {
            _system = system;
            _offset = offset;
            
        }

        private void Init()
        {
            if (_cols == null)
            {
                _cols = new string[17];
                _cols[0] = _offset.ToString("X8");
                for (var i = 0; i < 16; i++)
                    _cols[i + 1] = _system.Ram[_offset + i].ToString("X2");
            }
        }

        public string[] Cols
        {
            get { Init(); return _cols; }
        }
    }
}