using VectorEditor.Models;

namespace VectorEditor.Services
{
    /// <summary>
    /// Enumerates all logical shape tools supported by the editor.
    /// Note:
    /// - Rectangle / Ellipse / Line are created via ShapeFactory.
    /// - Polygon is a freehand multi-vertex polygon drawn directly in MainWindow
    ///   using a WPF Polygon, not via the IShape abstraction.
    /// </summary>
    public enum ShapeType
    {
        Rectangle,
        Ellipse,
        Line,
        Polygon // Freehand polygon tool (handled manually in MainWindow)
    }

    /// <summary>
    /// Factory responsible for creating IShape instances for tools that follow
    /// the IShape abstraction (Rectangle, Ellipse, Line only).
    /// 
    /// The Polygon tool is implemented manually in MainWindow, so it is not
    /// created via this factory.
    /// </summary>
    public static class ShapeFactory
    {
        /// <summary>
        /// Creates a concrete IShape implementation for the given shape type.
        /// Polygon is intentionally not created here.
        /// </summary>
        public static IShape CreateShape(ShapeType type)
        {
            return type switch
            {
                ShapeType.Rectangle => new RectangleShape(),
                ShapeType.Ellipse => new EllipseShape(),
                ShapeType.Line => new LineShape(),

                // Default for Polygon or any unsupported case
                _ => null
            };
        }
    }
}
