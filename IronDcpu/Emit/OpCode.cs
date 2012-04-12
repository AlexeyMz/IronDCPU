using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Emit
{
    public struct OpCode
    {
        ushort code;

        public ushort Code
        {
            get { return code; }
        }

        public bool IsBasic
        {
            get { return (code & 0x000f) != 0; }
        }

        internal OpCode(ushort code)
        {
            bool basic = (code & 0x000F) != 0;
            if (basic)
                this.code = (ushort)(code & 0x000F);
            else
                this.code = (ushort)(code & 0x03F0);
        }

        public override int GetHashCode()
        {
            return code;
        }

        public override bool Equals(object obj)
        {
            OpCode? opCode = obj as OpCode?;
            if (opCode == null)
                return false;

            return Equals(opCode.Value);
        }

        public bool Equals(OpCode opCode)
        {
            return this.code == opCode.code;
        }
    }

    public static class OpCodes
    {
        public static OpCode Set { get { return new OpCode(0x1); } }
        public static OpCode Add { get { return new OpCode(0x2); } }
        public static OpCode Sub { get { return new OpCode(0x3); } }
        public static OpCode Mul { get { return new OpCode(0x4); } }
        public static OpCode Div { get { return new OpCode(0x5); } }
        public static OpCode Mod { get { return new OpCode(0x6); } }
        public static OpCode Shl { get { return new OpCode(0x7); } }
        public static OpCode Shr { get { return new OpCode(0x8); } }
        public static OpCode And { get { return new OpCode(0x9); } }
        public static OpCode Bor { get { return new OpCode(0xA); } }
        public static OpCode Xor { get { return new OpCode(0xB); } }
        public static OpCode Ife { get { return new OpCode(0xC); } }
        public static OpCode Ifn { get { return new OpCode(0xD); } }
        public static OpCode Ifg { get { return new OpCode(0xE); } }
        public static OpCode Ifb { get { return new OpCode(0xF); } }
        
        public static OpCode Jsr { get { return new OpCode(0x010); } }
    }
}
