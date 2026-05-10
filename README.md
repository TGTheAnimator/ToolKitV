<div align="center">

# 🎨 TGToolKit

**A FiveM texture optimization tool — fork of the original ToolKitV by Umbrella.re**

*Reduce `.ytd` file sizes, fix mip chains, and improve streaming performance for your FiveM server.*

</div>

---

## ✨ Features

- **Texture Optimizer** — Batch process all `.ytd` files in a folder (recursive)
  - 🗜️ **Format Optimization** — Re-encodes to the best block-compressed format per texture type:
    - `BC7` for RGBA / DXT5 textures (best quality, same size as DXT5)
    - `BC1` for opaque RGB textures (half the size of DXT5)
    - `BC4` for grayscale / single-channel textures
    - `BC5` for normal maps (dual-channel XY)
  - 📐 **Downsize** — Halves texture resolution (÷2) to reduce VRAM usage
  - 🔬 **Analyze** — Scan your resource folder and see total file count, virtual/physical sizes, and how many YTDs exceed the FiveM 16 MB streaming budget
  - 💾 **Backup** — Automatically copies originals before modifying them
  - 🎯 **Oversized-only mode** — Process only YTDs that exceed 16 MB virtual size (FiveM streaming budget threshold)
  - 🔧 **Script RT protection** — Automatically decompresses `script_rt_*` render-target textures (GTA V crashes if these are block-compressed)
  - 📋 **Log file** — Full per-file log written to `log.txt` next to the executable

---

## 🚀 Getting Started

### Requirements

| Requirement | Version |
|-------------|---------|
| Windows | 10 / 11 |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) | 8.0+ |

> **Note:** If you're building from source, you need the **.NET 8 SDK**, not just the runtime.

### Running a Pre-Built Release

1. Download the latest release from the [Releases](../../releases) page
2. Extract the zip — keep all files together
3. Double-click **`TGToolKit.exe`** — no installer or launcher needed

### Building from Source

```powershell
# Clone the repo
git clone https://github.com/TGTheAnimator/ToolKitV.git
cd ToolKitV

# Build
dotnet build Application/ToolKitV.csproj --configuration Release

# Run
.\Application\bin\Release\net8.0-windows\TGToolKit.exe
```

---

## 🎛️ How to Use

1. **Select Textures Folder** — Point it at your FiveM resource folder (e.g. `resources/[cars]/my-vehicle/stream`)
2. *(Optional)* **Select Backup Folder** — Where originals will be saved before modification
3. **Tune the settings:**
   - **Only oversized YTDs** — Recommended: only re-process files >16 MB virtual size
   - **Optimize Size** — Threshold: textures with Width+Height ≥ this value get processed. Default: `4096` (2048×2048). Lower for more aggressive optimization
   - **Downsize (÷2)** — Halves resolution. Good for props/vehicles, avoid for UI textures
   - **Format Optimization** — Switches to best-fit BC format. Minimal visual loss, significant size reduction
4. Click **Analyze** to see current stats, then **Optimize** to process

---

## 💡 FiveM Optimization Tips

- **Virtual size > Physical size** — FiveM streams resources based on virtual (GPU) memory, not disk size. The 16 MB limit refers to virtual size. TGToolKit checks the right one.
- **2048×2048 is the sweet spot** — Most vehicle/prop textures don't need to exceed this. Set Optimize Size to `4096` and enable Downsize for anything above it.
- **BC7 is free quality** — When Format Optimization is on, RGBA textures are upgraded from DXT5 to BC7 at no extra size cost but with better quality preservation.
- **Normal maps should use BC5** — XY normal maps encoded as BC5 are smaller and more accurate than DXT5. TGToolKit handles this automatically.
- **Check the log** — A `log.txt` file is created next to the exe during every optimize/analyze run. Use it to see exactly what was changed and how much was saved.

---

## 🗺️ Roadmap

- [ ] Vehicle tools (optimize/join/batch-convert)
- [ ] Clothes tools (replace → add-on conversion, meta editor/creator)
- [ ] Drag-and-drop folder support
- [ ] Per-file preview with before/after size comparison

---

## 📦 Dependencies

| Library | Author | Purpose |
|---------|--------|---------|
| [CodeWalker.Core](https://github.com/dexyfex/CodeWalker) | dexyfex | Reading and writing GTA V `.ytd` / `.rpf` files |
| [texconv.exe](https://github.com/microsoft/DirectXTex) | Microsoft | DDS texture format conversion (BC1/BC3/BC4/BC5/BC7) |

---

## 📝 License

This project is licensed under the **GPL-3.0 License** — see [LICENSE](LICENSE) for details.

Original work by [Umbrella.re](https://umbrella.re) (2021–2022). Fork maintained by **TGTheAnimator** (2026).
