using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using CrosshairApp.Models;

namespace CrosshairApp.Services;

// Deliberately restricted offline SVG renderer. No scripts, external links, DTDs or web requests.
public static class AssetLoader
{
    public static ImageSource Load(Profile p)
    {
        var bytes = Convert.FromBase64String(p.Asset);
        if (bytes.Length > 2_000_000) throw new FormatException("Maximum asset size: 2 MB.");
        if (p.AssetType == "png")
        {
            using var stream = new MemoryStream(bytes);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapSource image = decoder.Frames[0];
            if (image.PixelWidth > 2048 || image.PixelHeight > 2048) throw new FormatException("Maximum PNG: 2048 × 2048.");
            if (image.Format != PixelFormats.Bgra32 && image.Format != PixelFormats.Pbgra32)
                image = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            int width = image.PixelWidth, height = image.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height]; image.CopyPixels(pixels, stride, 0);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[y * stride + x * 4 + 3] > 8)
                    { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            if (maxX < 0) throw new FormatException("PNG is fully transparent.");
            if (minX != 0 || minY != 0 || maxX != width - 1 || maxY != height - 1)
                image = new CroppedBitmap(image, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            image.Freeze(); return image;
        }
        if (p.AssetType != "svg") throw new FormatException("Only PNG / SVG supported.");
        using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
        var root = XDocument.Load(reader).Root ?? throw new FormatException("Invalid SVG.");
        if (root.Name.LocalName != "svg") throw new FormatException("SVG root required.");
        if (root.Descendants().Count() > 1000) throw new FormatException("SVG: maximum 1000 elements.");
        var group = new DrawingGroup();
        foreach (var el in root.DescendantsAndSelf())
        {
            foreach (var a in el.Attributes())
                if (a.Name.LocalName is "transform" or "style" or "href" or "filter" or "clip-path" or "mask" || a.Name.LocalName.StartsWith("on"))
                    throw new FormatException("SVG transforms/styles/references are unsupported. Flatten the SVG first.");
            string tag = el.Name.LocalName;
            if (tag is "svg" or "g" or "title" or "desc") continue;
            double N(string key, double fallback = 0) => el.Attribute(key) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : fallback;
            Geometry geo = tag switch
            {
                "path" => Geometry.Parse(el.Attribute("d")?.Value ?? ""),
                "circle" => new EllipseGeometry(new Point(N("cx"), N("cy")), N("r"), N("r")),
                "ellipse" => new EllipseGeometry(new Point(N("cx"), N("cy")), N("rx"), N("ry")),
                "rect" => new RectangleGeometry(new Rect(N("x"), N("y"), N("width"), N("height"))),
                "line" => new LineGeometry(new Point(N("x1"), N("y1")), new Point(N("x2"), N("y2"))),
                "polygon" or "polyline" => Geometry.Parse("M " + el.Attribute("points")?.Value + (tag == "polygon" ? " Z" : "")),
                _ => throw new FormatException($"Unsupported SVG element: {tag}")
            };
            string Attr(string key, string fallback) => el.AncestorsAndSelf().Select(e => e.Attribute(key)?.Value).FirstOrDefault(v => v != null) ?? fallback;
            Brush? B(string s) => s == "none" ? null : (Brush)new BrushConverter().ConvertFromInvariantString(s)!;
            var fill = B(Attr("fill", "#000000"));
            var stroke = B(Attr("stroke", "none"));
            double width = double.Parse(Attr("stroke-width", "1"), CultureInfo.InvariantCulture);
            if (!double.IsFinite(width) || width < 0 || width > 10000) throw new FormatException("Invalid SVG stroke width.");
            var drawing = new GeometryDrawing(tag == "line" ? null : fill, stroke == null ? null : new Pen(stroke, width), geo);
            group.Children.Add(drawing);
        }
        if (group.Children.Count == 0 || group.Bounds.IsEmpty || group.Bounds.Width <= 0 || group.Bounds.Height <= 0) throw new FormatException("SVG has no visible geometry.");
        var result = new DrawingImage(group); result.Freeze(); return result;
    }
}
