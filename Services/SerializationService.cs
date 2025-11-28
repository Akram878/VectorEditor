using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;

// Alias to avoid ambiguity between System.IO.Path and System.Windows.Shapes.Path
using SystemPath = System.IO.Path;

namespace VectorEditor.Services
{
    /// <summary>
    /// Provides functionality to serialize and deserialize the canvas
    /// to/from JSON, and to save/load these JSON strings to disk.
    /// </summary>
    public static class SerializationService
    {
        /// <summary>
        /// Resolves the directory where project files (JSON/SVG) should be saved.
        /// We look for a "bin" folder upwards from the current base directory
        /// and create/use a "Saved" subfolder inside it.
        /// Fall back to the current base directory if "bin" cannot be found.
        /// </summary>
        private static string ResolveSavedDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            DirectoryInfo binDir = null;

            // Walk up the directory tree until we find a folder named "bin".
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

            // If binDir is null, we just use the current base directory.
            string baseForSaved = binDir?.FullName ?? dir.FullName;
            return SystemPath.Combine(baseForSaved, "Saved");
        }

        /// <summary>
        /// Normalizes numeric values for JSON so we never write NaN or Infinity,
        /// which are invalid in JSON. If NaN or Infinity appears, we use 0 instead.
        /// </summary>
        private static double Safe(double v) => (double.IsNaN(v) || double.IsInfinity(v)) ? 0.0 : v;

        /// <summary>
        /// Serializes all shapes on the canvas to a JSON string.
        /// Only known shape types (Rectangle, Ellipse, Line, Polygon) are supported.
        /// </summary>
        /// <param name="canvas">The canvas containing the shapes.</param>
        /// <returns>A JSON string that describes all shapes on the canvas.</returns>
        public static string Serialize(Canvas canvas)
        {
            var shapes = new List<object>();

            // Iterate through all children of the canvas and capture their properties.
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
                    // Store each vertex as X/Y pair
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

            // Configure JsonSerializer options:
            // - Pretty print (WriteIndented)
            // - Allow named floating point literals (for safety, though we sanitize with Safe())
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            return JsonSerializer.Serialize(shapes, options);
        }

        /// <summary>
        /// Clears the canvas and recreates all shapes from a JSON string
        /// previously produced by Serialize().
        /// </summary>
        /// <param name="canvas">The target canvas to populate.</param>
        /// <param name="json">The JSON string describing the shapes.</param>
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
                            s = r;
                            break;
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
                            s = el;
                            break;
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
                            s = l;
                            break;
                        }
                    case "Polygon":
                        {
                            var p = new Polygon();
                            var pts = item.GetProperty("Points").EnumerateArray()
                                .Select(pe => new System.Windows.Point(pe.GetProperty("X").GetDouble(), pe.GetProperty("Y").GetDouble()));

                            foreach (var pt in pts)
                            {
                                p.Points.Add(pt);
                            }

                            p.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Fill").GetString()));
                            p.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.GetProperty("Stroke").GetString()));
                            p.StrokeThickness = item.TryGetProperty("StrokeThickness", out var st) ? st.GetDouble() : 2;
                            s = p;
                            break;
                        }
                }

                if (s != null)
                {
                    canvas.Children.Add(s);
                }
            }
        }

        /// <summary>
        /// Writes the given content into a file with the specified name
        /// inside the resolved "Saved" directory.
        /// </summary>
        /// <param name="fileName">File name (e.g. "project.json").</param>
        /// <param name="content">The text content to save.</param>
        public static void SaveToFile(string fileName, string content)
        {
            string saveDir = ResolveSavedDir();
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            File.WriteAllText(SystemPath.Combine(saveDir, fileName), content);
        }

        /// <summary>
        /// Loads the content of the given file name from the "Saved" directory.
        /// Throws FileNotFoundException if the file does not exist.
        /// </summary>
        /// <param name="fileName">File name (e.g. "project.json").</param>
        /// <returns>The text content of the file.</returns>
        public static string LoadFromFile(string fileName)
        {
            string saveDir = ResolveSavedDir();
            string path = SystemPath.Combine(saveDir, fileName);

            return File.Exists(path)
                ? File.ReadAllText(path)
                : throw new FileNotFoundException(path);
        }

        /// <summary>
        /// Returns the default directory used for saving project files (JSON/SVG).
        /// This allows the UI layer to configure file dialogs to open in the same folder.
        /// </summary>
        public static string GetSavedDirectory()
        {
            return ResolveSavedDir();
        }

        /// <summary>
        /// Saves text content to an explicitly specified absolute path.
        /// Used together with SaveFileDialog when user chooses a custom file name/location.
        /// </summary>
        public static void SaveToAbsolutePath(string fullPath, string content)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("Path must not be empty.", nameof(fullPath));

            string dir = SystemPath.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, content);
        }

        /// <summary>
        /// Loads text content from an explicitly specified absolute path.
        /// Used together with OpenFileDialog when user selects an arbitrary file.
        /// </summary>
        public static string LoadFromAbsolutePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("Path must not be empty.", nameof(fullPath));

            if (!File.Exists(fullPath))
                throw new FileNotFoundException(fullPath);

            return File.ReadAllText(fullPath);
        }
    }
}
