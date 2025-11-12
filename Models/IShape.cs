using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Interface representing a drawable shape in the vector editor.
    /// All shapes (Rectangle, Ellipse, Line, Polygon, etc.) implement this contract,
    /// ensuring that the editor can manipulate them in a uniform way regardless
    /// of their concrete type.
    /// </summary>
    public interface IShape
    {
        /// <summary>
        /// The underlying WPF Shape element that is placed on the Canvas.
        /// This is what actually gets rendered on-screen.
        /// </summary>
        Shape ShapeElement { get; }

        /// <summary>
        /// Draws the shape based on two points: the mouse-down (start)
        /// and mouse-drag/release (end) coordinates.
        /// Each shape implements its own drawing logic:
        /// - Rectangle/Ellipse: compute bounding box
        /// - Line: set X1,Y1,X2,Y2
        /// - Polygon: generate vertices
        /// </summary>
        void Draw(Point start, Point end);

        /// <summary>
        /// Applies a fill brush (interior color) to the shape.
        /// Some shapes may ignore fill (e.g., lines), but must still
        /// implement this method for API consistency.
        /// </summary>
        void SetFill(Brush fill);

        /// <summary>
        /// Applies the outline (stroke) color and thickness to the shape.
        /// Every shape must support stroke operations, even if some shapes
        /// treat stroke differently (e.g., line stroke is the line's width).
        /// </summary>
        void SetStroke(Brush stroke, double thickness);
    }
}
