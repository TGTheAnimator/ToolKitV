using System;
using CodeWalker.GameFiles;

class Program
{
    static void Main()
    {
        foreach(var name in Enum.GetNames(typeof(VertexSemantics)))
        {
             Console.WriteLine($"{(int)Enum.Parse(typeof(VertexSemantics), name)} : {name}");
        }
    }
}
