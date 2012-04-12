using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace IronDcpu.Emit
{
    public sealed class CodeGenerator
    {
        List<Label> labels = new List<Label>();

        Stream stream;

        private int CurrentBytePosition
        {
            get { return checked((int)stream.Position); }
        }

        public Stream OutputStream
        {
            get { return stream; }
        }

        public bool Finished { get; private set; }

        public CodeGenerator(Stream outputStream)
        {
            if (!(outputStream.CanSeek && outputStream.CanWrite))
            {
                throw new ArgumentException(
                    "outputStream must support seeking and writing.", "outputStream");
            }

            stream = outputStream;
        }

        public Label DefineLabel()
        {
            AssertNotFinished();

            Label label =  new Label(this);
            labels.Add(label);
            return label;
        }

        private void ValidateLabel(Label label)
        {
            if (label.Generator != this)
                throw new ArgumentException("Label must be defined in this generator.");
        }

        private void AssertNotFinished()
        {
            if (Finished)
                throw new InvalidOperationException("Code generation was finished.");
        }

        private byte GetArgumentValue(IArgument argument,
            int nextWordPosition, out ushort? nextWord)
        {
            nextWord = null;

            if (argument is Argument)
            {
                Argument arg = (Argument)argument;
                if (!arg.IsValid)
                    throw new ArgumentException("argument must be valid.");
                if (arg.NextLabel != null)
                {
                    ValidateLabel(arg.NextLabel);
                    arg.NextLabel.References.Add(nextWordPosition);
                }
                
                nextWord = arg.NextWord;
                return arg.Value;
            }
            else if (argument is Label)
            {
                Label label = (Label)argument;
                ValidateLabel(label);
                label.References.Add(nextWordPosition);

                // return 0 as label value to patch it later
                nextWord = 0;
                return 0x1f;
            }
            else
            {
                throw new CodeGenerationException("Unknown argument type.");
            }
        }

        internal static ushort ReadWord(Stream stream)
        {
            // read using BigEndian byte order
            return (ushort)((stream.ReadByte() << 8) + stream.ReadByte());
        }

        internal static void WriteWord(Stream stream, ushort word)
        {
            // write using BigEndian byte order
            stream.WriteByte((byte)(word >> 8));
            stream.WriteByte((byte)(word & 0x00FF));
        }

        public void MarkLabel(Label label)
        {
            AssertNotFinished();
            ValidateLabel(label);

            if (label.MarkedPosition != null)
                throw new CodeGenerationException("label already marked another position.");

            label.MarkedPosition = checked((ushort)(CurrentBytePosition / 2));
        }

        public void Emit(OpCode opCode, IArgument a, IArgument b)
        {
            AssertNotFinished();
            if (!opCode.IsBasic)
                throw new ArgumentException("Non-basic OpCodes cannot have 2 arguments.", "opCode");

            ushort? aNextWord;
            ushort? bNextWord;

            int nextWordPosition = CurrentBytePosition + 2;
            ushort aValue = GetArgumentValue(a, nextWordPosition, out aNextWord);
            
            if (aNextWord != null)
                nextWordPosition += 2;

            ushort bValue = GetArgumentValue(b, nextWordPosition, out bNextWord);

            ushort instruction = (ushort)(opCode.Code | (aValue << 4) | (bValue << 10));
            WriteWord(stream, instruction);

            if (aNextWord != null)
                WriteWord(stream, aNextWord.Value);
            if (bNextWord != null)
                WriteWord(stream, bNextWord.Value);
        }

        public void Emit(OpCode opCode, IArgument a)
        {
            AssertNotFinished();
            if (opCode.IsBasic)
                throw new ArgumentException("Basic OpCodes cannot have only 1 argument.", "opCode");

            ushort? aNextWord;
            int nextWordPosition = CurrentBytePosition + 2;
            ushort aValue = GetArgumentValue(a, nextWordPosition, out aNextWord);

            ushort instruction = (ushort)(opCode.Code | aValue << 10);
            WriteWord(stream, instruction);

            if (aNextWord != null)
                WriteWord(stream, aNextWord.Value);
        }

        public void EmitData(Label label)
        {
            AssertNotFinished();

            ushort? nextWord;
            GetArgumentValue(label, CurrentBytePosition, out nextWord);
            WriteWord(stream, 0x0000);
        }

        private void PatchLabels()
        {
            stream.Flush();
            bool error = false;
            
            foreach (Label label in labels)
            {
                if (label.MarkedPosition == null)
                {
                    error = true;
                    continue;
                }

                for (int i = 0; i < label.References.Count; i++)
                {
                    int referencePosition = label.References[i];
                    
                    stream.Position = referencePosition;
                    ushort word = ReadWord(stream);
                    word = (ushort)unchecked(word + label.MarkedPosition.Value);

                    stream.Position = referencePosition;
                    WriteWord(stream, word);
                }
            }

            if (error)
                throw new CodeGenerationException("Label wasn't mark any position.");
        }

        public void Finish()
        {
            AssertNotFinished();

            PatchLabels();
            stream.Flush();
            Finished = true;
        }
    }
}
