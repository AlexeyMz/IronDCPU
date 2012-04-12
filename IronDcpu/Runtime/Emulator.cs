using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Runtime
{
    public sealed class DcpuEmulator
    {
        ushort[] memory = new ushort[ushort.MaxValue];

        public DcpuEmulator()
        {
        }
    }
}
