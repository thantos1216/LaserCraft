# LaserCraft 0.25 physical qualification guide

Extract the complete portable ZIP locally on the Windows computer with the USB laser attached. Confirm toolbar 0.25. Server storage does not forward USB. Close other port users, select the saved GRBL profile and detected COM port. Devices/settings remain per user under LocalAppData/LaserCraft. No drivers are installed automatically.

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

Document placement restriction in 0.25: Home must succeed in the current connection before document output. Reconnect, job reset or custom console writes invalidate that evidence. This first streaming mode requires a homed negative machine range: X from -profile width to 0, Y from -profile height to 0. Preflight checks the entire translated footprint against those ranges. After homing near machine zero, jog sufficiently inside the bed before running a job extending +X/+Y. Controllers configured for positive machine ranges are not supported by this mode yet. These checks do not prove physical calibration or absence of fixtures/obstructions.
