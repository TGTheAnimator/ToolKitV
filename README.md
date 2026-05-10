<div align="center">

# 🎨 TGToolKit

**A FiveM texture optimization and vehicle tool — fork of the original ToolKitV by Umbrella.re**

*Optimize textures, fix crashes, and consolidate vehicle meta files for a premium, high-performance FiveM experience.*

</div>

---

## ✨ Features

### 🖼️ Texture Optimizer
- **Batch process** all `.ytd` files in a folder recursively.
- 🗜️ **Format Optimization** — Re-encodes to the best block-compressed format:
  - `BC7` for RGBA / DXT5 (best quality, same size as DXT5).
  - `BC1` for opaque RGB (half the size of DXT5).
  - `BC4` for grayscale / single-channel.
  - `BC5` for normal maps.
- 📐 **Downsize** — Halves texture resolution (÷2) to reduce VRAM usage.
- 🎯 **Oversized-only mode** — Process only YTDs that exceed the FiveM 16 MB streaming budget.
- 🛡️ **Mip-chain Validation** — Automatically fixes missing or incorrect mipmap chains to prevent shimmering.
- 📊 **Accurate Metrics** — Real-time tracking of Virtual (GPU) size vs. Physical (Disk) size.

### 🚗 Vehicle Tools (NEW)
- 🔗 **Meta Consolidation** — Merges hundreds of individual `.meta` files into master files.
  - Fixes the `productId != ProductID::INVALID` fatal crash caused by too many mini-DLCs.
  - Merges: `vehicles.meta`, `handling.meta`, `carcols.meta`, `carvariations.meta`, `vehiclelayouts.meta`.
  - 🛠️ **Modkit ID Conflict Resolution** — Automatically detects and remaps conflicting modkit IDs in `carcols.meta` and updates `carvariations.meta` references to match.
  - 📝 **FXManifest Generator** — Automatically generates the required `fxmanifest.lua` snippet for your merged resource.

### ⚠️ Crash Fixes
- 🔬 **Dedicated Script RT Fix** — One-click scan to decompress `script_rt_*` textures (like dials/radios).
  - Specifically fixes fatal crashes like the `gsts121.ytd` / `script_rt_dials_itali` issue.
  - Forces uncompressed `B8G8R8A8` format as required by the GTA V engine.

### 💅 Modern UI
- **Glassmorphism Design** — A sleek, premium dark-themed interface with red accents.
- **Responsive & Fast** — Built on .NET 8 for maximum performance.
- **Single-Instance** — No installers, no updaters, just run and optimize.

---

## 🚀 Getting Started

### Requirements

| Requirement | Version |
|-------------|---------|
| Windows | 10 / 11 |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) | 8.0+ |

> **Note:** Releases are **self-contained**. You do not need to install .NET if you download the pre-built zip.
> **Note:** If you're building from source, you need the **.NET 8 SDK**, not just the runtime.


### Installation

1. Download the latest **`TGToolKit-v1.1.0.zip`** from the [Releases](../../releases) page.
2. Extract the zip to any folder.
3. Run **`TGToolKit.exe`**.

---

## 🎛️ How to Use

### Textures
1. **Select Folder** — Point it at your resource stream folder.
2. **Analyze** to see VRAM usage, then **Optimize** to shrink.
3. Use **Fix Script RT Crashes** if you experience random crashes while entering vehicles.

### Vehicles
1. **Select Pack Folder** — Point it at the root of a large vehicle pack (e.g. `tebex-car-pack/`).
2. **Scan** to see how many meta files were found.
3. **Merge** to consolidate everything into a single `data/` folder.
4. Copy the `fxmanifest_snippet.lua` content into your resource manifest.

---

## 💡 FiveM Optimization Tips

- **Virtual size > Physical size** — FiveM limits are based on **Virtual (VRAM)** size. A file might be 5MB on disk but 64MB in VRAM. TGToolKit tracks both to keep you under the 16MB threshold.
- **BC7 is a free upgrade** — It provides better quality than DXT5 for the exact same file size.
- **Don't merge everything at once** — While merging 500 cars into one resource fixes crashes, it makes the initial download huge for players. Group them logically (e.g. 50-100 cars per pack).

---

## 🗺️ Roadmap

- [x] Vehicle tools (Meta consolidation)
- [x] Dedicated Script RT crash fixer
- [ ] Clothes tools (Replace → Add-on conversion)
- [ ] Drag-and-drop support
- [ ] Per-file XML export/import for advanced users

---

## 📦 Dependencies

| Library | Author | Purpose |
|---------|--------|---------|
| [CodeWalker.Core](https://github.com/dexyfex/CodeWalker) | dexyfex | Reading and writing GTA V `.ytd` / `.rpf` files |
| [texconv.exe](https://github.com/microsoft/DirectXTex) | Microsoft | DDS texture format conversion |

---

## 📝 License

This project is licensed under the **GPL-3.0 License**.

Original work by [Umbrella.re](https://umbrella.re). Fork maintained by **TGTheAnimator** (2026).
