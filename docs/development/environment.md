# Environment verification

2026-11-07, Windows x64 host. Project created and Git initialized at D:\Development\LaserCraft before solution generation.

| Item | Result |
| --- | --- |
| .NET | SDK 10.0.403 available; SDK 6.0.428 also present; global.json pins 10.0.403 |
| Desktop | Avalonia 12.1.5 restored from NuGet; original desktop launches |
| Rust | rustc / Cargo 1.99.1; stable-x86_64-pc-windows-msvc active |
| Visual Studio | vswhere latest reports BuildTools 18.11.12328.437 under Visual Studio/18; 2022 also present; Professional edition not independently verified |
| Windows SDK | 10.0.26100.0 library directory present |
| Python | 3.14.8; project-local venv works, pypdf 7.0.0 installed |
| Git | 2.55.0.windows.5 |
| Node | Host 26.13.0; bundled 24.22.1 used for REA source build |
| GPUs | NVIDIA GeForce RTX 5080 Laptop GPU and Intel Graphics detected; graphics API feature levels not benchmarked |
| Hyper-V | Get-VM denied by host authorization policy; existing VM console accessible through UI; Windows 11stream Running, LightBurn title shows 2.2.01 (Trial) |

Read-only repository discovery found BDK.Finance at D:\Development\C#\BDK.Finance. Its WPF theme engine was evaluated without edits. REA clone now resides at D:\Development\REA\rea; see REA findings for successful build and provider tests.

No new major system dependency, firewall change, VM network change, or machine-wide runtime update was made. NuGet, Python venv and REA checkout dependencies are local development dependencies.

Rust/MSVC linker verified by compiling and executing a small original Rust program in ignored tmp/rust-smoke. GPU API feature levels and acceleration performance remain unverified.
