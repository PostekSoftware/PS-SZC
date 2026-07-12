# PS-SZC

School payments and billing management application.

**Polish:** [README.pl.md](README.pl.md)

## Supported platforms

Pre-built releases are provided for:

- **macOS** — Apple Silicon (arm64)
- **Windows** — x64

Other platforms and CPU architectures are not supported by the official release packages.

## Install from releases

Download the latest release from:

**https://github.com/PostekSoftware/PS-SZC/releases/latest**

### macOS (Apple Silicon)

1. Download `.dmg` from the latest release.
2. Open the disk image and drag **PS-SZC.app** into **Applications**.
3. Because the application is not notarised or signed with an Apple Developer certificate, macOS may block it on first launch. Remove the quarantine attribute by running:

   ```bash
   xattr -cr /Applications/PS-SZC.app
   ```

4. Open **PS-SZC** from Applications or Launchpad.

If macOS still warns about an unidentified developer, open **System Settings → Privacy & Security** and choose **Open Anyway** for PS-SZC.

### Windows (x64)

1. Download `.msi` from the latest release.
2. Run the installer and follow the prompts.
3. Launch **PS-SZC** from the Start menu or the desktop shortcut created during installation.

The installer requires Windows x64 and the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) if it is not already installed.

## Build from source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **macOS:** Xcode Command Line Tools (`xcode-select --install`) — required to compile the print helper
- **Windows (installer only):** [WiX Toolset SDK 5](https://wixtoolset.org/) — used when building the MSI

Clone the repository:

```bash
git clone https://github.com/PostekSoftware/PS-SZC.git
cd PS-SZC
```

### Build the application (macOS)

On Apple Silicon Mac:

```bash
dotnet build PS-SZC/PS-SZC.csproj -c Release
```

This produces:

- `PS-SZC/bin/Release/net10.0/PS-SZC.app` — macOS application bundle
- `PS-SZC/bin/Release/net10.0/PS-SZC.dmg` — disk image for distribution

Run locally without installing:

```bash
open PS-SZC/bin/Release/net10.0/PS-SZC.app
```

### Build the application (Windows)

```bash
dotnet publish PS-SZC/PS-SZC.csproj -c Release -r win-x64
```

Published files are written to:

`PS-SZC/bin/Release/net10.0/win-x64/publish/`

Run:

```powershell
.\PS-SZC\bin\Release\net10.0\win-x64\publish\PS-SZC.exe
```

### Build the Windows installer (MSI)

The MSI is built separately and only on Windows:

```powershell
dotnet build PS-SZC.Installer/PS-SZC.Installer.wixproj -c Release
```

Output:

`PS-SZC.Installer/bin/Release/PS-SZC.msi`

The installer project publishes the Windows x64 application automatically before packaging.

### Build the solution (development)

```bash
dotnet build PS-SZC.sln -c Debug
```

The Windows installer project is skipped on non-Windows systems.

## License

PS-SZC is released under the **MIT License**. See [LICENSE](LICENSE) for the full text.

Copyright (c) 2026 PostekSoftware

## Third-party libraries

PS-SZC uses the following open-source components. Their licenses apply in addition to the project license above.

| Library | Version | Used for | License |
| --- | --- | --- | --- |
| [Hexa.NET.ImGui](https://www.nuget.org/packages/Hexa.NET.ImGui) | 2.2.9 | Immediate-mode UI | MIT |
| [Hexa.NET.ImGui.Backends.SDL3](https://www.nuget.org/packages/Hexa.NET.ImGui.Backends.SDL3) | 1.0.18 | ImGui rendering via SDL3 | MIT |
| [Hexa.NET.SDL3](https://www.nuget.org/packages/Hexa.NET.SDL3) | 1.2.17 | Windowing, input, graphics | MIT |
| [SDL3](https://github.com/libsdl-org/SDL) (native) | (via Hexa.NET.SDL3) | Cross-platform multimedia | Zlib |
| [SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp) | 3.1.11 | Image loading and processing | [Six Labors License](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE) |
| [Microsoft.EntityFrameworkCore.Sqlite](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite) | 9.0.0 | Project database access | MIT |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | 9.0.0 | SQLite ADO.NET provider | MIT |
| [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) | 3.0.3 | Native SQLite engine | Apache-2.0 |
| [SQLite](https://sqlite.org/) (native) | (via SQLitePCLRaw) | Embedded database | Public domain |
| [PdfSharpCore](https://www.nuget.org/packages/PdfSharpCore) | 1.3.67 | PDF report export | MIT |
| [WixToolset SDK](https://www.nuget.org/packages/WixToolset.Sdk) | 5.0.2 | Windows MSI packaging (build only) | MIT |

Additional transitive .NET libraries (for example Microsoft.Extensions.* and HexaGen.Runtime) are distributed under the MIT License unless noted otherwise in their respective packages.
