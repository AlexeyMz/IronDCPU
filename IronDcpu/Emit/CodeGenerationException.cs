using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronDcpu.Emit
{
    [Serializable]
    public class CodeGenerationException : Exception
    {
        public CodeGenerationException()
        {
        }

        public CodeGenerationException(string message)
            : base(message)
        {
        }

        public CodeGenerationException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected CodeGenerationException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
