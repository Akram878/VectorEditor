using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    public interface IShape
    {
        Shape ShapeElement { get; }
        void Draw(Point start, Point end);
        void SetFill(Brush fill);
        void SetStroke(Brush stroke, double thickness);
    }
}
