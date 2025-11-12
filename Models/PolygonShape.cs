using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Represents a drawable polygon shape.
    /// In this editor the polygon is generated as a regular pentagon (5 sides)
    /// whose size and position are determined by the user's drag selection.
    /// </summary>
    public class PolygonShape : ShapeBase
    {
        /// <summary>
        /// Strongly-typed access to the underlying WPF Polygon element.
        /// ShapeElement (inherited from ShapeBase) is stored as a generic Shape,
        /// so this property safely casts it to Polygon.
        /// </summary>
        private Polygon _polygon => (Polygon)ShapeElement;

        /// <summary>
        /// Constructor: creates the underlying WPF Polygon object.
        /// </summary>
        public PolygonShape()
        {
            ShapeElement = new Polygon();
        }

        /// <summary>
        /// Draws the polygon based on the drag rectangle defined by (start → end).
        /// This implementation generates a regular 5-sided polygon (pentagon).
        /// The pentagon is inscribed inside the bounding box formed by start/end.
        /// </summary>
        /// <param name="start">Mouse press position.</param>
        /// <param name="end">Mouse drag release position.</param>
        public override void Draw(Point start, Point end)
        {
            // Compute bounding rectangle
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);
            double w = Math.Abs(end.X - start.X);
            double h = Math.Abs(end.Y - start.Y);

            // Compute center and radii for the polygon
            double cx = x + w / 2.0;      // center X
            double cy = y + h / 2.0;      // center Y
            double rx = w / 2.0;          // horizontal radius
            double ry = h / 2.0;          // vertical radius

            // Number of sides — currently 5 → regular pentagon
            int N = 5;

            // Build the point collection for the polygon
            PointCollection pts = new PointCollection();

            // Compute each vertex using polar coordinates:
            // angle = -90 degrees + i * (360° / N)
            // This orients the first vertex to the top.
            for (int i = 0; i < N; i++)
            {
                double ang = -Math.PI / 2 + i * (2 * Math.PI / N);
                pts.Add(new Point(
                    cx + rx * Math.Cos(ang),   // vertex X
                    cy + ry * Math.Sin(ang)    // vertex Y
                ));
            }

            // Assign the computed vertices to the WPF polygon
            _polygon.Points = pts;
        }
    }
}
