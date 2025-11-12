using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    public class RectangleShape : ShapeBase
    {
        public RectangleShape()
        {
            ShapeElement = new Rectangle();
        }

        public override void Draw(Point start, Point end)
        {
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);
            double w = Math.Abs(start.X - end.X);
            double h = Math.Abs(start.Y - end.Y);

            Canvas.SetLeft(ShapeElement, x);
            Canvas.SetTop(ShapeElement, y);
            ShapeElement.Width = w;
            ShapeElement.Height = h;
        }
    }
}
