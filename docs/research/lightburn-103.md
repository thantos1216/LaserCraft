# Host LightBurn reference - verified 1.3.01

The user screenshot and live title/UI Automation confirm LightBurn 1.3.01 at C:\Program Files\LightBurn\LightBurn.exe. The earlier 1.03 label was approximate. Read-only inspection found File, Edit, Tools, Arrange, Laser Tools, Window, Language and Help menus; compact main/arrange/numeric/text toolbars; left creation/modifier tools; bottom C00-C29 plus T1/T2 palette; Cuts/Layers above Laser on the right.

Observed layer columns: #, Layer, Mode, Spd/Pwr, Output, Show and Air. Layer quick settings expose interval, passes, speed and maximum power. The host is configured to display speed in mm/s, while the supplied VM screenshot shows mm/min; LaserCraft currently explicitly uses mm/min.

Observed Laser controls: Pause, Stop, Start, two framing actions, Save/Run GCode, Start From (Current Position, User Origin, Absolute Coords), Cut Selected Graphics, Job Origin, Show Last Position, Optimize Cut Path, Optimization Settings, Home, Go to Origin, Devices and port/device selection. These are observed controls, not a claim of protocol or behavior equivalence.

Camera Control, Variable Text, Console and Library panels were exposed in UI Automation. Source assets, proprietary implementations, machine settings and installation files were not copied or modified. Detailed controlled behavior comparisons remain pending.
