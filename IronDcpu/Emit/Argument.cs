using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Emit
{
    public struct Argument : IArgument
    {
        bool isValid;
        byte value;
        ushort? nextWord;
        Label nextLabel;

        public bool IsValid
        {
            get { return isValid; }
        }

        public byte Value
        {
            get { return value; }
        }

        public ushort? NextWord
        {
            get { return nextWord; }
        }

        public Label NextLabel
        {
            get { return nextLabel; }
        }

        private Argument(byte value, ushort? nextWord = null, Label nextLabel = null)
        {
            this.isValid = true;
            this.value = value;
            this.nextWord = nextWord;
            this.nextLabel = nextLabel;
        }

        internal static void ValidateRegister(Register register)
        {
            if ((byte)register > 0x07)
                throw new ArgumentException("Invalid register value.", "register");
        }

        public static Argument Register(Register register)
        {
            ValidateRegister(register);
            return new Argument((byte)register);
        }

        public static Argument Indirect(Register register)
        {
            ValidateRegister(register);
            return new Argument((byte)(0x08 + register));
        }

        public static Argument Indirect(ushort offset, Register register, Label address = null)
        {
            ValidateRegister(register);
            return new Argument((byte)(0x10 + register), offset, address);
        }

        public static Argument Indirect(ushort offset, Label address = null)
        {
            return new Argument(0x1e, offset, address);
        }

        public static Argument Literal(ushort value, Label address = null)
        {
            if (address == null && value <= 0x1f)
                return new Argument((byte)(0x20 + value));
            else
                return new Argument(0x1f, value, address);
        }

        public static Argument A { get { return Register(IronDcpu.Emit.Register.A); } }
        public static Argument B { get { return Register(IronDcpu.Emit.Register.B); } }
        public static Argument C { get { return Register(IronDcpu.Emit.Register.C); } }
        public static Argument X { get { return Register(IronDcpu.Emit.Register.X); } }
        public static Argument Y { get { return Register(IronDcpu.Emit.Register.Y); } }
        public static Argument Z { get { return Register(IronDcpu.Emit.Register.Z); } }
        public static Argument I { get { return Register(IronDcpu.Emit.Register.I); } }
        public static Argument J { get { return Register(IronDcpu.Emit.Register.J); } }

        public static Argument IndirectA { get { return Indirect(IronDcpu.Emit.Register.A); } }
        public static Argument IndirectB { get { return Indirect(IronDcpu.Emit.Register.B); } }
        public static Argument IndirectC { get { return Indirect(IronDcpu.Emit.Register.C); } }
        public static Argument IndirectX { get { return Indirect(IronDcpu.Emit.Register.X); } }
        public static Argument IndirectY { get { return Indirect(IronDcpu.Emit.Register.Y); } }
        public static Argument IndirectZ { get { return Indirect(IronDcpu.Emit.Register.Z); } }
        public static Argument IndirectI { get { return Indirect(IronDcpu.Emit.Register.I); } }
        public static Argument IndirectJ { get { return Indirect(IronDcpu.Emit.Register.J); } }

        public static Argument Pop  { get { return new Argument(0x18); } }
        public static Argument Peek { get { return new Argument(0x19); } }
        public static Argument Push { get { return new Argument(0x1a); } }

        public static Argument SP       { get { return new Argument(0x1b); } }
        public static Argument PC       { get { return new Argument(0x1c); } }
        public static Argument Overflow { get { return new Argument(0x1d); } }
    }
}
