using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Services
{
    /// <summary>
    /// Provides functionality to export the current canvas into a valid SVG document.
    /// Each WPF shape is mapped to its SVG equivalent.
    /// </summary>
    public static class SvgService
    {
        /// <summary>
        /// Converts all shapes on the given canvas into an SVG-formatted string.
        /// Supports Rectangle, Ellipse, Line, and Polygon shapes.
        /// Handles fill/stroke colors including alpha transparency.
        /// </summary>
        /// <param name="canvas">The canvas containing drawable shapes.</param>
        /// <returns>Complete SVG XML as a string.</returns>
        public static string SerializeToSvg(Canvas canvas)
        {
            StringBuilder sb = new StringBuilder();

            // Open SVG root element with proper namespace.
            sb.AppendLine("<svg xmlns='http://www.w3.org/2000/svg' version='1.1'>");

            // Iterate over all shapes on the canvas and convert each into SVG markup.
            foreach (var child in canvas.Children)
            {
                // ============================
                //   RECTANGLE → <rect>
                // ============================
                if (child is Rectangle r)
                {
                    double x = Canvas.GetLeft(r);
                    double y = Canvas.GetTop(r);

                    // Convert brushes into (#RRGGBB, opacity)
                    var (fillHex, fillOpacity) = BrushToSvg(r.Fill);
                    var (strokeHex, strokeOpacity) = BrushToSvg(r.Stroke);

                    sb.Append("<rect ");
                    sb.Append($"x='{x}' y='{y}' width='{r.Width}' height='{r.Height}' ");
                    sb.Append($"fill='{fillHex}' ");
                    if (fillHex != "none") sb.Append($"fill-opacity='{fillOpacity:F3}' ");
                    sb.Append($"stroke='{strokeHex}' ");
                    if (strokeHex != "none") sb.Append($"stroke-opacity='{strokeOpacity:F3}' ");
                    sb.Append($"stroke-width='{Safe(r.StrokeThickness)}' ");
                    sb.AppendLine("/>");
                }

                // ============================
                //   ELLIPSE → <ellipse>
                // ============================
                else if (child is Ellipse el)
                {
                    double x = Canvas.GetLeft(el);
                    double y = Canvas.GetTop(el);

                    // SVG ellipses use center (cx, cy) and radii (rx, ry)
                    double cx = x + el.Width / 2.0;
                    double cy = y + el.Height / 2.0;
                    double rx = el.Width / 2.0;
                    double ry = el.Height / 2.0;

                    var (fillHex, fillOpacity) = BrushToSvg(el.Fill);
                    var (strokeHex, strokeOpacity) = BrushToSvg(el.Stroke);

                    sb.Append("<ellipse ");
                    sb.Append($"cx='{cx}' cy='{cy}' rx='{rx}' ry='{ry}' ");
                    sb.Append($"fill='{fillHex}' ");
                    if (fillHex != "none") sb.Append($"fill-opacity='{fillOpacity:F3}' ");
                    sb.Append($"stroke='{strokeHex}' ");
                    if (strokeHex != "none") sb.Append($"stroke-opacity='{strokeOpacity:F3}' ");
                    sb.Append($"stroke-width='{Safe(el.StrokeThickness)}' ");
                    sb.AppendLine("/>");
                }

                // ============================
                //   LINE → <line>
                // ============================
                else if (child is Line l)
                {
                    var (strokeHex, strokeOpacity) = BrushToSvg(l.Stroke);

                    sb.Append("<line ");
                    sb.Append($"x1='{l.X1}' y1='{l.Y1}' x2='{l.X2}' y2='{l.Y2}' ");
                    sb.Append($"stroke='{strokeHex}' ");
                    if (strokeHex != "none") sb.Append($"stroke-opacity='{strokeOpacity:F3}' ");
                    sb.Append($"stroke-width='{Safe(l.StrokeThickness)}' ");
                    sb.AppendLine("/>");
                }

                // ============================
                //   POLYGON → <polygon>
                // ============================
                else if (child is Polygon p)
                {
                    // Convert point collection into SVG "x,y x,y x,y" format
                    string pts = string.Join(" ", p.Points.Select(pt => $"{pt.X},{pt.Y}"));

                    var (fillHex, fillOpacity) = BrushToSvg(p.Fill);
                    var (strokeHex, strokeOpacity) = BrushToSvg(p.Stroke);

                    sb.Append("<polygon ");
                    sb.Append($"points='{pts}' ");
                    sb.Append($"fill='{fillHex}' ");
                    if (fillHex != "none") sb.Append($"fill-opacity='{fillOpacity:F3}' ");
                    sb.Append($"stroke='{strokeHex}' ");
                    if (strokeHex != "none") sb.Append($"stroke-opacity='{strokeOpacity:F3}' ");
                    sb.Append($"stroke-width='{Safe(p.StrokeThickness)}' ");
                    sb.AppendLine("/>");
                }
            }

            // Close SVG root tag
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// Converts a WPF Brush into SVG-friendly format:
        ///   - Returns (#RRGGBB, opacity)
        ///   - Supports SolidColorBrush including alpha transparency
        ///   - Transparent brushes return ("none", 0)
        /// </summary>
        /// <param name="brush">The WPF brush to convert.</param>
        /// <returns>Tuple (hexColor, opacityValue)</returns>
        private static (string hex, double opacity) BrushToSvg(Brush brush)
        {
            // Only SolidColorBrush is supported for export
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;

                // Fully transparent → treat as "none" in SVG
                if (c.A == 0)
                    return ("none", 0.0);

                // Convert ARGB to SVG 24-bit RGB (#RRGGBB)
                string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

                // Alpha transparency converted to SVG opacity (0–1 range)
                double opacity = Math.Round(c.A / 255.0, 3);

                return (hex, opacity);
            }

            // Unsupported brush types → no color
            return ("none", 0.0);
        }

        /// <summary>
        /// Ensures numeric values written into SVG are valid.
        /// Prevents NaN or Infinity which break SVG files.
        /// </summary>
        private static double Safe(double v) =>
            (double.IsNaN(v) || double.IsInfinity(v)) ? 1.0 : v;
    }
}
