param(
    [string]$SourceDirectory = "$HOME\Downloads",
    [string]$OutputDirectory = "$PSScriptRoot\..\Assets\Art\Creep_Lady_Frames\headturn",
    [string]$PreviewPath = "",
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

# Visual one-way order for the May 20 head-turn set. The exported animation then
# ping-pongs in Unity, so the forward pass should not already turn back on itself.
$sourceFileNames = @(
    "ChatGPT Image May 20, 2026, 02_53_45 PM (4).png",
    "ChatGPT Image May 20, 2026, 02_53_45 PM (3).png",
    "ChatGPT Image May 20, 2026, 02_53_44 PM (2).png",
    "ChatGPT Image May 20, 2026, 02_53_45 PM (5).png",
    "ChatGPT Image May 20, 2026, 02_53_46 PM (10).png",
    "ChatGPT Image May 20, 2026, 02_53_44 PM (1).png",
    "ChatGPT Image May 20, 2026, 02_53_45 PM (6).png",
    "ChatGPT Image May 20, 2026, 02_53_46 PM (8).png",
    "ChatGPT Image May 20, 2026, 02_53_46 PM (9).png",
    "ChatGPT Image May 20, 2026, 02_53_45 PM (7).png"
)

$sources = $sourceFileNames | ForEach-Object { Join-Path $SourceDirectory $_ }
foreach ($source in $sources) {
    if (!(Test-Path -LiteralPath $source)) {
        throw "Missing source frame: $source"
    }
}

if ([string]::IsNullOrWhiteSpace($PreviewPath)) {
    $PreviewPath = Join-Path $OutputDirectory "headturn_preview.gif"
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $OutputDirectory "headturn_normalization_report.txt"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Add-Type -AssemblyName System.Drawing

$source = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;

public static class HeadTurnFrameNormalizer
{
    private struct FrameData
    {
        public string SourcePath;
        public int Width;
        public int Height;
        public byte[] Pixels;
        public Rectangle Bounds;
        public double AnchorX;
        public double AnchorY;
        public int ShiftX;
        public int ShiftY;
    }

    public static string Normalize(string[] sourcePaths, string outputDirectory, string previewPath, string reportPath)
    {
        if (sourcePaths == null || sourcePaths.Length == 0)
        {
            throw new ArgumentException("At least one source path is required.", "sourcePaths");
        }

        FrameData[] frames = new FrameData[sourcePaths.Length];

        for (int i = 0; i < sourcePaths.Length; i++)
        {
            frames[i] = LoadFrame(sourcePaths[i]);
        }

        int canvasWidth = frames[0].Width;
        int canvasHeight = frames[0].Height;

        for (int i = 1; i < frames.Length; i++)
        {
            if (frames[i].Width != canvasWidth || frames[i].Height != canvasHeight)
            {
                throw new InvalidOperationException("All source frames must have the same canvas size before anchor alignment.");
            }
        }

        double targetAnchorX = Median(frames.Select(frame => frame.AnchorX).ToArray());
        double targetAnchorY = Median(frames.Select(frame => frame.AnchorY).ToArray());

        string[] outputPaths = new string[frames.Length];

        for (int i = 0; i < frames.Length; i++)
        {
            frames[i].ShiftX = (int)Math.Round(targetAnchorX - frames[i].AnchorX);
            frames[i].ShiftY = (int)Math.Round(targetAnchorY - frames[i].AnchorY);
            byte[] shifted = ShiftPixels(frames[i], canvasWidth, canvasHeight);
            string outputPath = Path.Combine(outputDirectory, string.Format("headturn_{0:00}.png", i));
            SavePng(outputPath, canvasWidth, canvasHeight, shifted);
            outputPaths[i] = outputPath;
        }

        WritePreviewGif(outputPaths, previewPath);
        WriteReport(frames, outputPaths, reportPath, canvasWidth, canvasHeight, targetAnchorX, targetAnchorY);

        return reportPath;
    }

    private static FrameData LoadFrame(string path)
    {
        using (Bitmap source = new Bitmap(path))
        using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);

            byte[] pixels = ReadPixels(bitmap);
            bool[] background = FloodBackground(pixels, bitmap.Width, bitmap.Height);
            ApplyAlphaFromFloodedBackground(pixels, background, bitmap.Width, bitmap.Height);

            Rectangle bounds = FindAlphaBounds(pixels, bitmap.Width, bitmap.Height);
            PointF anchor = FindStableBodyAnchor(pixels, bitmap.Width, bitmap.Height, bounds);

            return new FrameData
            {
                SourcePath = path,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Pixels = pixels,
                Bounds = bounds,
                AnchorX = anchor.X,
                AnchorY = anchor.Y
            };
        }
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int rowBytes = bitmap.Width * 4;
            int stride = Math.Abs(data.Stride);
            byte[] raw = new byte[stride * bitmap.Height];
            byte[] pixels = new byte[rowBytes * bitmap.Height];

            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            for (int y = 0; y < bitmap.Height; y++)
            {
                Buffer.BlockCopy(raw, y * stride, pixels, y * rowBytes, rowBytes);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void SavePng(string path, int width, int height, byte[] pixels)
    {
        using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int rowBytes = width * 4;
                int stride = Math.Abs(data.Stride);
                byte[] raw = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(pixels, y * rowBytes, raw, y * stride, rowBytes);
                }

                Marshal.Copy(raw, 0, data.Scan0, raw.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.Save(path, ImageFormat.Png);
        }
    }

    private static bool[] FloodBackground(byte[] pixels, int width, int height)
    {
        bool[] background = new bool[width * height];
        int[] queue = new int[width * height];
        int head = 0;
        int tail = 0;

        Action<int, int> trySeed = (x, y) =>
        {
            int index = y * width + x;

            if (!background[index] && LooksLikeExteriorBackground(pixels, width, x, y))
            {
                background[index] = true;
                queue[tail++] = index;
            }
        };

        for (int x = 0; x < width; x++)
        {
            trySeed(x, 0);
            trySeed(x, height - 1);
        }

        for (int y = 1; y < height - 1; y++)
        {
            trySeed(0, y);
            trySeed(width - 1, y);
        }

        while (head < tail)
        {
            int index = queue[head++];
            int x = index % width;
            int y = index / width;

            TryQueueBackgroundNeighbor(pixels, width, height, x + 1, y, background, queue, ref tail);
            TryQueueBackgroundNeighbor(pixels, width, height, x - 1, y, background, queue, ref tail);
            TryQueueBackgroundNeighbor(pixels, width, height, x, y + 1, background, queue, ref tail);
            TryQueueBackgroundNeighbor(pixels, width, height, x, y - 1, background, queue, ref tail);
        }

        return background;
    }

    private static void TryQueueBackgroundNeighbor(byte[] pixels, int width, int height, int x, int y, bool[] background, int[] queue, ref int tail)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        int index = y * width + x;

        if (background[index] || !LooksLikeExteriorBackground(pixels, width, x, y))
        {
            return;
        }

        background[index] = true;
        queue[tail++] = index;
    }

    private static bool LooksLikeExteriorBackground(byte[] pixels, int width, int x, int y)
    {
        int offset = (y * width + x) * 4;
        byte b = pixels[offset];
        byte g = pixels[offset + 1];
        byte r = pixels[offset + 2];
        byte a = pixels[offset + 3];

        return a <= 8 || (r >= 242 && g >= 242 && b >= 242);
    }

    private static void ApplyAlphaFromFloodedBackground(byte[] pixels, bool[] background, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                int offset = index * 4;

                if (background[index])
                {
                    pixels[offset] = 255;
                    pixels[offset + 1] = 255;
                    pixels[offset + 2] = 255;
                    pixels[offset + 3] = 0;
                    continue;
                }

                if (pixels[offset + 3] < 255)
                {
                    continue;
                }

                if (TouchesBackground(background, width, height, x, y) && IsNearWhite(pixels, offset))
                {
                    int darkestDistance = 255 - Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + 2]));
                    int alpha = ClampByte(darkestDistance * 9);
                    pixels[offset + 3] = (byte)Math.Max(32, alpha);
                }
                else
                {
                    pixels[offset + 3] = 255;
                }
            }
        }
    }

    private static bool TouchesBackground(bool[] background, int width, int height, int x, int y)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int nx = x + offsetX;
                int ny = y + offsetY;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                if (background[ny * width + nx])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsNearWhite(byte[] pixels, int offset)
    {
        return pixels[offset] >= 210 && pixels[offset + 1] >= 210 && pixels[offset + 2] >= 210;
    }

    private static Rectangle FindAlphaBounds(byte[] pixels, int width, int height)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int alpha = pixels[(y * width + x) * 4 + 3];

                if (alpha <= 24)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new Rectangle(0, 0, width, height);
        }

        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static PointF FindStableBodyAnchor(byte[] pixels, int width, int height, Rectangle bounds)
    {
        int yMin = bounds.Top + (int)Math.Round(bounds.Height * 0.43);
        int yMax = bounds.Top + (int)Math.Round(bounds.Height * 0.92);
        double sumWeight = 0.0;
        double sumX = 0.0;
        double sumY = 0.0;

        for (int y = Math.Max(0, yMin); y < Math.Min(height, yMax); y++)
        {
            double normalizedY = bounds.Height > 0 ? (double)(y - bounds.Top) / bounds.Height : 0.0;
            double verticalWeight = 1.0 + Math.Max(0.0, 1.0 - Math.Abs(normalizedY - 0.70) / 0.28) * 2.0;

            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                int offset = (y * width + x) * 4;
                int alpha = pixels[offset + 3];

                if (alpha <= 24)
                {
                    continue;
                }

                byte b = pixels[offset];
                byte g = pixels[offset + 1];
                byte r = pixels[offset + 2];
                double colorWeight = 1.0;

                if (r > g + 14 && r > b + 22)
                {
                    colorWeight += 0.75;
                }

                if (r < 95 && g < 95 && b < 95)
                {
                    colorWeight += 0.4;
                }

                double weight = (alpha / 255.0) * verticalWeight * colorWeight;
                sumWeight += weight;
                sumX += x * weight;
                sumY += y * weight;
            }
        }

        if (sumWeight <= 0.0)
        {
            return new PointF(bounds.Left + bounds.Width * 0.5f, bounds.Top + bounds.Height * 0.68f);
        }

        return new PointF((float)(sumX / sumWeight), (float)(sumY / sumWeight));
    }

    private static byte[] ShiftPixels(FrameData frame, int width, int height)
    {
        byte[] shifted = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int targetY = y + frame.ShiftY;

            if (targetY < 0 || targetY >= height)
            {
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                int targetX = x + frame.ShiftX;

                if (targetX < 0 || targetX >= width)
                {
                    continue;
                }

                int sourceOffset = (y * width + x) * 4;

                if (frame.Pixels[sourceOffset + 3] == 0)
                {
                    continue;
                }

                int targetOffset = (targetY * width + targetX) * 4;
                shifted[targetOffset] = frame.Pixels[sourceOffset];
                shifted[targetOffset + 1] = frame.Pixels[sourceOffset + 1];
                shifted[targetOffset + 2] = frame.Pixels[sourceOffset + 2];
                shifted[targetOffset + 3] = frame.Pixels[sourceOffset + 3];
            }
        }

        return shifted;
    }

    private static void WriteReport(FrameData[] frames, string[] outputPaths, string reportPath, int width, int height, double targetAnchorX, double targetAnchorY)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Head-turn normalization report");
        builder.AppendLine("Canvas: " + width + "x" + height);
        builder.AppendLine("Anchor: lower torso/coat/hands weighted centroid");
        builder.AppendLine("Target anchor: " + targetAnchorX.ToString("F2") + ", " + targetAnchorY.ToString("F2"));
        builder.AppendLine("Playback order: 0..N-1, then N-2..1 through Unity ping-pong playback");
        builder.AppendLine();

        for (int i = 0; i < frames.Length; i++)
        {
            builder.AppendFormat(
                "{0:00}: {1} -> {2}, anchor=({3:F2},{4:F2}), shift=({5},{6}), bounds=({7},{8},{9},{10})",
                i,
                Path.GetFileName(frames[i].SourcePath),
                Path.GetFileName(outputPaths[i]),
                frames[i].AnchorX,
                frames[i].AnchorY,
                frames[i].ShiftX,
                frames[i].ShiftY,
                frames[i].Bounds.X,
                frames[i].Bounds.Y,
                frames[i].Bounds.Width,
                frames[i].Bounds.Height);
            builder.AppendLine();
        }

        File.WriteAllText(reportPath, builder.ToString());
    }

    private static void WritePreviewGif(string[] framePaths, string previewPath)
    {
        if (framePaths == null || framePaths.Length == 0)
        {
            return;
        }

        int previewWidth = 240;
        int previewHeight = 360;
        int[] order = BuildPingPongOrder(framePaths.Length);
        Bitmap[] frames = new Bitmap[order.Length];

        try
        {
            for (int i = 0; i < order.Length; i++)
            {
                using (Bitmap source = new Bitmap(framePaths[order[i]]))
                {
                    frames[i] = CreatePreviewFrame(source, previewWidth, previewHeight);
                }
            }

            SaveAnimatedGif(frames, previewPath, 8);
        }
        finally
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    frames[i].Dispose();
                }
            }
        }
    }

    private static int[] BuildPingPongOrder(int frameCount)
    {
        if (frameCount <= 1)
        {
            return new int[] { 0 };
        }

        if (frameCount == 2)
        {
            return new int[] { 0, 1 };
        }

        int[] order = new int[frameCount * 2 - 2];
        int index = 0;

        for (int i = 0; i < frameCount; i++)
        {
            order[index++] = i;
        }

        for (int i = frameCount - 2; i >= 1; i--)
        {
            order[index++] = i;
        }

        return order;
    }

    private static Bitmap CreatePreviewFrame(Bitmap source, int width, int height)
    {
        Bitmap frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(frame))
        using (Brush light = new SolidBrush(Color.FromArgb(255, 236, 236, 236)))
        using (Brush dark = new SolidBrush(Color.FromArgb(255, 214, 214, 214)))
        {
            const int tile = 12;

            for (int y = 0; y < height; y += tile)
            {
                for (int x = 0; x < width; x += tile)
                {
                    graphics.FillRectangle(((x / tile + y / tile) % 2 == 0) ? light : dark, x, y, tile, tile);
                }
            }

            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.DrawImage(source, 0, 0, width, height);
        }

        return frame;
    }

    private static void SaveAnimatedGif(Bitmap[] frames, string path, int delayHundredths)
    {
        ImageCodecInfo gifEncoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Gif.Guid);
        System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.SaveFlag;
        EncoderParameters parameters = new EncoderParameters(1);

        using (Bitmap first = frames[0])
        {
            first.SetPropertyItem(CreateFrameDelayProperty(frames.Length, delayHundredths));
            first.SetPropertyItem(CreateLoopProperty());

            parameters.Param[0] = new EncoderParameter(encoder, (long)EncoderValue.MultiFrame);
            first.Save(path, gifEncoder, parameters);

            parameters.Param[0] = new EncoderParameter(encoder, (long)EncoderValue.FrameDimensionTime);

            for (int i = 1; i < frames.Length; i++)
            {
                first.SaveAdd(frames[i], parameters);
            }

            parameters.Param[0] = new EncoderParameter(encoder, (long)EncoderValue.Flush);
            first.SaveAdd(parameters);
            frames[0] = null;
        }
    }

    private static PropertyItem CreateFrameDelayProperty(int frameCount, int delayHundredths)
    {
        PropertyItem item = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
        item.Id = 0x5100;
        item.Type = 4;
        item.Len = frameCount * 4;
        item.Value = new byte[item.Len];

        for (int i = 0; i < frameCount; i++)
        {
            byte[] value = BitConverter.GetBytes(delayHundredths);
            Buffer.BlockCopy(value, 0, item.Value, i * 4, 4);
        }

        return item;
    }

    private static PropertyItem CreateLoopProperty()
    {
        PropertyItem item = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
        item.Id = 0x5101;
        item.Type = 3;
        item.Len = 2;
        item.Value = new byte[] { 0, 0 };
        return item;
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;

        if (values.Length % 2 == 1)
        {
            return values[middle];
        }

        return (values[middle - 1] + values[middle]) * 0.5;
    }

    private static int ClampByte(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 255)
        {
            return 255;
        }

        return value;
    }
}
"@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing

$report = [HeadTurnFrameNormalizer]::Normalize($sources, (Resolve-Path $OutputDirectory).Path, $PreviewPath, $ReportPath)

function Get-MetaGuid {
    param([string]$MetaPath)

    if (!(Test-Path -LiteralPath $MetaPath)) {
        return $null
    }

    $line = Get-Content -LiteralPath $MetaPath | Where-Object { $_ -match "^guid:\s+([0-9a-fA-F]+)" } | Select-Object -First 1

    if ($line -match "^guid:\s+([0-9a-fA-F]+)") {
        return $Matches[1]
    }

    return $null
}

function Write-TextureMeta {
    param(
        [string]$PngPath,
        [string]$Guid
    )

    $metaPath = "$PngPath.meta"
    $existingGuid = Get-MetaGuid $metaPath

    if (![string]::IsNullOrWhiteSpace($existingGuid)) {
        $Guid = $existingGuid
    }

    if ([string]::IsNullOrWhiteSpace($Guid)) {
        $Guid = ([guid]::NewGuid().ToString("N"))
    }

    @"
fileFormatVersion: 2
guid: $Guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $metaPath -Encoding UTF8

    return $Guid
}

function Write-FolderMeta {
    param([string]$FolderPath)

    $metaPath = "$FolderPath.meta"

    if (Test-Path -LiteralPath $metaPath) {
        return
    }

    $guid = [guid]::NewGuid().ToString("N")
    @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $metaPath -Encoding UTF8
}

function Write-DefaultMeta {
    param([string]$AssetPath)

    $metaPath = "$AssetPath.meta"

    if (Test-Path -LiteralPath $metaPath) {
        return
    }

    $guid = [guid]::NewGuid().ToString("N")
    @"
fileFormatVersion: 2
guid: $guid
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $metaPath -Encoding UTF8
}

Write-FolderMeta (Resolve-Path $OutputDirectory).Path

$metaLines = @()
Get-ChildItem -LiteralPath $OutputDirectory -Filter "headturn_*.png" | Sort-Object Name | ForEach-Object {
    $guid = Write-TextureMeta $_.FullName ""
    $metaLines += "$($_.Name) $guid"
}

$metaLines | Add-Content -LiteralPath $report -Encoding UTF8
Write-DefaultMeta $PreviewPath
Write-DefaultMeta $report

Write-Host "Normalized frames written to $OutputDirectory"
Write-Host "Preview written to $PreviewPath"
Write-Host "Report written to $report"
