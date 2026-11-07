# LaserCraft Help

Version 0.1 - desktop foundation

## Your first design

1. Choose Rectangle or Ellipse on the left.
2. Drag on the bed to draw. All dimensions are millimetres.
3. Choose Select, then click or drag an object to select or move it.
4. Edit its X, Y, width and height in Inspect, then choose Apply geometry.
5. Save your editable project as .lcraft. Export SVG for use in other design software.

The default 400 x 400 mm bed is a design workspace, not a verified device profile.

## Navigation

Mouse wheel zooms. Hold the middle mouse button and drag to pan. Fit bed resets the view. Snap to 1 mm rounds drawing and movement coordinates to whole millimetres. New creates a separate document tab. Click a tab to return to a project.

## Layers

Two layers are available: Outline (cyan) and Detail (violet). The layer dropdown chooses the layer for new objects. To move an existing object to that layer, select it and choose Apply geometry. Speed is stored in mm/min and power in percent. Apply layer settings stores these values with the project. They do not operate hardware.

## Editing and history

Undo and Redo restore geometry and layer edits. Ctrl+Z and Ctrl+Y are shortcuts. Delete removes the selected object when the canvas has keyboard focus. Escape cancels an active drawing or clears the selection. Objects outside the bed are clipped visually but remain in the project and SVG export; check your design before using it elsewhere.

## Files

Save and Ctrl+S save the current document. Save as creates a separate file. A dot next to a tab name indicates unsaved changes. Closing the window with unsaved work asks whether to discard it. Open validates the file version, dimensions, layer references, and basic geometry. Writes use a temporary file followed by replacement.

SVG export preserves rectangles, ellipses and millimetre dimensions. It does not contain LaserCraft cut settings or history. SVG import is not available yet. Project recovery and embedded raster sources are planned.

## Appearance

Light / dark switches the interface theme. The selection is stored for your Windows account in LocalAppData/LaserCraft/settings.json. The design canvas stays dark in both themes to keep layer colours readable. Drag the divider beside Inspect to resize the side panel.

## Current capabilities

This build supports rectangle and ellipse editing, document tabs, zoom and pan, basic snapping, two editable layer presets, undo/redo, project save/load, SVG export, and this integrated help document.

Hardware communication, G-code generation, raster processing, text, nodes, booleans, tracing, camera, rotary, nesting, and AI are not implemented in this build. No laser connection or job execution is available.

## Development documentation

The feature parity matrix and progress record describe implemented, tested and planned functionality. Help is copied from docs/help/Help.md during every build; update that source whenever the user workflow changes.
