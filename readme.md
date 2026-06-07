# projectFrameCut.Drawing

A powerful image processing library, also the core of [projectFrameCut](https://github.com/hexadecimal0x12e/projectFrameCut)
High-performance .NET library for image manipulation, compositing, vector graphics, and text rendering.

## Features

- **Pixel representations** — 8-bit (`byte`) and 16-bit (`ushort`) per-channel RGB(A) images, plus HDR (with per-pixel brightness metadata), monochrome, and bitmask variants
- **File I/O** — PNG encode/decode and a custom VFD format with both 8-bit and 16-bit support
- **Image processing** — compose (multiple blend modes), crop, resize (bilinear), and bit-depth conversion
- **Effects** — invert, grayscale, brightness, contrast, saturation, gamma, threshold, flip, opacity, vignette, sharpen, blur, mask, crop, place, hue rotation, and arbitrary-angle rotation (bilinear-sampled)
- **Vector graphics** — layer-based vector canvas with `VectorCanvasElement` shapes, SVG import/export, and rasterization
- **Text rendering** — text entries, rich-text entries, vertical and horizontal typesetting engines, line-break handling, and glyph canvas elements
- **Processing pipeline** — fluent extension-method chaining via `ProcessableIPictureContext` with full process-stack audit trail for debugging
- **Content-based hashing** — XXH64-based unique IDs for pixel data

## Projects

| Project | Description |
|---|---|
| `Drawing.Base` | Core types: `IPicture`, `Picture8bpp`, `Picture16bpp`, `HDRPicture16bpp`, file loaders, pixel-format conversion |
| `Drawing.Processing` | Compositing (blend), cropping, resizing, HDR tone-mapping |
| `Drawing.Effect` | Pixel/area effects (color, blur, flip, rotate, vignette, etc.) |
| `Drawing.Vector` | Vector canvas, shapes, SVG import/export, vector-to-raster |
| `Drawing.Text` | Text entries, typesetting engines, glyph rendering |
| `Drawing.Text.FontHelper` | Font metadata helpers |
| `Drawing.Canvas` | Drawing surface abstraction |
| `Drawing` | Meta-package that bundles all sub-projects via ILRepack |

## Quick start

```csharp
using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;
using projectFrameCut.Drawing.Processing.Composing;

// Load an image
var img = new Picture8bpp("input.png");

// Apply effects via fluent pipeline
var result = img
    .AsProcessable()
    .Rotate(45f)
    .WithEffect(new GrayscaleEffect())
    .WithEffect(new BrightnessEffect(1.2f))
    .Commit();

// Save
result.SaveAsPng("output.png");
```

## Requirements

- .NET 10.0 or later

## License

Licensed under LGPL-3.0-or-later.
Copyright (c) hexadecimal0x12e 2026. 