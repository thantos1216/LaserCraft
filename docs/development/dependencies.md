# Dependencies

Direct runtime NuGet dependencies: Avalonia.Desktop 12.1.5 and Avalonia.Themes.Fluent 12.1.5 (MIT). Avalonia resolves SkiaSharp (MIT), HarfBuzzSharp (MIT), platform native assets and other transitives. Preserve upstream notices when packaging; release license review remains pending.

Tests use xunit 2.9.3 (Apache-2.0), xunit.runner.visualstudio 3.1.4 (Apache-2.0), Microsoft.NET.Test.Sdk 17.14.1 (MIT), and coverlet.collector 6.0.4 (MIT); consult the csproj for exact resolved direct versions.

Development-only: pypdf 7.0.0 (BSD-3-Clause), installed inside .venv. REA 6.8.0 (MIT), separately owned checkout and dependencies. REA's Ghidra provider uses the existing Ghidra 12.1.4 installation (Apache-2.0 and bundled third-party notices). Neither the manual nor LightBurn binaries are distributed.

No custom icons, proprietary images or copied LightBurn UI assets are used.

Milestone 0.3 adds System.IO.Ports 10.0.7 (MIT) to the isolated Devices project. Raster decoding declares SkiaSharp 3.119.4 directly (MIT), the same version already present transitively; no new image runtime was installed. Original LaserCraft source is MIT-licensed; third-party license notices still apply.
