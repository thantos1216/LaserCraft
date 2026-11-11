# LaserCraft 0.27 physical qualification guide

New placement controls: choose Start From and Job Origin before reviewing output. Absolute Coordinates preserves drawing bed location. Current Position places a chosen drawing corner/center at the head. User Origin uses Move > Set User Origin here and survives head movement, but Home/reconnect clears it. Move also offers laser-off exact bed X/Y positioning. Coordinates are controller-axis distances from the resolved machine minimum. Use Frame laser off before burning; powered review offers automatic frame-before-cut. Selected-only uses selected output bounds. These new modes require hardware qualification. Offline Preview/Save G-code retain their earlier semantics. Older instructions below describe the default Current Position / BottomLeft anchor workflow.

Extract the complete portable ZIP locally on the Windows computer with the USB laser attached. Confirm toolbar 0.27. Server storage does not forward USB. Close other port users, select the saved GRBL profile and detected COM port. Devices/settings remain per user under LocalAppData/LaserCraft. No drivers are installed automatically.

## Connect and home

The current profile bed is automatically applied to the current document without scaling artwork. Home on Connect is checked by default for physical GRBL; the button reads Connect + Home. Clear the travel area before using it. Uncheck it for query-only connection. Homing requires reported $22=1 and may take 120 seconds. A dedicated Home button repeats the operation. This establishes firmware reference, not work zero; negative machine coordinates can be normal. No G54/G92 changes or automatic unlock are sent.

The prior enable-control/firing checkboxes are removed. Move buttons are available on a physical GRBL connection. Start with small jogs and verify actual axis direction. Larger jogs are accepted up to each profile axis length; this does not guarantee clearance from the current position. Pulse and reviewed line tests remain available. Your earlier report/log shows S1000/$31=0/$32=1 and 430 mm X/Y travel, plus soft limits off. Those observations are not a current safety envelope.

## First document cut / image job

1. Prepare a small vector or processed image on scrap. Configure its layer speed, power, passes, image interval and air. Preview and inspect the bounds/output.
2. Position the head at the minimum machine X/Y corner of the intended footprint; the job extends +X/+Y. Profile origin transforms the drawing first. The job uses current MPos with G53, not a silently reset work offset. Include overscan and ensure full clearance.
3. Choose Review & Run / Run document. Check the footprint, layer scope and ideal time. Confirm placement/material/protection and physical stop readiness before Run. No guaranteed material settings are supplied for 5 mm plywood.
4. Progress shows acknowledgements, then the app requires final Idle. Inspect the actual result. Pause / Resume controls realtime hold/resume; Abort cancels dispatch and requests reset. Abort/error requires reconnect and has no automatic resume.

Preflight requires GRBL 1.1 Idle/MPos, $13=0, $31=0/$32=1 and $30/$130/$131 matching the profile. It does not change firmware settings. One-line-at-a-time streaming prioritizes bounded response ownership over dense raster speed. New homing/document workflows need your hardware test; a successful line test alone does not qualify them.

Disconnect during a job attempts abort/reset; closing is blocked while a physical job runs. A failed connection/controller can prevent reset/off, so keep the physical stop/power switch ready. Software controls are not hardware emergency stops. Save connection log after success or failure, including selected settings and observed motion/output; the bounded journal may drop old entries. Nothing is uploaded automatically. Include BUILD-INFO.json when reporting a result.
## Corrected coordinates and framing (0.26)

Your 0.25 log reports Home at MPos 1,1, then 101,101. The previous negative-only restriction was incorrect for this configuration. Keep Device coordinate range at FromHoming: successful Home now establishes each axis range from its reported sign. Positive ranges are [0,travel]; negative ranges [-travel,0]. Zero/missing homed positions need a verified Positive/Negative override in Devices. No work offsets or firmware settings are written.

Open samples/vector-and-image-demo.lcraft, review its independent layers and Preview. Sample power/feed values are marking examples, not a through-cut recipe. Position the head, choose Frame laser off, review clearance and confirm. Frame uses S0 throughout, ends air off and returns to the captured starting position. Then use Run document / Review & Run for powered output. Save the connection log after any failure; document-preflight errors are now included.

The package includes LaserCraft.ico for a Windows shortcut. Use the new extracted LaserCraft.Desktop.exe; update old pinned shortcut paths/icons if necessary.
