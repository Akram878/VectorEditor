using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Represents a drawable rectangular shape in the vector editor.
    /// The actual visual element is a WPF Rectangle, stored in ShapeElement
    /// (inherited from ShapeBase).
    /// </summary>
    public class RectangleShape : ShapeBase
    {
        /// <summary>
        /// Constructor: initializes the underlying WPF Rectangle object.
        /// </summary>
        public RectangleShape()
        {
            // ShapeElement is defined in ShapeBase.
            // Here we assign a concrete Rectangle instance.
            ShapeElement = new Rectangle();
        }

        /// <summary>
        /// Draws the rectangle according to the drag gesture defined by
        /// (start → end) mouse positions. The rectangle is drawn as the
        /// bounding box between these two points regardless of drag direction.
        /// </summary>
        /// <param name="start">Mouse press point.</param>
        /// <param name="end">Mouse release/drag point.</param>
        public override void Draw(Point start, Point end)
        {
            // Determine the top-left corner of the rectangle.
            // If the user drags left/up, Min() ensures the rectangle is positioned correctly.
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);

            // Compute width/height as the absolute distance between points.
            double w = Math.Abs(start.X - end.X);
            double h = Math.Abs(start.Y - end.Y);

            // Apply position to the Canvas using attached properties.
            Canvas.SetLeft(ShapeElement, x);
            Canvas.SetTop(ShapeElement, y);

            // Apply size to the underlying Rectangle.
            ShapeElement.Width = w;
            ShapeElement.Height = h;
        }
    }
}
