using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    public abstract class ShapeBase : IShape
    {
        public Shape ShapeElement { get; protected set; }

        public abstract void Draw(Point start, Point end);

        public virtual void SetFill(Brush fill)
        {
            ShapeElement.Fill = fill;
        }

        public virtual void SetStroke(Brush stroke, double thickness)
        {
            ShapeElement.Stroke = stroke;
            ShapeElement.StrokeThickness = thickness;
        }
    }
}
