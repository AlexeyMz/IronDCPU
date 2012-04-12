using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Assembler
{
    public enum IssueKind
    {
        Error,
        Warning,
    }

    public sealed class CodeIssue
    {
        public int Line { get; private set; }
        public IssueKind Kind { get; private set; }
        public string Message { get; private set; }

        public CodeIssue(int line, IssueKind kind, string message)
        {
            if (line < 0)
                throw new ArgumentOutOfRangeException("line", "line must be >= 0.");
            if (message == null)
                throw new ArgumentNullException("message");

            Line = line;
            Kind = kind;
            Message = message;
        }
    }
}
