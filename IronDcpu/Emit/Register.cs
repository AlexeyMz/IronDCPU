using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Emit
{
    public enum Register : byte
    {
        A  = 0x00,
        B  = 0x01,
        C  = 0x02,
        X  = 0x03,
        Y  = 0x04,
        Z  = 0x05,
        I  = 0x06,
        J  = 0x07,
    }
}
