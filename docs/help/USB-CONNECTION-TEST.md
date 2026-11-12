# LaserCraft 0.31 hardware qualification

Extract the complete portable ZIP into a fresh folder on the Windows computer with the USB laser. Confirm toolbar 0.31 and close other port users.

1. Select the physical device and detected COM port in the top-right connection box. The profile bed adjusts automatically and the selection/defaults persist.
2. Clear travel, then Connect. GRBL homing runs after a one-second pause if $22=1. Use Home again whenever a new reference is required.
3. Use a small jog, Read position and (when ready) a bounded pulse. mm/in switches main lengths and feed displays; commands remain millimetres. Advanced dialogs retain explicit metric labels.
4. Open top-toolbar Preview for a small vector/image sample; check playback/seek and Back to Design. All enabled artwork is the default scope; choose Selected objects only to restrict output. Check layer speed/power/passes and processed raster. Choose Start From and a drawing anchor. Frame rectangle or circle with laser OFF, then Run. Circle framing is intended for round designs and does not enclose rectangular corners.
5. Verify laser-off return to the pre-job head X/Y and final Idle. Repeat Run on the same unchanged artwork/material to verify path alignment. Confirm the Pass column and choose Whole layer each pass or All passes per object; edits apply immediately. Abort must cancel output and reopen without homing/resuming; Home if firmware reports Alarm. A failed USB reopen is an error, not a completed cut.
6. Disconnect and close the window. Wait for it to exit, then test that another port user can open the device. Do not run both applications on the same port.

The 18:48 and 18:55 logs fail during Windows port open/configuration before any TX. They do not identify a leaked app handle or prove a repaired driver/device. Shutdown now awaits cancellation and disposal, including pending connection startup; it allows a one-second serial-release interval. No drivers, firmware offsets or USB settings are changed automatically. Keep a physical stop available. Save the connection log and BUILD-INFO.json with the next hardware result.
