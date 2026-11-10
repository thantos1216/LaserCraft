# LaserCraft USB connection test

Extract the complete portable ZIP into a local folder and start LaserCraft.Desktop.exe. Keep its DLLs, native libraries and Help.md together. This self-contained Windows x64 package includes its .NET runtime. A Debug executable copied alone does not contain its dependencies.

Run the app on the Windows computer with the laser physically attached by USB. Storing the download on a server does not forward a desktop USB COM port to that server. This build has no remote USB bridge. Install the controller's appropriate USB driver if Windows does not expose a COM port; LaserCraft does not install drivers.

1. Close other laser software that could hold the port.
2. Open Devices. Use Attzr 35 W USB draft for the user-specified 430 × 430 mm bed. Select a detected COM port. Baud 115200, S1000 and bottom-left origin are unverified starting values; review them, then save the profile. Saving does not open the port.
3. Select that profile in Laser, refresh COM ports and choose the connected port. Connect queries identity and status only. Opening USB serial can reset some controllers depending on their hardware/driver.
4. Run Connection check to read $$, $G and status. Match labels do not establish homing, work zero, hardware compatibility or permission to cut. No settings are changed automatically.
5. Use Save connection log after success or failure. It saves a bounded snapshot of the latest connection attempt, with selected endpoint/profile values, raw replies, errors and close events. TX means attempted write, not accepted or executed. Control characters are escaped. Review the file before sharing it; nothing is uploaded automatically. New Connect replaces the in-memory journal. No log survives app exit unless saved.

If there are no ports, check power/cable/Windows driver and refresh. If opening fails, verify the selected port and close other port users. If identity times out, check baud/reset/startup delay and save the log; no automatic baud sweep or arbitrary port probing is performed. Include BUILD-INFO.json with a bug report to identify this package. Devices/settings are per Windows user under LocalAppData/LaserCraft, not carried in this ZIP.

Connect/diagnostic buttons are read-only. Console input and Submit/Enter permit queries by default; setting writes and custom G-code require Enable setting writes / custom G-code for this connection. Such commands can move/fire or change persistent firmware settings. Verify changes with $$ afterward; profiles are not synchronized automatically. Timeout/disconnect is not a physical stop, and offline Start/Pause/Stop do not control hardware. Version 0.22 adds separately opted-in guided GRBL 1.1 X/Y, jog cancel, laser-off and pulse tests. Homing, job streaming and firmware-image flashing remain unavailable. Simulator controls and offline previews do not demonstrate real output.

## Supervised control test (0.22)

After identity/settings/status checks, open Move. Keep laser protection and the physical stop/power switch ready. Enable physical control; start with one 1 mm X jog at 600 mm/min, observe direction/travel, and Read status until Idle. Repeat Y with clearance from limits. The profile bed does not establish homing/position bounds. Each jog sends M5 S0 first. Cancel jog sends 0x85; Laser off sends queued M5 S0. Neither is an emergency stop; disconnect is not a stop.

For the optional pulse, confirm suitable test material and enable the separate firing checkbox. Start at 1% / 100 ms. Firmware $32 must be 1, $31 must be 0 and $30 must match the profile maximum; values are read and never changed automatically. Controller-side dwell/off are queued together, but partial USB transmission/controller failure can prevent off. Nominal duration is not an optical measurement. G1/G94/F60 remain active afterward. Check output with the physical stop ready, then save the connection log. Physical success remains unverified until your test; full cut-job streaming is unavailable.

## First reviewed line test (0.24)

Your submitted screenshots show 0.14, which predates guided control. Use the complete 0.24 package and confirm the toolbar version. The supplied settings report shows S1000, laser mode 1, minimum S0, 430 mm X/Y travel and soft limits off. These are user-reported observations; do not assume homing or a safe work envelope.

After small unpowered jogs establish directions, position over scrap with clearance. In Move enable physical control and firing, then Review line cut test. Choose signed X/Y length, feed and power, review the exact summary and tick the positioning/protection/physical-stop confirmation before Run. The default 5 mm / 600 mm/min / 10% is a mark example, not a through-cut recipe for your 5 mm plywood. Air is unchanged. The test makes one pass from the current position without returning.

Preflight reads current settings/modes and requires Idle, S maximum match, $31=0/$32=1 and G21/G90/G94. It queues motion and off together and requires final Idle. It ends off in G21/G90/G94/G0 with the test feed retained. Keep the physical stop ready; partial transmission/controller failure can prevent off, and app disconnect is not a stop. Save the connection log and report build version, chosen settings, observed travel/mark and any error. Full document streaming remains unavailable.
