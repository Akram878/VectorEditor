using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Represents a drawable ellipse (or circle) in the vector editor.
    /// The actual WPF element is an Ellipse, stored in ShapeElement 
    /// (inherited from ShapeBase).
    /// </summary>
    public class EllipseShape : ShapeBase
    {
        /// <summary>
        /// Constructor: initializes the underlying WPF Ellipse object.
        /// </summary>
        public EllipseShape()
        {
            // Assign a new Ellipse to the base class ShapeElement.
            ShapeElement = new Ellipse();
        }

        /// <summary>
        /// Draws the ellipse using a bounding rectangle defined by the 
        /// start (mouse-down) and end (mouse-up/drag) points.
        /// No matter which direction the user drags, the ellipse is always
        /// drawn correctly by computing Min/Abs for coordinates.
        /// </summary>
        /// <param name="start">Mouse-down position.</param>
        /// <param name="end">Mouse-drag or mouse-up position.</param>
        public override void Draw(Point start, Point end)
        {
            // Determine top-left corner of the bounding rectangle.
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);

            // Compute width and height as absolute differences.
            double w = Math.Abs(start.X - end.X);
            double h = Math.Abs(start.Y - end.Y);

            // Apply position to the Canvas via attached properties.
            Canvas.SetLeft(ShapeElement, x);
            Canvas.SetTop(ShapeElement, y);

            // Apply size to the ellipse.
            ShapeElement.Width = w;
            ShapeElement.Height = h;
        }
    }
}
