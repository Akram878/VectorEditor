using System.Windows;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Represents a drawable line segment in the vector editor.
    /// Wraps a WPF LineShape and provides drawing behavior based on
    /// the user's drag gesture (start → end).
    /// </summary>
    public class LineShape : ShapeBase
    {
        /// <summary>
        /// Strongly-typed access to the underlying WPF Line element.
        /// ShapeElement is declared in ShapeBase (as a generic Shape),
        /// so we cast it here for convenience.
        /// </summary>
        private Line _line => (Line)ShapeElement;

        /// <summary>
        /// Constructor: initializes the underlying WPF Line.
        /// </summary>
        public LineShape()
        {
            ShapeElement = new Line();
        }

        /// <summary>
        /// Draws the line by assigning its start (X1,Y1) and end (X2,Y2)
        /// coordinates directly. Unlike rectangles or ellipses, a line does
        /// not use Canvas.Left/Top or Width/Height — only endpoint positioning.
        /// </summary>
        /// <param name="start">Mouse-down position (first endpoint).</param>
        /// <param name="end">Mouse-drag/release position (second endpoint).</param>
        public override void Draw(Point start, Point end)
        {
            _line.X1 = start.X;
            _line.Y1 = start.Y;
            _line.X2 = end.X;
            _line.Y2 = end.Y;
        }
    }
}
