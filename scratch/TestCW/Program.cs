using System;
using CodeWalker.GameFiles;

class Program
{
    static void Main()
    {
        Console.WriteLine("--- AwcFile.Load methods ---");
        foreach(var m in typeof(AwcFile).GetMethods().Where(m => m.Name == "Load"))
        {
            Console.WriteLine($"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }
    }
}
