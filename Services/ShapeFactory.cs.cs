using System.Windows.Media;
using VectorEditor.Models;

namespace VectorEditor.Services
{
    public enum ShapeType
    {
        Rectangle,
        Ellipse,
        Line,
        Polygon  // Polygon (Pentagon in our project)
    }
    /// <summary>
    /// Factory responsible for creating shape objects based on the requested ShapeType.
    /// This prevents duplicated "new" calls across the project and keeps creation logic centralized.
    /// </summary>
    public static class ShapeFactory
    {
        public static IShape CreateShape(ShapeType type)
        {
            return type switch
            {
                ShapeType.Rectangle => new RectangleShape(),
                ShapeType.Ellipse => new EllipseShape(),
                ShapeType.Line => new LineShape(),
                ShapeType.Polygon => new PolygonShape(),
                _ => null
            };
        }
    }
}
