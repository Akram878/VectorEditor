using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Services
{
    public static class SvgService
    {
        public static string SerializeToSvg(Canvas canvas)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<svg xmlns='http://www.w3.org/2000/svg' version='1.1'>");

            foreach (var child in canvas.Children)
            {
                if (child is Rectangle r)
                {
                    double x = Canvas.GetLeft(r);
                    double y = Canvas.GetTop(r);

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
                else if (child is Ellipse el)
                {
                    double x = Canvas.GetLeft(el);
                    double y = Canvas.GetTop(el);
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
                else if (child is Polygon p)
                {
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

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        // يحوّل Brush إلى (لون SVG بدون ألفا, شفافية)
        // SolidColorBrush #AARRGGBB -> (#RRGGBB, A/255). Transparent => ("none", 0)
        private static (string hex, double opacity) BrushToSvg(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                // إن كانت ألفا = 0 نعتبره none
                if (c.A == 0) return ("none", 0.0);

                string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                double opacity = Math.Round(c.A / 255.0, 3);
                return (hex, opacity);
            }
            return ("none", 0.0);
        }

        private static double Safe(double v) => (double.IsNaN(v) || double.IsInfinity(v)) ? 1.0 : v;
    }
}
