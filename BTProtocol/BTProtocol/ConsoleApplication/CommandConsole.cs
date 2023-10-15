using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BTProtocol.ConsoleApplication
{
    internal class CommandConsole
    {

    public static void Main(string input, string output, int x = 0, int y = 0)
        {
            Console.WriteLine(input);
            Console.WriteLine(output);
            Console.WriteLine(x);
            Console.WriteLine(y);
        }
    }
}
