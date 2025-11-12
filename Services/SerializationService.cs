using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;

// 👇 هذا السطر الجديد يحل الالتباس تمامًا
using SystemPath = System.IO.Path;

namespace VectorEditor.Services
{
    public static class SerializationService
    {
        private static string ResolveSavedDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            DirectoryInfo binDir = null;

            // نصعد حتى نجد مجلد bin
            var cur = dir;
            while (cur != null)
            {
                if (string.Equals(cur.Name, "bin", StringComparison.OrdinalIgnoreCase))
                {
                    binDir = cur;
                    break;
                }
                cur = cur.Parent;
            }

            string baseForSaved = binDir?.FullName ?? dir.FullName;
            return SystemPath.Combine(baseForSaved, "Saved");
        }

        private static double Safe(double v) => (double.IsNaN(v) || double.IsInfinity(v)) ? 0.0 : v;

        public static string Serialize(Canvas canvas)
        {
            var shapes = new List<object>();

            foreach (var child in canvas.Children)
            {
                if (child is Rectangle r)
                {
                    shapes.Add(new
                    {
                        Type = "Rectangle",
                        Left = Safe(Canvas.GetLeft(r)),
                        Top = Safe(Canvas.GetTop(r)),
                        Width = Safe(r.Width),
                        Height = Safe(r.Height),
                        Fill = (r.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent",
                        Stroke = (r.Stroke as SolidColorBrush)?.Color.ToString() ?? "#000000",
                        StrokeThickness = r.StrokeThickness
                    });
                }
                else if (child is Ellipse el)
                {
                    shapes.Add(new
                    {
                        Type = "Ellipse",
                        Left = Safe(Canvas.GetLeft(el)),
                        Top = Safe(Canvas.GetTop(el)),
                        Width = Safe(el.Width),
                        Height = Safe(el.Height),
                        Fill = (el.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent",
                        Stroke = (el.Stroke as SolidColorBrush)?.Color.ToString() ?? "#000000",
                        StrokeThickness = el.StrokeThickness
                    });
                }
                else if (child is Line l)
                {
                    shapes.Add(new
                    {
                        Type = "Line",
                        X1 = Safe(l.X1),
                        Y1 = Safe(l.Y1),
                        X2 = Safe(l.X2),
                        Y2 = Safe(l.Y2),
                        Stroke = (l.Stroke as SolidColorBrush)?.Color.ToString() ?? "#000000",
                        StrokeThickness = l.StrokeThickness
                    });
                }
                else if (child is Polygon p)
                {
                    var pts = p.Points.Select(pt => new { X = Safe(pt.X), Y = Safe(pt.Y) }).ToList();
                    shapes.Add(new
                    {
                        Type = "Polygon",
                        Points = pts,
                        Fill = (p.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent",
                        Stroke = (p.Stroke as SolidColorBrush)?.Color.ToString() ?? "#000000",
                        StrokeThickness = p.StrokeThickness
                    });
                }
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            return JsonSerializer.Serialize(shapes, options);
        }

        public static void Deserialize(Canvas canvas, string json)
        {
            canvas.Children.Clear();
            var doc = JsonSerializer.Deserialize<List<JsonElement>>(json);

            foreach (var item in doc)
            {
                string type = item.GetProperty("Type").GetString();
                Shape s = null;

                switch (type)
                {
                    case "Rectangle":
                        {
                            var r = new Rectangle();
                            r.Width = item.GetProperty("Width").GetDouble();
                            r.Height = item.GetProperty("Height").GetDouble();
                            r.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Fill").GetString()));
                            r.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Stroke").GetString()));
                            r.StrokeThickness = item.TryGetProperty("StrokeThickness", out var st) ? st.GetDouble() : 2;
                            Canvas.SetLeft(r, item.GetProperty("Left").GetDouble());
                            Canvas.SetTop(r, item.GetProperty("Top").GetDouble());
                            s = r; break;
                        }
                    case "Ellipse":
                        {
                            var el = new Ellipse();
                            el.Width = item.GetProperty("Width").GetDouble();
                            el.Height = item.GetProperty("Height").GetDouble();
                            el.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Fill").GetString()));
                            el.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Stroke").GetString()));
                            el.StrokeThickness = item.TryGetProperty("StrokeThickness", out var st) ? st.GetDouble() : 2;
                            Canvas.SetLeft(el, item.GetProperty("Left").GetDouble());
                            Canvas.SetTop(el, item.GetProperty("Top").GetDouble());
                            s = el; break;
                        }
                    case "Line":
                        {
                            var l = new Line();
                            l.X1 = item.GetProperty("X1").GetDouble();
                            l.Y1 = item.GetProperty("Y1").GetDouble();
                            l.X2 = item.GetProperty("X2").GetDouble();
                            l.Y2 = item.GetProperty("Y2").GetDouble();
                            l.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Stroke").GetString()));
                            l.StrokeThickness = item.TryGetProperty("StrokeThickness", out var st) ? st.GetDouble() : 2;
                            s = l; break;
                        }
                    case "Polygon":
                        {
                            var p = new Polygon();
                            var pts = item.GetProperty("Points").EnumerateArray()
                                .Select(pe => new System.Windows.Point(pe.GetProperty("X").GetDouble(), pe.GetProperty("Y").GetDouble()));
                            foreach (var pt in pts) p.Points.Add(pt);
                            p.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Fill").GetString()));
                            p.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Stroke").GetString()));
                            p.StrokeThickness = item.TryGetProperty("StrokeThickness", out var st) ? st.GetDouble() : 2;
                            s = p; break;
                        }
                }

                if (s != null) canvas.Children.Add(s);
            }
        }

        public static void SaveToFile(string fileName, string content)
        {
            string saveDir = ResolveSavedDir();
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
            File.WriteAllText(SystemPath.Combine(saveDir, fileName), content);
        }

        public static string LoadFromFile(string fileName)
        {
            string saveDir = ResolveSavedDir();
            string path = SystemPath.Combine(saveDir, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : throw new FileNotFoundException(path);
        }
    }
}
