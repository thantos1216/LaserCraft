# LaserCraft USB connection test

Extract the complete portable ZIP into a local folder and start LaserCraft.Desktop.exe. Keep its DLLs, native libraries and Help.md together. This self-contained Windows x64 package includes its .NET runtime. A Debug executable copied alone does not contain its dependencies.

Run the app on the Windows computer with the laser physically attached by USB. Storing the download on a server does not forward a desktop USB COM port to that server. This build has no remote USB bridge. Install the controller's appropriate USB driver if Windows does not expose a COM port; LaserCraft does not install drivers.

1. Close other laser software that could hold the port.
2. Open Devices. Use Attzr 35 W USB draft for the user-specified 430 × 430 mm bed. Select a detected COM port. Baud 115200, S1000 and bottom-left origin are unverified starting values; review them, then save the profile. Saving does not open the port.
3. Select that profile in Laser, refresh COM ports and choose the connected port. Connect queries identity and status only. Opening USB serial can reset some controllers depending on their hardware/driver.
4. Run Connection check to read $$, $G and status. Match labels do not establish homing, work zero, hardware compatibility or permission to cut. No settings are changed automatically.
5. Use Save connection log after success or failure. It saves a bounded snapshot of the latest connection attempt, with selected endpoint/profile values, raw replies, errors and close events. TX means attempted write, not accepted or executed. Control characters are escaped. Review the file before sharing it; nothing is uploaded automatically. New Connect replaces the in-memory journal. No log survives app exit unless saved.

If there are no ports, check power/cable/Windows driver and refresh. If opening fails, verify the selected port and close other port users. If identity times out, check baud/reset/startup delay and save the log; no automatic baud sweep or arbitrary port probing is performed. Include BUILD-INFO.json with a bug report to identify this package. Devices/settings are per Windows user under LocalAppData/LaserCraft, not carried in this ZIP.

This build supports read-only physical connection diagnostics. Physical jogging, homing, firing, air switching and first cuts remain unavailable. Simulator controls and offline previews do not demonstrate real laser output.
