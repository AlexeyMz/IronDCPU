using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using IronDcpu.Emit;
using IronDcpu.Assembler;

namespace DcpuTest
{
    static class Program
    {
        static string code1 =
@"; Assembler test for DCPU
; by Markus Persson

:start
	set i, 0
	set j, 0
	set b, 0xf100

:nextchar
	set a, [data+i]
	ife a, 0
	    set PC, end
	ifg a, 0xff
	    set PC, setcolor
	bor a, b
	set [0x8000+j], a
	add i, 1
	add j, 1
	set PC, nextchar

:setcolor
	set b, a
	and b, 0xff
	shl b, 8
	ifg a, 0x1ff
	    add b, 0x80
	add i, 1
	set PC, nextchar

:end
	set PC, end

:data
	dat 0x170, ""Hello "", 0x2e1, ""world"", 0x170, "", how are you?""";

        static string code2 =
@"; Try some basic stuff
                SET A, 0x30              ; 7c01 0030
                SET [0x1000], 0x20       ; 7de1 1000 0020
                SUB A, [0x1000]          ; 7803 1000
                IFN A, 0x10              ; c00d 
                    SET PC, crash         ; 7dc1 001a [*]
                      
; Do a loopy thing
                SET I, 10                ; a861
                SET A, 0x2000            ; 7c01 2000
:loop           SET [0x2000+I], [A]      ; 2161 2000
                SUB I, 1                 ; 8463
                IFN I, 0                 ; 806d
                    SET PC, loop          ; 7dc1 000d [*]
        
; Call a subroutine
                SET X, 0x4               ; 9031
                JSR testsub              ; 7c10 0018 [*]
                SET PC, crash            ; 7dc1 001a [*]
        
:testsub        SHL X, 4                 ; 9037
                SET PC, POP              ; 61c1
                        
; Hang forever. X should now be 0x40 if everything went right.
:crash          SET PC, crash            ; 7dc1 001a [*]
        
; [*]: Note that these can be one word shorter and one cycle faster by using the short form (0x00-0x1f) of literals,
;      but my assembler doesn't support short form labels yet. ";

        static void Main(string[] args)
        {
            CompilerTest(code1);
        }

        static void CompilerTest(string code)
        {
            Compiler compiler = new Compiler();
            compiler.Compile(new StringReader(code),
                (lineIndex, line, compiled) =>
                {
                    Console.WriteLine("{0,3}: {1}", lineIndex + 1, line);

                    if (compiled.Item2.Length > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("{0:X4}: ", compiled.Item1 >> 1);
                        for (int i = 0; i < compiled.Item2.Length; i += 2)
                        {
                            Console.Write("{0:X2}{1:X2} ", compiled.Item2[i], compiled.Item2[i + 1]);
                        }
                        Console.WriteLine();
                    }

                    var lineIssues = compiler.ErrorsAndWarnings
                        .Where(iss => iss.Line == lineIndex);

                    foreach (CodeIssue issue in lineIssues)
                    {
                        Console.ForegroundColor = issue.Kind == IssueKind.Error
                            ? ConsoleColor.Red : ConsoleColor.Yellow;

                        Console.WriteLine("[{0}] {1}", issue.Kind, issue.Message);
                    }

                    Console.ResetColor();
                });

            if (compiler.CompileResult != null)
            {
                Console.WriteLine();
                PrintDump(compiler.CompileResult.ToArray(), 8);
            }

            Console.ReadLine();
        }

        static void LexerTest(string code)
        {
            Lexer lexer = new Lexer();

            var lines = code.Split(new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Console.WriteLine("{0,3}: {1}", i + 1, line);

                lexer.Reader = new StringReader(line);
                try
                {
                    var tokens = new List<Tuple<Token, object>>();

                    Token token = lexer.Read();
                    while (token != Token.Eol)
                    {
                        tokens.Add(Tuple.Create(token, lexer.TokenValue));
                        token = lexer.Read();
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(string.Join(" ", tokens.Select(t =>
                        t.Item1.ToString() + (t.Item2 == null ? "" : "<" + t.Item2.ToString() + ">"))));
                }
                catch (ParseException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                }

                Console.ResetColor();
            }

            Console.ReadLine();
        }

        static void CodeGeneratorTest()
        {
            CodeGenerator gen = new CodeGenerator(new MemoryStream());

            Label loop    = gen.DefineLabel();
            Label testsub = gen.DefineLabel();
            Label crash   = gen.DefineLabel();

            gen.Emit(OpCodes.Set, Argument.A, Argument.Literal(0x30));
            gen.Emit(OpCodes.Set, Argument.Indirect(0x1000), Argument.Literal(0x20));
            gen.Emit(OpCodes.Sub, Argument.A, Argument.Indirect(0x1000));
            gen.Emit(OpCodes.Ifn, Argument.A, Argument.Literal(0x10));
            gen.Emit(OpCodes.Set, Argument.PC, crash);

            gen.Emit(OpCodes.Set, Argument.I, Argument.Literal(10));
            gen.Emit(OpCodes.Set, Argument.A, Argument.Literal(0x2000));
            gen.MarkLabel(loop);
            gen.Emit(OpCodes.Set, Argument.Indirect(0x2000, Register.I), Argument.IndirectA);
            gen.Emit(OpCodes.Sub, Argument.I, Argument.Literal(1));
            gen.Emit(OpCodes.Ifn, Argument.I, Argument.Literal(0));
            gen.Emit(OpCodes.Set, Argument.PC, loop);

            gen.Emit(OpCodes.Set, Argument.X, Argument.Literal(0x4));
            gen.Emit(OpCodes.Jsr, testsub);
            gen.Emit(OpCodes.Set, Argument.PC, crash);

            gen.MarkLabel(testsub);
            gen.Emit(OpCodes.Shl, Argument.X, Argument.Literal(4));
            gen.Emit(OpCodes.Set, Argument.PC, Argument.Pop);

            gen.MarkLabel(crash);
            gen.Emit(OpCodes.Set, Argument.PC, crash);

            gen.Finish();

            PrintDump(((MemoryStream)gen.OutputStream).ToArray(), 8);
            Console.ReadLine();
        }

        static void PrintDump(byte[] data, int wordByRow)
        {
            int rowWord = 0;
            while (rowWord * 2 < data.Length)
            {
                Console.Write("{0:X4}: ", rowWord);
                
                for (int i = 0; i < wordByRow; i++)
                {
                    int index = (rowWord + i) * 2;
                    byte high = index < data.Length ? data[index] : (byte)0;
                    byte low = index + 1 < data.Length ? data[index + 1] : (byte)0;

                    Console.Write("{0:X2}{1:X2}", high, low);
                    if (i != wordByRow - 1)
                        Console.Write(" ");
                }

                Console.WriteLine();
                rowWord += wordByRow;
            }
        }
    }
}
