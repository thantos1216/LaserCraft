# Coordinate reference check

2026-11-11. Analyst question: must GRBL machines use negative coordinates, and how does current-position placement compare?

Observed user log: positive MPos after successful Home. Source-derived bug: LaserCraft 0.25 checked only [-travel,0]. Original repair: infer each axis range from signed homed machine position or require an explicit profile choice when ambiguous. This inference is LaserCraft design, not a claim about LightBurn internals or measured machine limits.

REA provider metadata: Docker CLI is working, but native UI observation/capture is advertised unavailable on this host (unsupported_host, macOS required). No LightBurn window was currently available in Windows inventory. Existing bounded 2.2 inventory remains historical evidence, not current behavioral verification.

Publisher references reviewed: [CNC-based lasers](https://docs.lightburnsoftware.com/legacy/Troubleshooting/CNCBasedLasers) distinguishes CNC/router coordinate conventions from common front-left laser setups; [Coordinates and Job Origin](https://docs.lightburnsoftware.com/legacy/CoordinatesOrigin) documents separate absolute/current/user-origin placement and selectable job anchor. LaserCraft still exposes only current-position/minimum-machine-X/Y anchoring here; nine-point origin and other placement modes remain incomplete. These are legacy official documentation pages, not live 2.2 UI observations. No settings/offset commands suggested by those pages were executed.
