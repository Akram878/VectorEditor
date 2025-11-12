using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor.Models
{
    public class PolygonShape : ShapeBase
    {
        private Polygon _polygon => (Polygon)ShapeElement;

        public PolygonShape()
        {
            ShapeElement = new Polygon();
        }

        public override void Draw(Point start, Point end)
        {
            // نرسم مخمّسًا منتظمًا داخل مستطيل السحب (start..end)
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);
            double w = Math.Abs(end.X - start.X);
            double h = Math.Abs(end.Y - start.Y);

            // مركز و"نصف قطر" تقريبيين يناسبان الصندوق
            double cx = x + w / 2.0;
            double cy = y + h / 2.0;
            double rx = w / 2.0;
            double ry = h / 2.0;

            // مخمّس: 5 رؤوس، نبدأ من الأعلى (زاوية -90°)
            int N = 5;
            PointCollection pts = new PointCollection();
            for (int i = 0; i < N; i++)
            {
                double ang = -Math.PI / 2 + i * (2 * Math.PI / N);
                pts.Add(new Point(cx + rx * Math.Cos(ang), cy + ry * Math.Sin(ang)));
            }
            _polygon.Points = pts;
        }
    }
}
