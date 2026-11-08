# LaserCraft Help

Version 0.6 - editable straight paths and node movement

## Create layers and control order

New projects start with one vector layer. In Cuts / Layers, choose + Layer, enter a name, and choose Vector or Image. You can optionally assign the current selection when creating it. Each layer has its own name, color, speed, power, passes, output, visibility and air-assist setting. Edit the fields below the list and click Apply cut settings.

Enabled layers run from top to bottom. Select a row and use the up/down buttons to change its job order. Object associations and settings move with the layer. Reordering is undoable. Delete empty removes unused layers; move their objects elsewhere first. At least one layer remains. Existing projects keep their layer lists.

Choose the Layer color button to open a visual picker with palette swatches and red/green/blue sliders. Apply color updates the selected layer and its artwork, and can be undone. Cancel keeps its previous color. Hex entry is no longer required.

To reassign artwork, select objects on the canvas, select the destination layer row, and click Assign selected. Clicking its bottom color swatch also assigns the selection. With no selection, a swatch chooses the drawing layer. Imported SVG geometry uses vector layers, just like drawn shapes. Colors identify job settings; they do not recolor a photograph.

## Separate image layers

An image must have its own Image layer. It cannot share a layer with lines, shapes, SVG vectors or another image. Assignments that violate this rule are rejected without changing the document. Duplicating an image creates another dedicated layer. Vector layers support Line/Fill; image layers use Image mode.

File / Import image accepts PNG, JPEG and BMP. Original encoded bytes are embedded in .lcraft, so reopening does not depend on the source file. Images support selection, movement, numerical resizing, mirroring, copy/duplicate and layer reassignment. They have a colored border showing the assigned layer while their original pixels remain unchanged. Drawing tools switch back to an eligible vector layer.

Select the image or its image layer and choose Image settings…. The dialog compares the original with prepared engraving pixels. Choose Threshold, FloydSteinberg, OrderedBayer (4 x 4), Grayscale, Stucki, Jarvis or Atkinson; adjust brightness, contrast, gamma and inversion. Threshold is enabled only for Threshold mode. Update preview recalculates the sample view; Apply saves the independent settings to that layer. Cancel leaves the project unchanged. Original image bytes remain untouched and undo/redo includes processing settings.

Interval sets a maximum sample spacing in mm; sample counts are rounded upward and evenly cover the full physical image size, so effective DPI may be slightly higher than 25.4 / interval. The dialog reports effective horizontal/vertical DPI. Transparent pixels stay laser-off even when inverted. Partial alpha blends toward white after tone adjustments. Processing uses sRGB-weighted luminance and 8-bit output; no material-response or optical calibration is implied.

Enabled image layers now contribute scanline paths in their list order alongside vector layers. Threshold and dithers map enabled samples to layer power; Grayscale maps sample darkness proportionally from zero to the layer power. Consecutive equal-power cells form runs. White runs are off and all-white rows are skipped. Mirror flags affect sample placement. Bidirectional alternates row direction; turning it off returns to the left for each row. Overscan adds laser-off travel before and after the image; out-of-bed overscan is rejected. Off regions during a row use the layer scan speed; moves between rows use the current ideal 6000 mm/min travel assumption.

Source pixels stay visible behind the path overlay as design context. The prepared image dialog is the monochrome sample view, not a physical burn prediction. SVG export embeds original raster data; tone/dither settings stay in .lcraft. SVG import currently accepts only the vector subset described below.

Limits: 8 MB per imported encoded image; 16000 pixels per dimension; 40 megapixels per original; 128 image objects, 80 megapixels of unique assets and 24 MB of base64 image data per project. Preview textures are limited to 2048 pixels on their longest edge. Projects are limited to 32 MB on disk. Large-image tiling and asset deduplication remain planned.

## Device profiles and connection

Choose Devices… in the Laser panel or Laser Tools menu. Create a named profile and select the controller family, Simulator/Serial/TCP connection, COM port, baud rate, TCP host/port, bed dimensions, origin and S-value maximum. Serial uses 8 data bits, no parity, 1 stop bit and no flow control; DTR/RTS are optional. Opening some serial ports can reset their controller. Profile settings are stored under LocalAppData/LaserCraft/devices.json, separately from projects.

GRBL has a read-only connection adapter. grblHAL and FluidNC use an experimental GRBL-compatible adapter. Marlin, Smoothieware, Ruida and Trocen can be saved as profiles but do not yet have connection adapters. Selecting a controller family is not a claim of tested machine compatibility. No Atezr hardware has been tested.

Select a saved profile, then Connect. The application queries firmware identity ($I) and status (?), without homing, jogging, firing or streaming a job. Read status refreshes the report; Disconnect closes the connection. Status coordinates identify MPos or WPos exactly as reported. Missing coordinates stay unknown. A built-in GRBL simulator is the default and works without hardware. Connection cancellation, line limits and response timeouts bound reads; real serial hardware behavior remains unverified.

Apply device bed explicitly changes the document dimensions to the selected profile. It does not move a machine. Origin and power range are stored for future job-coordinate/power mapping; the current design model uses a top-left origin. Device profile changes do not silently alter document geometry or cut settings.

## Air assist

Each layer has an Air assist on/off checkbox. The state is saved and follows its objects when the layer is reordered. Cut-plan segments carry the requested state; travel segments do not. The device profile records whether its controller has a configured air-assist output. GRBL export emits M8/M9 for these requests and rejects enabled air requests when the profile does not support them. The app sends no air commands to a device.

## Vector editing and navigation

Lines creates straight vector paths: click vertices, then Enter or double-click to finish an open path. C or clicking the first vertex closes a contour with at least three vertices. Backspace removes the last draft vertex; Escape cancels the draft. Switching tools cancels unfinished drawing. A draft has at most 10000 vertices. Drawing is restricted to the bed and commits as one undo step.

Nodes selects a path near its outline. Its square vertices can be dragged; releasing commits one undo step. Tools / Convert selected to paths converts rectangles to four corners and ellipses to 128 straight segments. This conversion is undoable, retains layer/identity, and cannot convert images. Tools / Close selected paths closes every contour in selected paths; each needs at least three vertices. Curve handles and vertex insertion/deletion/join/split are not implemented yet. Select still moves whole objects; numerical resizing, copy, layer assignment and mirroring also apply to paths.

Open and closed paths can be cut in Line mode. Fill requires every contour to be closed and uses the even-odd rule across shapes/contours, preserving holes independent of contour direction. Unconnected contours are separated by laser-off travel. Horizontal or vertical paths can have zero height or width; numeric scaling cannot create a missing extent, so move a vertex with Nodes to change their direction. Path files have limits of 100000 vertices per object, 200000 per project and 10000 contours per object. Dense fill checks an intersection-work budget before scanning. Bézier paths, arcs and automatic tracing remain planned.

Try samples/paths-and-nodes.lcraft for compound fill, a polyline and a horizontal line. samples/linear-paths.svg exercises supported straight-path SVG import. Imported styles are not applied; layer settings govern output. SVG path imports use SVG user units and the root viewBox mapping. SVG exports straight contours with M/L/Z; native files retain open/closed topology and normalized immutable vertices.

Choose Rectangle or Ellipse and drag. Select clicks or moves an object. Ctrl-click adds/removes selection; Ctrl+A selects visible objects. Line shapes are selected near their outlines so a surrounding frame does not intercept an image inside it. Dragging a selected object moves the selection. Enter X, Y, width and height at the top, then Apply. Multi-object sizing scales their combined bounds, in millimetres.

Wheel zooms; middle-drag pans; Fit workspace resets the view. Snap rounds to 1 mm. Ctrl+Z/ Ctrl+Y undo/redo. Ctrl+C copies, Ctrl+V pastes, Ctrl+D duplicates. The internal artwork clipboard is separate from the Windows graphics clipboard. Paste offsets 5 mm and retains layer colors; target vector layers keep existing settings. Delete removes selected objects when the canvas is focused; Escape deselects/cancels. Arrange provides alignment, flips and Center on bed.

## Vector preview and playback

Images on the design canvas now show their monochrome preparation, retaining the layer color only on the border. Source color bytes remain embedded for nondestructive editing and SVG export. Canvas previews are bounded to 800 samples on the longest edge and may differ in dot detail from the full output-resolution samples in Image settings. Changing tone/mode/interval invalidates this background-generated display; mirroring also mirrors the displayed preparation. Image settings includes Stucki, Jarvis and Atkinson in addition to Threshold, Floyd-Steinberg, Ordered Bayer and Grayscale. These are independent implementations, not claims of pixel-identical LightBurn output.

Preview hides the editing artwork and shows the planned output on a white bed. Cutting marks use black with opacity proportional to commanded power; Show travel controls pale dashed off-power movements. Start animates a red beam marker. Playback speed is adjustable from 1× to 100×; Pause holds it and Stop / Return to design restores editing. This is an ideal path/power visualization, not a material-specific darkness or thermal model.

Preview plans enabled layers in list order. Line follows rectangles and approximates ellipses with 128 segments. Fill scans horizontally with the even-odd rule, leaving nested holes. Passes repeat their layer. Grey dashed paths are travel; black marks are cutting. Cut selected graphics limits the plan to selected objects.

Start/Pause/Stop control only offline playback, even while a controller monitor is connected. Playback defaults to 10 times ideal time and can be adjusted. Edits, layer reordering, selected-only scope changes or document switches invalidate the old plan. Frame preview checks bed bounds and fits the viewport; it does not move a machine. Estimates assume constant layer speed and 6000 mm/min inter-path travel, without acceleration or material effects. Power levels are planning requests; no physical burn is simulated. Preview generation runs in the background and Stop cancels it. Edits or scope changes cancel/discard stale work. Dense plans are limited to 100000 fill rows, 200000 segments, 16000 samples per axis and 4 million samples per image, 8 million raster cells including repeated passes per job, and a bounded vector intersection-work budget. Increase interval or reduce passes for dense plans. Beyond 25000 segments the canvas samples paths for display; complete paths still drive timing and playback. High-resolution large-photo planning needs further optimization.

## Files and SVG

Save G-code creates an offline GRBL 1.1 laser-mode file from enabled layers (or Cut selected graphics). Select the intended device profile first. The exporter uses its bed dimensions, corner origin, S-value maximum and configured air-output capability; it supports only the GRBL family for now. Bottom origins reflect Y and right origins reflect X around that bed. Set the controller work zero to the chosen bed corner and match firmware $30 to the S-value maximum. Firmware must already have $32=1; export never changes firmware settings. Files use millimetres, absolute work coordinates, units-per-minute feed, M4 dynamic power, explicit S0 travel, optional M8 air, and final M5/M9. Feed-controlled off spans retain raster overscan speed. It adds an initial laser-off rapid to the plan start, but does not home or return home. Bounds, continuity, finite feeds/powers and nonempty plans are validated. Physical execution and controller-specific qualification remain unavailable. The app does not run or transmit saved programs.

New opens a document tab. Save / Ctrl+S persists .lcraft; Save as creates another file. A tab dot/title asterisk marks unsaved work. File / Close project saves modified work first; canceling save keeps it open. Closing the entire window asks before discarding changes. A .lcraft path can also be supplied as a startup argument.

SVG import supports untransformed numeric rectangles, circles and ellipses, root mm/cm/in/px dimensions (96 DPI pixels) and uniform viewBox scaling. SVG paths support absolute/relative M/L/H/V/Z commands, repeated coordinates and compound contours; line, polyline and polygon elements are supported too. Curves/arcs, styles, transforms, rounded rectangles and other constructs reject the entire import. Geometry goes to the active vector layer, or a new vector layer if an image layer was active. SVG export retains geometry and embedded raster sources; .lcraft retains job settings. Full SVG and additional formats remain planned.

Try samples/raster-engraving.lcraft for a working mixed raster/vector preview, samples/layers-and-image.lcraft for the image/layer workflow, samples/studio-demo.lcraft for nested vector fill, and samples/basic-shapes.svg for supported SVG import. Sample settings are illustrative, not material recommendations.

## Appearance, source and remaining work

Window / Light / dark switches the persisted theme. Help is copied from docs/help/Help.md at build time. LaserCraft's original source is MIT-licensed; third-party packages retain their own licenses. No LightBurn source or protected assets are used.

Full parity remains unfinished: Bezier curves, text, advanced node operations, tracing, booleans, additional raster algorithms and high-resolution optimization, advanced imports, real docking, cameras, rotary, materials, AI and physical job streaming remain planned. Console is currently a read-only log; arbitrary command entry and macros remain planned. Disabled commands identify unimplemented capabilities. The feature matrix and progress record distinguish tested behavior from planned work.
