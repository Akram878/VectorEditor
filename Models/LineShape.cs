using System.Windows;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    public class LineShape : ShapeBase
    {
        private Line _line => (Line)ShapeElement;

        public LineShape()
        {
            ShapeElement = new Line();
        }

        public override void Draw(Point start, Point end)
        {
            _line.X1 = start.X;
            _line.Y1 = start.Y;
            _line.X2 = end.X;
            _line.Y2 = end.Y;
        }
    }
}
