using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    /// <summary>
    /// Abstract base class for all drawable shapes in the editor.
    /// This class defines the common functionality shared by all shapes,
    /// such as storing the underlying WPF Shape element and applying
    /// fill & stroke properties.
    /// </summary>
    public abstract class ShapeBase : IShape
    {
        /// <summary>
        /// The actual WPF Shape object (Rectangle, Ellipse, Line, Polygon, etc.)
        /// that is placed and rendered on the Canvas.
        /// 
        /// Derived shape classes assign this property in their constructors.
        /// </summary>
        public Shape ShapeElement { get; protected set; }

        /// <summary>
        /// Draws the shape based on user drag input (start → end).
        /// Each specific shape (Rectangle, Ellipse, Line, Polygon) must provide
        /// its own implementation of this method, because the drawing logic
        /// is different for each type.
        /// </summary>
        /// <param name="start">The mouse-down position.</param>
        /// <param name="end">The mouse-up (or drag) position.</param>
        public abstract void Draw(Point start, Point end);

        /// <summary>
        /// Applies a fill brush to the shape. 
        /// Default implementation simply assigns the brush directly.
        /// Derived classes can override if needed.
        /// </summary>
        public virtual void SetFill(Brush fill)
        {
            ShapeElement.Fill = fill;
        }

        /// <summary>
        /// Applies stroke (outline) brush and stroke thickness to the shape.
        /// Default implementation directly assigns both properties.
        /// </summary>
        /// <param name="stroke">The outline color brush.</param>
        /// <param name="thickness">The outline thickness.</param>
        public virtual void SetStroke(Brush stroke, double thickness)
        {
            ShapeElement.Stroke = stroke;
            ShapeElement.StrokeThickness = thickness;
        }
    }
}
