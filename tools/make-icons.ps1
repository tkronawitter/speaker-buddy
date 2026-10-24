# Generates the Speaker Buddy logo icons (logo D, "face in the colour ring") from the geometry of
# the SVG masters in assets/logo (speaker-buddy*.svg; -32 and -16 are the small-size variants),
# drawn with System.Drawing at each size, no extra tools. Change a master, change this script too.
# Usage: pwsh tools/make-icons.ps1
# Writes assets/logo/speaker-buddy.ico and speaker-buddy-waiting.ico with 16, 24, 32, 48 and 256 px.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\assets\logo'
$sizes = 16, 24, 32, 48, 256

$normal = @{ Ring = '#8000a0', '#1a9e3a', '#c00000'; Smile = '#1a9e3a' }
$waiting = @{ Ring = '#6e6e6e', '#8a8a8a', '#9e9e9e'; Smile = '#8a8a8a' }

function Color([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# Draws the logo into a size x size bitmap; coordinates are in the 64 x 64 units of the SVG
function Draw-Logo([int] $size, $palette) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 64.0, $size / 64.0)

    # Thicker ring and no smile at small sizes, as in the small-size SVGs
    $ring = if ($size -le 16) { 14 } elseif ($size -le 32) { 12 } else { 10 }

    # Three segments of 46 on the circumference 2*pi*24 (109.8 degrees), starting at 12 o'clock
    # and every 120 degrees clockwise: purple, green, red
    $sweep = 46 / (2 * [Math]::PI * 24) * 360
    $starts = -90, 30, 150
    for ($i = 0; $i -lt 3; $i++) {
        $pen = [System.Drawing.Pen]::new((Color $palette.Ring[$i]), $ring)
        $g.DrawArc($pen, 8, 8, 48, 48, $starts[$i], $sweep)
        $pen.Dispose()
    }

    $face = [System.Drawing.SolidBrush]::new((Color '#141414'))
    $g.FillEllipse($face, 16, 16, 32, 32)
    $face.Dispose()

    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    if ($size -le 16) {
        $g.FillEllipse($white, 22, 26, 8, 8)
        $g.FillEllipse($white, 34, 26, 8, 8)
    }
    else {
        $g.FillEllipse($white, 23.5, 26, 6, 6)
        $g.FillEllipse($white, 34.5, 26, 6, 6)
        # The SVG's quadratic smile M25 36 q7 6 14 0 as the equivalent cubic Bezier
        $pen = [System.Drawing.Pen]::new((Color $palette.Smile), 3.5)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawBezier($pen, 25, 36, (25 + 14 / 3), 40, (39 - 14 / 3), 40, 39, 36)
        $pen.Dispose()
    }
    $white.Dispose()
    $g.Dispose()
    return $bitmap
}

# One icon image: PNG for 256 px, a 32-bit DIB with an empty AND mask below that
function Icon-Image([System.Drawing.Bitmap] $bitmap) {
    $size = $bitmap.Width
    $stream = [IO.MemoryStream]::new()
    if ($size -ge 256) {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    }
    $writer = [IO.BinaryWriter]::new($stream)
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    # BITMAPINFOHEADER; the height counts the colour rows and the mask rows
    $writer.Write([uint32]40); $writer.Write([int32]$size); $writer.Write([int32]($size * 2))
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
    $writer.Write([uint32]($size * $size * 4 + $maskStride * $size))
    $writer.Write([int32]0); $writer.Write([int32]0); $writer.Write([uint32]0); $writer.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $p = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$p.B); $writer.Write([byte]$p.G); $writer.Write([byte]$p.R); $writer.Write([byte]$p.A)
        }
    }
    $writer.Write([byte[]]::new($maskStride * $size))
    $writer.Flush()
    return , $stream.ToArray()
}

function Write-Icon([string] $path, $palette) {
    $images = foreach ($size in $sizes) {
        $bitmap = Draw-Logo $size $palette
        , (Icon-Image $bitmap)
        $bitmap.Dispose()
    }
    $stream = [IO.File]::Create($path)
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
    $writer.Dispose()
    '{0} ({1} bytes)' -f $path, (Get-Item $path).Length
}

Write-Icon (Join-Path $out 'speaker-buddy.ico') $normal
Write-Icon (Join-Path $out 'speaker-buddy-waiting.ico') $waiting
