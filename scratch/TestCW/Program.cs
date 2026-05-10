using System;
using CodeWalker.GameFiles;

class Program
{
    static void Main()
    {
        Console.WriteLine("--- RpfBinaryFileEntry Constructors ---");
        foreach(var c in typeof(RpfBinaryFileEntry).GetConstructors())
        {
            Console.WriteLine($"Constructor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }
    }
}
