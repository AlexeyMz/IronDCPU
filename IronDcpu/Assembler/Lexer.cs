using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace IronDcpu.Assembler
{
    public sealed class Lexer
    {
        TextReader reader;

        public object TokenValue { get; private set; }

        public TextReader Reader
        {
            get { return reader; }
            set
            {
                TokenValue = null;
                reader = value;
            }
        }

        public Lexer()
        {
        }

        private void SkipSpaces()
        {
            while (reader.Peek() >= 0
                && char.IsWhiteSpace((char)reader.Peek()))
            {
                char ch = (char)reader.Read();
            }
        }

        private bool IsInRange(int character, char start, char end)
        {
            return character >= start && character <= end;
        }

        private bool IsLetterOrUnderscore(int character)
        {
            if (character < 0)
                return false;

            char ch = char.ToLowerInvariant((char)character);
            return ch == '_' || (ch >= 'a' && ch <= 'z');
        }

        private char ReadChar()
        {
            if (reader.Peek() < 0)
                throw new ParseException("Unexpected end of line.");

            char ch = (char)reader.Read();
            if (ch == '\\')
            {
                if (reader.Peek() < 0)
                    throw new ParseException("Unexpected end of line.");

                ch = (char)reader.Read();
                switch (ch)
                {
                    case 'n': return '\n';
                    case 'r': return '\r';
                    case 't': return '\t';
                    case 'v': return '\v';
                    case '\\': return '\\';
                    case '\'': return '\'';
                    case '\"': return '\"';
                    default:
                        throw new ParseException(
                            "Unexpected escaped character '\\" + ch + "'.");
                }
            }
            else
            {
                return ch;
            }
        }

        public Token Read()
        {
            TokenValue = null;
            SkipSpaces();

            int read = reader.Read();
            if (read < 0)
                return Token.Eol;

            char ch = (char)read;
            if (ch == ';')
            {
                reader.ReadToEnd();
                return Token.Eol;
            }
            else if (ch >= '0' && ch <= '9')
            {
                var numberBuilder = new StringBuilder();

                bool isHex = reader.Peek() == 'x';
                if (isHex)
                {
                    // skip "0x" prefix
                    reader.Read();
                }
                else
                {
                    numberBuilder.Append(ch);
                }

                // read digits
                while (IsInRange(reader.Peek(), '0', '9')
                    || IsInRange(reader.Peek(), 'a', 'f')
                    || IsInRange(reader.Peek(), 'A', 'F'))
                {
                    numberBuilder.Append((char)reader.Read());
                }

                var numberStyle = System.Globalization.NumberStyles.None;
                if (isHex)
                    numberStyle |= System.Globalization.NumberStyles.AllowHexSpecifier;

                ushort number;
                if (!ushort.TryParse(numberBuilder.ToString(), numberStyle, null, out number))
                {
                    throw new ParseException("Invalid numeric literal '" +
                        (isHex ? "0x" : "") + numberBuilder.ToString() + "'.");
                }

                TokenValue = number;
                return Token.Number;
            }
            else if (IsLetterOrUnderscore(ch))
            {
                StringBuilder id = new StringBuilder();
                id.Append(ch);

                while (IsLetterOrUnderscore(reader.Peek())
                    || IsInRange(reader.Peek(), '0', '9'))
                {
                    id.Append((char)reader.Read());
                }

                TokenValue = id.ToString();
                return Token.ID;
            }
            else if (ch == '\'')
            {
                ch = ReadChar();
                if (reader.Peek() != '\'')
                    throw new ParseException("End of character literal (') expected.");

                // read (')
                reader.Read();
                TokenValue = ch;
                return Token.Char;
            }
            else if (ch == '\"')
            {
                StringBuilder str = new StringBuilder();
                while (reader.Peek() > 0 && reader.Peek() != '\"')
                {
                    str.Append(ReadChar());
                }

                if (reader.Peek() != '\"')
                    throw new ParseException("End of string literal (\") expected.");

                // read (")
                reader.Read();
                TokenValue = str.ToString();
                return Token.String;
            }
            else
            {
                switch (ch)
                {
                    case ',': return Token.Comma;
                    case ':': return Token.Colon;
                    case '+': return Token.Plus;
                    case '[': return Token.OpenBracket;
                    case ']': return Token.CloseBracket;
                    default:
                        throw new ParseException(
                            "Unexpected character '" + ch.ToString() + "'.");
                }
            }
        }
    }
}
