using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Emit
{
    public sealed class Label : IArgument
    {
        public CodeGenerator Generator { get; private set; }

        internal ushort? MarkedPosition { get; set; }

        internal List<int> References { get; private set; }

        internal Label(CodeGenerator generator)
        {
            Generator = generator;
            References = new List<int>();
        }
    }
}
