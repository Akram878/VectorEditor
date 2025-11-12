using System.Windows.Media;
using VectorEditor.Models;

namespace VectorEditor.Services
{
    public enum ShapeType
    {
        Rectangle,
        Ellipse,
        Line,
        Polygon
    }

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
