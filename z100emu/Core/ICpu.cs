#region License
// The MIT License (MIT)
// 
// Copyright (c) 2016 Rick Button
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using JetBrains.Annotations;

namespace z100emu.Core
{
    /// <summary>
    /// A virtual CPU.
    /// </summary>
    [PublicAPI]
    public interface ICpu
    {
        /// <summary>
        /// Executes one instruction.
        /// </summary>
        /// <returns>Returns number of clocks.</returns>
        [PublicAPI]
        int ProcessSingleInstruction(bool debug = false);
        /// <summary>
        /// Reads from the virtual memory and copies it to a new array.
        /// </summary>
        /// <param name="address">The start address to start from.</param>
        /// <param name="size">The amount of memory to read.</param>
        /// <returns>The array of memory.</returns>
        [NotNull, PublicAPI]
        byte[] ReadBytes(uint address, uint size);
        /// <summary>
        /// Copies the memory from <paramref name="value"/> to the CPU's virtual memory.
        /// </summary>
        /// <param name="address">The start address to write to.</param>
        /// <param name="value">The array of memory to copy.</param>
        [PublicAPI]
        void WriteBytes(uint address, [NotNull] byte[] value);

        void AttachPortDevice(IPortDevice device);

        IPortDevice[] GetPortDevices();
        IPortDevice GetPortDevice(int port);

        void Interrupt(byte interrupt);
    }
}