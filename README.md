<div align="center">

# 🎨 TGToolKit

**A comprehensive FiveM administrative and development utility kit.**

*Optimize textures, fix crashes, and consolidate vehicle meta files for a premium, high-performance FiveM experience.*

</div>

---

## ✨ Features

### 🧊 3D Model Viewer (NEW)
- **High-Performance Rendering** — Native DirectX 11 hardware acceleration for smooth asset inspection.
- **YDR Support** — Real-time 3D preview of FiveM drawable models.
- **Dynamic Texturing** — Automatically maps `.ytd` texture dictionaries to models with full support for BC1-BC7 compression.
- **Orbital Camera** — Intuitive mouse-based rotation and zoom for detailed inspection.
- **Zero-Copy Pipeline** — Direct GPU memory sharing between DX11 and WPF for maximum efficiency.

### 🔊 Audio Previewer (NEW)
- **AWC Native Playback** — Instantly preview audio streams inside GTA V `.awc` containers.
- **Quality Assurance** — Verify sound quality in real-time after optimization.
- **Stream Extraction** — Lists all internal streams with length and metadata details.
- **Built-in Player** — Seeker bar, playback controls, and volume management powered by NAudio.

### 🖼️ Texture Optimizer
- **Batch process** all `.ytd` files in a folder recursively.
- 🗜️ **Format Optimization** — Re-encodes to the best block-compressed format:
  - `BC7` for RGBA / DXT5 (best quality, same size as DXT5).
  - `BC1` for opaque RGB (half the size of DXT5).
  - `BC4` for grayscale / single-channel.
  - `BC5` for normal maps.
- 📐 **Downsize** — Halves texture resolution (÷2) to reduce VRAM usage.
- 📉 **Auto-Downscale 4K** — Automatically shrinks massive 4096px+ textures perfectly to 2048px to prevent engine memory crashes.
- 🎯 **Oversized-only mode** — Process only YTDs that exceed the FiveM 16 MB streaming budget.
- 📊 **Accurate Metrics** — Real-time tracking of Virtual (GPU) size vs. Physical (Disk) size.
- 🔄 **Built-in Updater** — One-click updates pulled directly from the GitHub repository.

### 🚗 Vehicle Tools
- 🔗 **Meta Consolidation** — Merges hundreds of individual `.meta` files into master files.
  - Fixes the `productId != ProductID::INVALID` fatal crash caused by too many mini-DLCs.
  - 🛠️ **Modkit & Siren ID Conflict Resolution** — Automatically detects and remaps conflicting IDs.
- 📝 **FXManifest Generator** — Automatically generates the required `fxmanifest.lua` snippet.

### 👕 Clothing Tools
- 👗 **Add-on Generator** — Drop a folder of loose FiveM clothing models and textures to generate a compiled `.ymt` package.
- 🎩 **Smart Component Splitting** — Automatically separates base clothing from props.

---

## 🚀 Getting Started

### Requirements

| Requirement | Version |
|-------------|---------|
| Windows | 10 / 11 |
| DirectX | Runtime 11+ |

> **Note:** Releases are **self-contained**. You do not need to install the .NET Runtime separately.

### Installation

1. Download the latest **`TGToolKit-v3.0.0.zip`** from the [Releases](../../releases) page.
2. Extract the zip to any folder.
3. Run **`TGToolKit.exe`** and enjoy!

---

## 📦 Build from Source

If you want to build the self-contained package yourself:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfContained=true
```

---

## 📦 Dependencies

| Library | Author | Purpose |
|---------|--------|---------|
| [CodeWalker.Core](https://github.com/dexyfex/CodeWalker) | dexyfex | Reading and writing GTA V assets |
| [SharpDX](http://sharpdx.org/) | Alexandre Mutel | DirectX 11 Hardware Rendering |
| [NAudio](https://github.com/naudio/NAudio) | Mark Heath | Audio Playback & DSP |
| [texconv.exe](https://github.com/microsoft/DirectXTex) | Microsoft | DDS texture format conversion |

---

## 📝 License

This project is licensed under the **GPL-3.0 License**.

Original work by [Umbrella.re](https://umbrella.re). Fork maintained by **TGTheAnimator** (2026).
