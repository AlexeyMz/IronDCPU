using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Assembler
{
    public enum Token
    {
        Number,
        ID,
        Comma,
        Colon,
        String,
        Char,
        Plus,
        OpenBracket,
        CloseBracket,
        Eol,
    }
}
