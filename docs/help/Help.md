# LaserCraft Help

Version 0.2 - compact workspace and vector preview

## Workspace

The layout follows the familiar laser-design arrangement: menus and compact tool icons at the top, numeric object controls below, drawing tools on the left, Cuts / Layers above Laser on the right, and layer colors along the bottom. Drag either divider to resize the panels. Hover icons for tooltips. Disabled tools and panels are not implemented yet.

## Create and edit

Choose Rectangle or Ellipse and drag on the bed. Choose Select to click or move an object. Ctrl-click adds or removes objects from the selection; Ctrl+A selects all visible objects. Dragging one selected object moves the whole selection. Enter X, Y, width and height in the top bar, then Apply. Multiple-object sizing scales their combined bounding box. All dimensions are millimetres. The current design origin is top-left; this differs from some LightBurn/device coordinate setups.

Edit offers Copy, Cut, Paste, Duplicate, Delete and Select all. Copy/paste uses the LaserCraft session clipboard, not the Windows graphics clipboard. Paste offsets objects 5 mm and retains layer colors. Existing target layers keep their cut settings. Arrange offers edge/center alignment, horizontal/vertical flips and Center on bed. Alignment uses the selected objects' combined bounds. Single axis-aligned symmetric shapes will look unchanged when flipped.

## Navigation and history

Wheel zooms. Middle-drag pans. Fit workspace resets the viewport. Snap rounds drawing and movement to 1 mm. Rulers follow design coordinates. Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+D duplicates, Ctrl+C copies and Ctrl+V pastes. Delete works when the canvas is focused; Escape cancels drawing or clears selection. Numeric inputs keep their normal text-editing shortcuts.

## Cuts / Layers

New projects have 30 layers, C00 through C29. Click a bottom color to assign every selected object to that layer; with no selection it chooses the layer for new shapes. The Cuts / Layers list shows used layers and the active drawing layer. Click a row or use the dropdown to edit a layer.

Choose Line or Fill. Set speed in mm/min, power in percent, passes and fill interval in mm. Output controls inclusion in preview; Show controls canvas visibility independently. A hidden layer can still be included in output. Air is saved as a requested air-assist setting; it does not operate hardware. Click Apply cut settings to commit changes. All these values round-trip with the project and participate in undo/redo.

## Preview and simulator

Preview generates paths for enabled layers. Line follows rectangle boundaries and approximates ellipses with 128 segments. Fill scans horizontally using the even-odd rule across shapes on the same layer, leaving nested holes. Each pass repeats the layer. Grey dashed paths are travel; amber paths are cutting. Cut selected graphics limits the plan to selected IDs.

Start plays the preview at 10 times ideal time. Pause freezes playback; Start resumes a paused plan. Stop resets playback and returns to editing. Geometry/layer edits, changing selected-only scope or switching documents invalidate the previous plan. Frame preview checks the output bounds and fits the bed; it does not move a machine.

Output outside the bed is rejected. Estimates assume constant layer speed and 6000 mm/min travel. They omit acceleration, controller delays and material effects. Power and air settings are stored but no physical behavior is simulated. Limits are 100000 fill rows and 200000 plan segments. Increase the line interval or reduce repeated passes for very dense previews.

## Projects and SVG

New creates a document tab. Open loads .lcraft. Save / Ctrl+S persists it. Save as creates another file. A dot in the tab and an asterisk in the title indicate unsaved work. File / Close project saves modified work before closing; canceling the save keeps it open. Closing the entire window asks before discarding unsaved projects.

Import SVG supports untransformed rectangles, circles and ellipses with numeric geometry. Root dimensions can use mm, cm, in or px; pixels use 96 DPI. Uniform viewBox scaling is supported. Imported shapes go to the active layer. CSS styles, paths, transforms, rounded rectangles and other unsupported constructs reject the entire import rather than silently omitting content. Styles and original layer colors are not imported. Export SVG writes geometry with millimetre dimensions; cut settings stay in .lcraft.

Try samples/studio-demo.lcraft for line/fill preview, or samples/basic-shapes.svg for the supported import subset. The sample settings are illustrative, not material recommendations.

## Appearance and Help

Window / Light / dark switches the theme; the preference is saved for your account. Window / Help opens this document, which is copied from docs/help/Help.md on each build. The design canvas stays dark in both themes.

## Remaining functionality

This is an independently implemented application under development. Full LightBurn parity is not achieved. Lines/Bezier paths, text, nodes, groups, booleans, raster processing, tracing, advanced imports, real docking, multi-page documents, cameras, rotary, material libraries and AI remain planned. Save G-code, Run G-code, Home, Go to origin and Devices are disabled because controller integration is not implemented. No serial/network connection or laser operation occurs in this build. The feature parity matrix records detailed progress.
