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
    /// <summary>
    /// Main window for the vector editor application.
    /// Handles tool selection, drawing, selection, resizing,
    /// property editing, zooming, undo, and persistence (JSON/SVG).
    /// </summary>
    public partial class MainWindow : Window
    {
        // Current drawing tool (rectangle, ellipse, line, polygon).
        private ShapeType _currentTool = ShapeType.Rectangle;

        // Indicates whether the editor is in "selection mode" instead of "drawing mode".
        private bool _selectMode = false;

        // Shape currently being drawn (during mouse drag).
        private IShape _currentShape;

        // Shape currently selected on the canvas (for move/resize/edit).
        private Shape _selectedShape;

        // Mouse state tracking for drawing/moving/resizing.
        private Point _startPoint;
        private bool _isDrawing;
        private bool _isDragging;
        private bool _isResizing;
        private Point _lastMousePos;

        // Visual resize handle displayed at bottom-right of selected shape.
        private Ellipse _resizeHandle;

        // Current zoom factor applied to the canvas.
        private double _canvasScale = 1.0;

        // Undo service storing serialized canvas snapshots (up to 5 steps).
        private readonly UndoService _undo = new UndoService(maxSteps: 5);

        // Flags and fields to control property text box editing safely.
        private bool _internalSet = false;
        private TextBox _activeBox = null;
        private string _originalText = null;

        public MainWindow()
        {
            InitializeComponent();
            // Store the initial empty canvas state in the undo stack.
            SaveState(force: true);
        }

        // =======================
        //  Tool selection buttons
        // =======================

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            _selectMode = true;
            Deselect();
        }

        private void BtnRect_Click(object sender, RoutedEventArgs e)
        {
            _selectMode = false;
            _currentTool = ShapeType.Rectangle;
            Deselect();
        }

        private void BtnEllipse_Click(object sender, RoutedEventArgs e)
        {
            _selectMode = false;
            _currentTool = ShapeType.Ellipse;
            Deselect();
        }

        private void BtnLine_Click(object sender, RoutedEventArgs e)
        {
            _selectMode = false;
            _currentTool = ShapeType.Line;
            Deselect();
        }

        private void BtnPolygon_Click(object sender, RoutedEventArgs e)
        {
            _selectMode = false;
            _currentTool = ShapeType.Polygon;
            Deselect();
        }

        // =======================
        //  Canvas mouse handlers
        // =======================

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(MainCanvas);

            // If we clicked on an existing shape: select it and start dragging.
            if (e.OriginalSource is Shape s && MainCanvas.Children.Contains(s))
            {
                Select(s);
                _isDragging = true;
                _lastMousePos = _startPoint;
                Mouse.Capture(MainCanvas);
                return;
            }

            // In selection mode, clicking on empty space should clear selection
            // and must NOT create a new shape.
            if (_selectMode)
            {
                Deselect();
                return;
            }

            // Otherwise: start drawing a new shape.
            _isDrawing = true;
            _currentShape = ShapeFactory.CreateShape(_currentTool);
            if (_currentShape == null)
            {
                return;
            }

            // Determine initial fill and stroke colors from the property boxes if possible.
            Color fillColor = Colors.LightBlue;
            Color strokeColor = Colors.Black;

            try
            {
                if (!string.IsNullOrWhiteSpace(FillColorBox.Text))
                {
                    fillColor = (Color)ColorConverter.ConvertFromString(FillColorBox.Text);
                }
            }
            catch
            {
                // If parsing fails, keep default fillColor.
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(StrokeColorBox.Text))
                {
                    strokeColor = (Color)ColorConverter.ConvertFromString(StrokeColorBox.Text);
                }
            }
            catch
            {
                // If parsing fails, keep default strokeColor.
            }

            _currentShape.SetFill(new SolidColorBrush(fillColor));
            _currentShape.SetStroke(new SolidColorBrush(strokeColor), 2);
            MainCanvas.Children.Add(_currentShape.ShapeElement);
            Mouse.Capture(MainCanvas);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MainCanvas);

            // While drawing a new shape, update its geometry as the mouse moves.
            if (_isDrawing && _currentShape != null)
            {
                _currentShape.Draw(_startPoint, pos);
                return;
            }

            // While dragging an existing selection, move the shape.
            if (_isDragging && _selectedShape != null && !_isResizing)
            {
                double dx = pos.X - _lastMousePos.X;
                double dy = pos.Y - _lastMousePos.Y;
                MoveShape(_selectedShape, dx, dy);
                _lastMousePos = pos;
                UpdatePropBoxes();
                UpdateHandle();
            }

            // While resizing, update the shape size according to mouse delta.
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
            // Stop any active drawing/dragging/resizing operation.
            if (_isDrawing || _isDragging || _isResizing)
            {
                _isDrawing = false;
                _isDragging = false;
                _isResizing = false;
                Mouse.Capture(null);

                // Save a single undo state only when an operation is completed.
                SaveState();
            }
        }

        // =======================
        //  Moving and resizing
        // =======================

        /// <summary>
        /// Translates the given shape by (dx, dy).
        /// The logic differs slightly by shape type.
        /// </summary>
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
                {
                    pg.Points[i] = new Point(pg.Points[i].X + dx, pg.Points[i].Y + dy);
                }
            }
            else
            {
                double left = Canvas.GetLeft(shape);
                if (double.IsNaN(left)) left = 0;
                double top = Canvas.GetTop(shape);
                if (double.IsNaN(top)) top = 0;

                Canvas.SetLeft(shape, left + dx);
                Canvas.SetTop(shape, top + dy);
            }
        }

        /// <summary>
        /// Resizes the given shape using a delta (dx, dy) coming from mouse movement.
        /// Implementation depends on the specific shape type.
        /// </summary>
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
                if (b.Width < 1 || b.Height < 1)
                {
                    return;
                }

                double newW = Math.Max(5, b.Width + dx);
                double newH = Math.Max(5, b.Height + dy);
                ScalePolygonTo(pg, b, newW, newH);
            }
        }

        // =======================
        //  Selection management
        // =======================

        /// <summary>
        /// Marks the specified shape as selected and creates a resize handle for it.
        /// </summary>
        private void Select(Shape s)
        {
            Deselect();
            _selectedShape = s;
            AddResizeHandle();
            UpdatePropBoxes();
        }

        /// <summary>
        /// Clears selection state and removes any resize handle from the canvas.
        /// </summary>
        private void Deselect()
        {
            _selectedShape = null;
            if (_resizeHandle != null)
            {
                MainCanvas.Children.Remove(_resizeHandle);
                _resizeHandle = null;
            }
        }

        /// <summary>
        /// Creates and attaches a small resize handle to the currently selected shape.
        /// The handle is placed at the bottom-right corner of the shape's bounds.
        /// </summary>
        private void AddResizeHandle()
        {
            if (_selectedShape == null)
            {
                return;
            }

            _resizeHandle = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                Cursor = Cursors.SizeNWSE,
                IsHitTestVisible = true
            };

            // Mouse down on the handle starts a resize operation.
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

        /// <summary>
        /// Updates the visual position of the resize handle
        /// so that it sticks to the bottom-right corner of the selected shape.
        /// </summary>
        private void UpdateHandle()
        {
            if (_resizeHandle == null || _selectedShape == null)
            {
                return;
            }

            Rect b = GetBounds(_selectedShape);
            Canvas.SetLeft(_resizeHandle, b.Right - _resizeHandle.Width / 2);
            Canvas.SetTop(_resizeHandle, b.Bottom - _resizeHandle.Height / 2);
        }

        // =======================
        //  Property panel handling
        // =======================

        /// <summary>
        /// Fills the property UI text boxes (X/Y/Width/Height/Fill/Stroke)
        /// based on the currently selected shape.
        /// This method sets a flag to avoid recursive property updates.
        /// </summary>
        private void UpdatePropBoxes()
        {
            if (_selectedShape == null)
            {
                return;
            }

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

        /// <summary>
        /// When a property textbox receives focus, remember its original value
        /// and clear its content to allow immediate typing.
        /// </summary>
        private void PropBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_internalSet)
            {
                return;
            }

            _activeBox = sender as TextBox;
            _originalText = _activeBox?.Text;
            _activeBox?.SelectAll();
            _activeBox.Text = string.Empty;
        }

        /// <summary>
        /// Handles Enter key in property textboxes to commit changes immediately.
        /// </summary>
        private void PropBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitProp(sender as TextBox);
                // Move focus away from the text box to avoid further editing.
                this.Focus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// When a property textbox loses focus, we attempt to apply the edited value.
        /// </summary>
        private void PropBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitProp(sender as TextBox);
        }

        /// <summary>
        /// Validates and applies a property change coming from one of the text boxes.
        /// If the value is invalid or empty, the original value is restored.
        /// On successful change, a new undo state is stored.
        /// </summary>
        private void CommitProp(TextBox tb)
        {
            if (_internalSet || tb == null)
            {
                return;
            }

            // If left empty, restore original text and do not apply any change.
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                _internalSet = true;
                tb.Text = _originalText ?? tb.Text;
                _internalSet = false;
                _activeBox = null;
                _originalText = null;
                return;
            }

            try
            {
                if (_selectedShape == null)
                {
                    return;
                }

                Rect b = GetBounds(_selectedShape);

                // Position (X/Y)
                if (tb == XBox && double.TryParse(tb.Text, out double nx))
                {
                    MoveShape(_selectedShape, nx - b.X, 0);
                }
                else if (tb == YBox && double.TryParse(tb.Text, out double ny))
                {
                    MoveShape(_selectedShape, 0, ny - b.Y);
                }
                // Size (Width/Height)
                else if (tb == WidthBox && double.TryParse(tb.Text, out double w))
                {
                    ApplySizeToShape(_selectedShape, b, Math.Max(5, w), b.Height);
                }
                else if (tb == HeightBox && double.TryParse(tb.Text, out double h))
                {
                    ApplySizeToShape(_selectedShape, b, b.Width, Math.Max(5, h));
                }
                // Fill color
                else if (tb == FillColorBox)
                {
                    Color c = Colors.LightBlue;
                    try
                    {
                        c = (Color)ColorConverter.ConvertFromString(tb.Text);
                    }
                    catch
                    {
                        // If parsing fails, we fall back to the default color.
                    }
                    _selectedShape.Fill = new SolidColorBrush(c);
                }
                // Stroke color
                else if (tb == StrokeColorBox)
                {
                    Color c = Colors.Black;
                    try
                    {
                        c = (Color)ColorConverter.ConvertFromString(tb.Text);
                    }
                    catch
                    {
                        // If parsing fails, we fall back to the default color.
                    }
                    _selectedShape.Stroke = new SolidColorBrush(c);
                }

                UpdateHandle();
                UpdatePropBoxes();
                // Store a new undo state only when a property change is successfully applied.
                SaveState();
            }
            finally
            {
                _activeBox = null;
                _originalText = null;
            }
        }

        /// <summary>
        /// Applies a new width/height to the given shape, preserving its base position.
        /// Implementation differs by shape type (rectangle, ellipse, line, polygon).
        /// </summary>
        private void ApplySizeToShape(Shape s, Rect currentBounds, double targetW, double targetH)
        {
            if (s is Rectangle or Ellipse)
            {
                double left = Canvas.GetLeft(s);
                if (double.IsNaN(left)) left = currentBounds.X;
                double top = Canvas.GetTop(s);
                if (double.IsNaN(top)) top = currentBounds.Y;

                s.Width = targetW;
                s.Height = targetH;
                Canvas.SetLeft(s, left);
                Canvas.SetTop(s, top);
            }
            else if (s is Line ln)
            {
                // Adjust the second endpoint to match the target width/height,
                // preserving the direction of the line.
                ln.X2 = ln.X1 + targetW * Math.Sign((ln.X2 - ln.X1) == 0 ? 1 : (ln.X2 - ln.X1));
                ln.Y2 = ln.Y1 + targetH * Math.Sign((ln.Y2 - ln.Y1) == 0 ? 1 : (ln.Y2 - ln.Y1));
            }
            else if (s is Polygon pg)
            {
                ScalePolygonTo(pg, currentBounds, targetW, targetH);
            }
        }

        /// <summary>
        /// Computes a tight bounding rectangle for any supported shape type.
        /// </summary>
        private static Rect GetBounds(Shape shape)
        {
            if (shape is Rectangle or Ellipse)
            {
                double left = Canvas.GetLeft(shape);
                if (double.IsNaN(left)) left = 0;
                double top = Canvas.GetTop(shape);
                if (double.IsNaN(top)) top = 0;
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

        /// <summary>
        /// Rescales a polygon so that its bounding box becomes targetW x targetH,
        /// relative to its current bounding rectangle.
        /// </summary>
        private static void ScalePolygonTo(Polygon pg, Rect current, double targetW, double targetH)
        {
            if (current.Width <= 0 || current.Height <= 0)
            {
                return;
            }

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

        // =======================
        //  Zoom in/out
        // =======================

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

        // =======================
        //  Delete / Undo / Save-Load
        // =======================

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShape != null)
            {
                MainCanvas.Children.Remove(_selectedShape);
                Deselect();
                // Deletion is considered a real change and stored in the undo history.
                SaveState();
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var prev = _undo.Undo();
            if (prev != null)
            {
                SerializationService.Deserialize(MainCanvas, prev);
                // Selection is not part of the logical state, so it is cleared.
                Deselect();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string json = SerializationService.Serialize(MainCanvas);
            SerializationService.SaveToFile("project.json", json);
            MessageBox.Show("Project has been saved to VectorEditor\\bin\\Saved (JSON).");
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = SerializationService.LoadFromFile("project.json");
                SerializationService.Deserialize(MainCanvas, json);
                Deselect();
                _undo.ResetWith(json);
                MessageBox.Show("Project loaded successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while loading project: " + ex.Message);
            }
        }

        private void BtnSaveSvg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string svg = SvgService.SerializeToSvg(MainCanvas);
                SerializationService.SaveToFile("project.svg", svg);
                MessageBox.Show("SVG file has been saved to VectorEditor\\bin\\Saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while saving SVG: " + ex.Message);
            }
        }

        /// <summary>
        /// Serializes the current canvas and records a new undo state.
        /// </summary>
        private void SaveState(bool force = false)
        {
            string state = SerializationService.Serialize(MainCanvas);
            _undo.Save(state, force);
        }
    }
}
