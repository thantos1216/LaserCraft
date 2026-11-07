# Raster pipeline reference and definitions

This is an independent reference implementation, not copied commercial processing code. Public algorithm context: [ImageMagick quantization/dithering options](https://imagemagick.org/command-line-options/#dither). ImageMagick is not a dependency and its output is not claimed to match this pipeline. The supplied LightBurn manual's Image Mode chapter remains the functional inventory reference.

Decode/resample uses the existing SkiaSharp package. RGB samples are unpremultiplied; weighted sRGB luminance uses 0.2126 / 0.7152 / 0.0722. Brightness is an additive fraction; contrast scales around 0.5; gamma applies exponent 1/gamma; inversion follows tone adjustments. Alpha blends the adjusted tone toward white. Fully transparent samples are explicitly off, including during error diffusion.

Threshold uses the configured brightness threshold. Floyd-Steinberg uses left-to-right rows and distributes quantization error right/down-left/down/down-right with weights 7/16, 3/16, 5/16 and 1/16. Bayer uses a fixed 4x4 ordered threshold matrix. Grayscale preserves an 8-bit darkness level for proportional power requests. These are software samples, not calibrated physical engraving predictions.

The work-grid dimensions round up from physical bounds divided by interval. Cells uniformly cover the original physical bounds, and the image dialog reports effective DPI. Scan direction and mirrored placement are applied in planning after processing. Constant darkness runs share power; white runs and overscan are off moves at scan speed. Entirely white rows are skipped. Inter-path moves use the current ideal travel speed. Physical acceleration, laser response, controller power units and overscan calibration remain future integration work.
