using System;
using System.Reflection;
using CodeWalker.GameFiles;

class Program
{
    static void Main()
    {
        Console.WriteLine("CodeWalker.Core Geometry structure:");
        var geom = typeof(Geometry);
        foreach(var prop in geom.GetProperties()) {
            Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }

        Console.WriteLine("\nCodeWalker.Core VertexBuffer structure:");
        var vb = typeof(VertexBuffer);
        foreach(var prop in vb.GetProperties()) {
            Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }

        Console.WriteLine("\nCodeWalker.Core VertexDeclaration structure:");
        var vd = typeof(VertexDeclaration);
        foreach(var prop in vd.GetProperties()) {
            Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }
    }
}
