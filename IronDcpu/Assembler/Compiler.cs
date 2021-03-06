﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;

using IronDcpu.Emit;

namespace IronDcpu.Assembler
{
    // <Grammar>
    //
    //  Program  ::= Line { NELINE Line }
    //  Line     ::= { Label } [Command]
    //  Label    ::=  ':' ID
    //  Command  ::= ID Args
    //  Args     ::= Arg { ',' Arg } | #
    //  Arg      ::= '[' Expr ']' | Simple | STRING
    //  Expr     ::= Simple { '+' Simple }
    //  Simple   ::= ID | Register | Special | NUMBER | CHAR
    //  Register ::= A | B | C | X | Y | Z | I | J
    //  Special  ::= PC | SP | O | PUSH | PEEK | POP
    //
    // </Grammar>

    public sealed class Compiler
    {
        Lexer lexer = new Lexer();
        CodeGenerator gen;

        Dictionary<string, Label> labels = new Dictionary<string, Label>();
        Dictionary<Label, int> labelLines = new Dictionary<Label, int>();

        List<CodeIssue> errorsAndWarnings = new List<CodeIssue>();

        Dictionary<string, OpCode> commandOpCodes =
            typeof(OpCodes).GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)
                .ToDictionary(
                    p => p.Name.ToLowerInvariant(),
                    p => (OpCode)p.GetValue(null, null));

        Dictionary<string, Register> registers =
            typeof(Register).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)
                .ToDictionary(
                    f => f.Name.ToLowerInvariant(),
                    f => (Register)f.GetValue(null));

        public IList<CodeIssue> ErrorsAndWarnings { get; private set; }

        public MemoryStream CompileResult { get; private set; }

        public Compiler()
        {
            ErrorsAndWarnings = new ReadOnlyCollection<CodeIssue>(
                errorsAndWarnings);
        }

        public void Compile(TextReader assemblySourceReader,
            Action<int, string, Tuple<long, byte[]>> listingHandler = null)
        {
            gen = new CodeGenerator(new MemoryStream());

            errorsAndWarnings.Clear();
            CompileResult = null;

            int lineIndex = 0;
            long lastPositionInStream = 0;

            List<long> lineOffsets = null;
            List<string> lines = null;

            if (listingHandler != null)
            {
                lineOffsets = new List<long>();
                lines = new List<string>();
            }

            string line = assemblySourceReader.ReadLine();
            while (line != null)
            {
                try
                {
                    CompileLine(lineIndex, line);
                }
                catch (ParseException ex)
                {
                    errorsAndWarnings.Add(
                        new CodeIssue(lineIndex, IssueKind.Error, ex.Message));
                }

                if (listingHandler != null)
                {
                    lineOffsets.Add(lastPositionInStream);
                    lines.Add(line);
                }

                lineIndex++;
                lastPositionInStream = gen.OutputStream.Position;
                line = assemblySourceReader.ReadLine();
            }

            CheckLabels();

            try
            {
                gen.Finish();
            }
            catch (CodeGenerationException)
            { // eat 'incorrect label' exceptions
            }

            if (listingHandler != null)
            {
                var stream = (MemoryStream)gen.OutputStream;
                stream.Position = 0;

                for (int i = 0; i < lines.Count; i++)
                {
                    long next = (i != lines.Count - 1)
                        ? lineOffsets[i + 1] : stream.Length;

                    byte[] bytes = new byte[next - lineOffsets[i]];

                    // read generated bytes for line i
                    for (int j = 0; j < bytes.Length; j++)
                    {
                        bytes[j] = (byte)stream.ReadByte();
                    }

                    listingHandler(i, lines[i], Tuple.Create(lineOffsets[i], bytes));
                }
            }

            if (errorsAndWarnings.Count(iss => iss.Kind == IssueKind.Error) == 0)
            {
                CompileResult = (MemoryStream)gen.OutputStream;
            }

            labels.Clear();
            labelLines.Clear();
        }

        private void CheckLabels()
        {
            foreach (var pair in labels)
            {
                if (pair.Value.MarkedPosition == null)
                {
                    errorsAndWarnings.Add(new CodeIssue(
                        0, IssueKind.Error,
                        "Label '" + pair.Key + "' didn't mark any position."));
                }
                else if (pair.Value.References.Count == 0)
                {
                    errorsAndWarnings.Add(new CodeIssue(
                        labelLines[pair.Value], IssueKind.Warning,
                        "Label '" + pair.Key + "' doesn't have any references."));
                }
            }
        }

        private Label GetLabel(string name)
        {
            Label label;
            if (!labels.TryGetValue(name, out label))
            {
                label = gen.DefineLabel();
                labels.Add(name, label);
            }
            
            return label;
        }

        private void CompileLine(int lineIndex, string line)
        {
            lexer.Reader = new StringReader(line);

            Token token = lexer.Read();
            while (token == Token.Colon)
            {
                ReadExpectedToken(Token.ID);
                
                Label label = GetLabel((string)lexer.TokenValue);
                if (label.MarkedPosition != null)
                {
                    throw new ParseException(string.Format(
                        "Label '{0}' already marked another position.",
                        (string)lexer.TokenValue));
                }

                gen.MarkLabel(label);
                labelLines.Add(label, lineIndex);

                token = lexer.Read();
            }

            if (token == Token.Eol)
                return;

            if (token != Token.ID)
                throw new ParseException(string.Format(
                    "Unexpected token '{0}', expected '{1}' or '{2}'.",
                    token, Token.ID, Token.Eol));

            ParseCommand();
        }

        private void ParseCommand()
        {
            string command = ((string)lexer.TokenValue).ToLowerInvariant();
            if (command == "dat")
            {
                ParseData();
            }
            else if (commandOpCodes.ContainsKey(command))
            {
                OpCode opCode = commandOpCodes[command];
                IArgument arg1 = ReadArgument();

                if (opCode.IsBasic)
                {
                    ReadExpectedToken(Token.Comma);
                    IArgument arg2 = ReadArgument();

                    gen.Emit(opCode, arg1, arg2);
                }
                else
                {
                    // non-basic command
                    gen.Emit(opCode, arg1);
                }
            }
            else
            {
                throw new ParseException("Unknown command '" + command + "'.");
            }
        }

        private void ParseData()
        {
            Stream stream = gen.OutputStream;

            Token token = lexer.Read();
            while (token != Token.Eol)
            {
                if (token == Token.Number)
                {
                    CodeGenerator.WriteWord(stream, (ushort)lexer.TokenValue);
                }
                else if (token == Token.Char)
                {
                    // write unicode character as its code
                    CodeGenerator.WriteWord(stream, (char)lexer.TokenValue);
                }
                else if (token == Token.String)
                {
                    string text = (string)lexer.TokenValue;
                    for (int i = 0; i < text.Length; i++)
                    {
                        CodeGenerator.WriteWord(stream, text[i]);
                    }
                }
                else if (token == Token.ID)
                {
                    Label label = GetLabel((string)lexer.TokenValue);
                    gen.EmitData(label);
                }
                else
                {
                    throw new ParseException("Arguments to data directive must be constant.");
                }

                token = lexer.Read();
                if (token != Token.Eol && token != Token.Comma)
                {
                    throw new ParseException(string.Format(
                        "Unexpected token '{0}', expected '{1}' or '{2}'.",
                        token, Token.Comma, Token.Eol));
                }

                token = lexer.Read();
            }
        }

        private IArgument ReadArgument()
        {
            Token token = lexer.Read();
            if (token == Token.Number)
            {
                return Argument.Literal((ushort)lexer.TokenValue);
            }
            else if (token == Token.Char)
            {
                return Argument.Literal((char)lexer.TokenValue);
            }
            else if (token == Token.ID)
            {
                string id = (string)lexer.TokenValue;
                string lowered = id.ToLowerInvariant();

                if (registers.ContainsKey(lowered))
                    return Argument.Register(registers[lowered]);
                else
                {
                    switch (lowered)
                    {
                        case "pc":   return Argument.PC;
                        case "sp":   return Argument.SP;
                        case "o":    return Argument.Overflow;
                        case "push": return Argument.Push;
                        case "peek": return Argument.Peek;
                        case "pop":  return Argument.Pop;
                        default:
                            return GetLabel(id);
                    }
                }

            }
            else if (token == Token.OpenBracket)
            {
                return ReadIndirectArgument();
            }
            else
            {
                throw new ParseException(string.Format(
                    "Unexpected toke '{0}', expected '{1}', '{2}', '{3}' or '{4}'",
                    token, Token.Number, Token.Char, Token.ID, Token.OpenBracket));
            }
        }

        private IArgument ReadIndirectArgument()
        {
            Register? register = null;
            ushort value = 0;
            Label label = null;

            Token token;
            do
            {
                token = lexer.Read();
                if (token == Token.Number)
                    value = (ushort)unchecked(value + (ushort)lexer.TokenValue);
                else if (token == Token.Char)
                    value = (ushort)unchecked(value + (char)lexer.TokenValue);
                else if (token == Token.ID)
                {
                    string name = (string)lexer.TokenValue;
                    string lowered = name.ToLowerInvariant();

                    if (registers.ContainsKey(lowered))
                    {
                        if (register != null)
                            throw new ParseException("Indirect value supports only one register in expression.");

                        register = registers[lowered];
                    }
                    else
                    {
                        if (label != null)
                            throw new ParseException("Indirect value supports only one label in expression.");

                        label = GetLabel(name);
                    }
                }

                token = lexer.Read();
                if (token != Token.Plus && token != Token.CloseBracket)
                {
                    throw new ParseException(string.Format(
                        "Unexpected token '{0}', expected '{1}' or '{2}'.",
                        token, Token.Plus, Token.CloseBracket));
                }
            }
            while (token != Token.CloseBracket);
            
            if (register != null)
            {
                if (value == 0 && label == null)
                    return Argument.Indirect(register.Value);
                else
                    return Argument.Indirect(value, register.Value, label);
            }
            else
            {
                return Argument.Indirect(value, label);
            }
        }

        private void ReadExpectedToken(Token expected)
        {
            Token token = lexer.Read();
            if (token != expected)
            {
                throw new ParseException(string.Format(
                    "Unexpected token '{0}', expected '{1}'.",
                    token, expected));
            }
        }
    }
}
