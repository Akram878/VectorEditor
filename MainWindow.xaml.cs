using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VectorEditor.Models;
using VectorEditor.Services;

namespace VectorEditor
{
    public partial class MainWindow : Window
    {
        private ShapeType _currentTool = ShapeType.Rectangle;
        private bool _selectMode = false; //  وضع التحديد

        private IShape _currentShape;
        private Shape _selectedShape;

        private Point _startPoint;
        private bool _isDrawing;
        private bool _isDragging;
        private bool _isResizing;
        private Point _lastMousePos;

        private Ellipse _resizeHandle;
        private double _canvasScale = 1.0;

        private readonly UndoService _undo = new UndoService(maxSteps: 5);

        // إدخال مُعتمد للخصائص
        private bool _internalSet = false;
        private TextBox _activeBox = null;
        private string _originalText = null;

        public MainWindow()
        {
            InitializeComponent();
            SaveState(force: true); // أول حالة فارغة
        }

        // ===== الأدوات =====
        private void BtnSelect_Click(object sender, RoutedEventArgs e) { _selectMode = true; Deselect(); }
        private void BtnRect_Click(object sender, RoutedEventArgs e) { _selectMode = false; _currentTool = ShapeType.Rectangle; Deselect(); }
        private void BtnEllipse_Click(object sender, RoutedEventArgs e) { _selectMode = false; _currentTool = ShapeType.Ellipse; Deselect(); }
        private void BtnLine_Click(object sender, RoutedEventArgs e) { _selectMode = false; _currentTool = ShapeType.Line; Deselect(); }
        private void BtnPolygon_Click(object sender, RoutedEventArgs e) { _selectMode = false; _currentTool = ShapeType.Polygon; Deselect(); }

        // ===== ماوس على اللوحة =====
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(MainCanvas);

            // إذا نقرنا على شكل: تحديد وبداية سحب (حتى في وضع التحديد)
            if (e.OriginalSource is Shape s && MainCanvas.Children.Contains(s))
            {
                Select(s);
                _isDragging = true;
                _lastMousePos = _startPoint;
                Mouse.Capture(MainCanvas);
                return;
            }

            // إذا كنا في وضع التحديد ونقرنا على فراغ: لا نرسم شيئًا
            if (_selectMode)
            {
                Deselect();
                return;
            }

            // خلاف ذلك: رسم شكل جديد
            _isDrawing = true;
            _currentShape = ShapeFactory.CreateShape(_currentTool);
            if (_currentShape == null) return;

            Color fillColor = Colors.LightBlue, strokeColor = Colors.Black;
            try { if (!string.IsNullOrWhiteSpace(FillColorBox.Text)) fillColor = (Color)ColorConverter.ConvertFromString(FillColorBox.Text); } catch { }
            try { if (!string.IsNullOrWhiteSpace(StrokeColorBox.Text)) strokeColor = (Color)ColorConverter.ConvertFromString(StrokeColorBox.Text); } catch { }

            _currentShape.SetFill(new SolidColorBrush(fillColor));
            _currentShape.SetStroke(new SolidColorBrush(strokeColor), 2);
            MainCanvas.Children.Add(_currentShape.ShapeElement);
            Mouse.Capture(MainCanvas);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MainCanvas);

            if (_isDrawing && _currentShape != null)
            {
                _currentShape.Draw(_startPoint, pos);
                return;
            }

            if (_isDragging && _selectedShape != null && !_isResizing)
            {
                double dx = pos.X - _lastMousePos.X;
                double dy = pos.Y - _lastMousePos.Y;
                MoveShape(_selectedShape, dx, dy);
                _lastMousePos = pos;
                UpdatePropBoxes();
                UpdateHandle();
            }

            if (_isResizing && _selectedShape != null)
            {
                double dx = pos.X - _lastMousePos.X;
                double dy = pos.Y - _lastMousePos.Y;
                ResizeShapeBy(_selectedShape, dx, dy);
                _lastMousePos = pos;
                UpdatePropBoxes();
                UpdateHandle();
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing || _isDragging || _isResizing)
            {
                _isDrawing = _isDragging = _isResizing = false;
                Mouse.Capture(null);
                SaveState(); // ✔️ نحفظ خطوة الإنشاء/التحريك/التحجيم عند الانتهاء فقط
            }
        }

        // ===== حركة وتحجيم =====
        private void MoveShape(Shape shape, double dx, double dy)
        {
            if (shape is Line ln)
            {
                ln.X1 += dx; ln.Y1 += dy;
                ln.X2 += dx; ln.Y2 += dy;
            }
            else if (shape is Polygon pg)
            {
                for (int i = 0; i < pg.Points.Count; i++)
                    pg.Points[i] = new Point(pg.Points[i].X + dx, pg.Points[i].Y + dy);
            }
            else
            {
                double left = Canvas.GetLeft(shape); if (double.IsNaN(left)) left = 0;
                double top = Canvas.GetTop(shape); if (double.IsNaN(top)) top = 0;
                Canvas.SetLeft(shape, left + dx);
                Canvas.SetTop(shape, top + dy);
            }
        }

        private void ResizeShapeBy(Shape shape, double dx, double dy)
        {
            if (shape is Rectangle or Ellipse)
            {
                shape.Width = Math.Max(5, shape.Width + dx);
                shape.Height = Math.Max(5, shape.Height + dy);
            }
            else if (shape is Line ln)
            {
                ln.X2 += dx;
                ln.Y2 += dy;
            }
            else if (shape is Polygon pg)
            {
                var b = GetBounds(pg);
                if (b.Width < 1 || b.Height < 1) return;
                double newW = Math.Max(5, b.Width + dx);
                double newH = Math.Max(5, b.Height + dy);
                ScalePolygonTo(pg, b, newW, newH);
            }
        }

        // ===== تحديد =====
        private void Select(Shape s)
        {
            Deselect();
            _selectedShape = s;
            AddResizeHandle();
            UpdatePropBoxes();
        }

        private void Deselect()
        {
            _selectedShape = null;
            if (_resizeHandle != null)
            {
                MainCanvas.Children.Remove(_resizeHandle);
                _resizeHandle = null;
            }
        }

        private void AddResizeHandle()
        {
            if (_selectedShape == null) return;

            _resizeHandle = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                Cursor = Cursors.SizeNWSE,
                IsHitTestVisible = true
            };

            _resizeHandle.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _isResizing = true;
                _lastMousePos = e.GetPosition(MainCanvas);
                Mouse.Capture(MainCanvas);
            };

            UpdateHandle();
            MainCanvas.Children.Add(_resizeHandle);
        }

        private void UpdateHandle()
        {
            if (_resizeHandle == null || _selectedShape == null) return;
            Rect b = GetBounds(_selectedShape);
            Canvas.SetLeft(_resizeHandle, b.Right - _resizeHandle.Width / 2);
            Canvas.SetTop(_resizeHandle, b.Bottom - _resizeHandle.Height / 2);
        }

        // ===== خصائص (إدخال مُعتمد فقط) =====
        private void UpdatePropBoxes()
        {
            if (_selectedShape == null) return;

            _internalSet = true;

            Rect b = GetBounds(_selectedShape);
            XBox.Text = Math.Round(b.X).ToString();
            YBox.Text = Math.Round(b.Y).ToString();
            WidthBox.Text = Math.Round(b.Width).ToString();
            HeightBox.Text = Math.Round(b.Height).ToString();

            FillColorBox.Text = (_selectedShape.Fill as SolidColorBrush)?.Color.ToString() ?? "Transparent";
            StrokeColorBox.Text = (_selectedShape.Stroke as SolidColorBrush)?.Color.ToString() ?? "#000000";

            _internalSet = false;
        }

        private void PropBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_internalSet) return;
            _activeBox = sender as TextBox;
            _originalText = _activeBox?.Text;
            _activeBox?.SelectAll();
            _activeBox.Text = string.Empty;
        }

        private void PropBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitProp(sender as TextBox);
                this.Focus(); // خروج من الصندوق
                e.Handled = true;
            }
        }

        private void PropBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitProp(sender as TextBox);
        }

        private void CommitProp(TextBox tb)
        {
            if (_internalSet || tb == null) return;

            // إن تركها فارغة → أعِد النص الأصلي بلا حفظ
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                _internalSet = true;
                tb.Text = _originalText ?? tb.Text;
                _internalSet = false;
                _activeBox = null; _originalText = null;
                return;
            }

            try
            {
                if (_selectedShape == null) return;

                Rect b = GetBounds(_selectedShape);

                if (tb == XBox && double.TryParse(tb.Text, out double nx))
                    MoveShape(_selectedShape, nx - b.X, 0);
                else if (tb == YBox && double.TryParse(tb.Text, out double ny))
                    MoveShape(_selectedShape, 0, ny - b.Y);
                else if (tb == WidthBox && double.TryParse(tb.Text, out double w))
                    ApplySizeToShape(_selectedShape, b, Math.Max(5, w), b.Height);
                else if (tb == HeightBox && double.TryParse(tb.Text, out double h))
                    ApplySizeToShape(_selectedShape, b, b.Width, Math.Max(5, h));
                else if (tb == FillColorBox)
                {
                    Color c = Colors.LightBlue;
                    try { c = (Color)ColorConverter.ConvertFromString(tb.Text); } catch { }
                    _selectedShape.Fill = new SolidColorBrush(c);
                }
                else if (tb == StrokeColorBox)
                {
                    Color c = Colors.Black;
                    try { c = (Color)ColorConverter.ConvertFromString(tb.Text); } catch { }
                    _selectedShape.Stroke = new SolidColorBrush(c);
                }

                UpdateHandle();
                UpdatePropBoxes();
                SaveState(); // ✔️ حفظ خطوة واحدة مُعتمدة فقط
            }
            finally
            {
                _activeBox = null; _originalText = null;
            }
        }

        private void ApplySizeToShape(Shape s, Rect currentBounds, double targetW, double targetH)
        {
            if (s is Rectangle or Ellipse)
            {
                double left = Canvas.GetLeft(s); if (double.IsNaN(left)) left = currentBounds.X;
                double top = Canvas.GetTop(s); if (double.IsNaN(top)) top = currentBounds.Y;
                s.Width = targetW;
                s.Height = targetH;
                Canvas.SetLeft(s, left);
                Canvas.SetTop(s, top);
            }
            else if (s is Line ln)
            {
                ln.X2 = ln.X1 + targetW * Math.Sign((ln.X2 - ln.X1) == 0 ? 1 : (ln.X2 - ln.X1));
                ln.Y2 = ln.Y1 + targetH * Math.Sign((ln.Y2 - ln.Y1) == 0 ? 1 : (ln.Y2 - ln.Y1));
            }
            else if (s is Polygon pg)
            {
                ScalePolygonTo(pg, currentBounds, targetW, targetH);
            }
        }

        private static Rect GetBounds(Shape shape)
        {
            if (shape is Rectangle or Ellipse)
            {
                double left = Canvas.GetLeft(shape); if (double.IsNaN(left)) left = 0;
                double top = Canvas.GetTop(shape); if (double.IsNaN(top)) top = 0;
                double w = double.IsNaN(shape.Width) ? 0 : shape.Width;
                double h = double.IsNaN(shape.Height) ? 0 : shape.Height;
                return new Rect(left, top, w, h);
            }
            if (shape is Line ln)
            {
                double minX = Math.Min(ln.X1, ln.X2);
                double minY = Math.Min(ln.Y1, ln.Y2);
                double w = Math.Abs(ln.X2 - ln.X1);
                double h = Math.Abs(ln.Y2 - ln.Y1);
                return new Rect(minX, minY, w, h);
            }
            if (shape is Polygon pg && pg.Points.Count > 0)
            {
                double minX = pg.Points.Min(p => p.X);
                double maxX = pg.Points.Max(p => p.X);
                double minY = pg.Points.Min(p => p.Y);
                double maxY = pg.Points.Max(p => p.Y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            return Rect.Empty;
        }

        private static void ScalePolygonTo(Polygon pg, Rect current, double targetW, double targetH)
        {
            if (current.Width <= 0 || current.Height <= 0) return;

            double sx = targetW / current.Width;
            double sy = targetH / current.Height;
            double ox = current.X;
            double oy = current.Y;

            for (int i = 0; i < pg.Points.Count; i++)
            {
                var p = pg.Points[i];
                double nx = ox + (p.X - ox) * sx;
                double ny = oy + (p.Y - oy) * sy;
                pg.Points[i] = new Point(nx, ny);
            }
        }

        // ===== تكبير / تصغير =====
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _canvasScale += 0.1;
            MainCanvas.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _canvasScale = Math.Max(0.2, _canvasScale - 0.1);
            MainCanvas.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
        }

        // ===== حذف / تراجع / حفظ-تحميل =====
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShape != null)
            {
                MainCanvas.Children.Remove(_selectedShape);
                Deselect();
                SaveState(); // ✔️ الحذف تعديل حقيقي
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var prev = _undo.Undo();
            if (prev != null)
            {
                SerializationService.Deserialize(MainCanvas, prev);
                Deselect(); // التحديد ليس تغييرًا
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string json = SerializationService.Serialize(MainCanvas);
            SerializationService.SaveToFile("project.json", json);
            MessageBox.Show("تم الحفظ في VectorEditor\\bin\\Saved (JSON).");
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = SerializationService.LoadFromFile("project.json");
                SerializationService.Deserialize(MainCanvas, json);
                Deselect();
                _undo.ResetWith(json);
                MessageBox.Show("تم التحميل بنجاح.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في التحميل: " + ex.Message);
            }
        }

        private void BtnSaveSvg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string svg = SvgService.SerializeToSvg(MainCanvas);
                SerializationService.SaveToFile("project.svg", svg);
                MessageBox.Show("تم الحفظ في VectorEditor\\bin\\Saved (SVG).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في حفظ SVG: " + ex.Message);
            }
        }

        private void SaveState(bool force = false)
        {
            string state = SerializationService.Serialize(MainCanvas);
            _undo.Save(state, force);
        }
    }
}
