using CodeWalker.GameFiles;
using System;
using System.IO;
using System.Linq;

var yft = Directory.GetFiles(
    @"C:\Users\rogue\OneDrive\Documents",
    "*.yft",
    SearchOption.AllDirectories
).FirstOrDefault();

if (yft == null) { Console.WriteLine("No yft found"); return; }
Console.WriteLine($"YFT: {yft}");

var bytes = File.ReadAllBytes(yft);
var f = new YftFile();
f.Load(bytes);

var d = f.Fragment?.Drawable;
if (d == null) { Console.WriteLine("No drawable"); return; }

var shaders = d.ShaderGroup?.Shaders?.data_items;
Console.WriteLine($"Shaders count: {shaders?.Length ?? 0}");
for (int si = 0; si < Math.Min(5, shaders?.Length ?? 0); si++)
{
    var sh = shaders![si];
    var plist = sh.ParametersList;
    Console.WriteLine($"  Shader[{si}] Name={sh.Name} FileName={sh.FileName} TextureParamsCount={sh.TextureParametersCount} TotalParams={plist?.Parameters?.Length ?? 0}");
    if (plist?.Parameters != null)
    {
        for (int pi = 0; pi < plist.Parameters.Length; pi++)
        {
            var p = plist.Parameters[pi];
            if (p.DataType == 0) // texture param
            {
                string texName = (p.Data is Texture t) ? (t.Name ?? "null-name") : $"not-texture({p.Data?.GetType().Name})";
                Console.WriteLine($"    TextureParam[{pi}] tex={texName}");
            }
        }
    }
}

var models = d.DrawableModels?.High;
Console.WriteLine($"\nModels (High): {models?.Length ?? 0}");
for (int mi = 0; mi < Math.Min(2, models?.Length ?? 0); mi++)
{
    var m = models![mi];
    Console.WriteLine($"  Model[{mi}] ShaderMapping=[{string.Join(",", m.ShaderMapping ?? Array.Empty<ushort>())}]");
    Console.WriteLine($"  Model[{mi}] GeomCount={m.Geometries?.Length ?? 0}");
}
