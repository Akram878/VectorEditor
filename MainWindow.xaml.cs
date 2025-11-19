using System;
using System.Collections.Generic;
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
    /// Main application window for the vector editor.
    /// 
    /// Responsibilities:
    /// - Tool selection (rectangle, ellipse, line, free polygon, selection)
    /// - Drawing new shapes and editing existing ones (move / resize / color / geometry)
    /// - Freehand polygon construction with live preview
    /// - Selection visuals (resize handle)
    /// - Zooming (buttons + numeric zoom box)
    /// - Undo (based on serialized canvas state)
    /// - Saving/loading to JSON and SVG
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields: tools, selection, and drawing state

        /// <summary>
        /// Currently active drawing tool.
        /// </summary>
        private ShapeType _currentTool = ShapeType.Rectangle;

        /// <summary>
        /// If true, canvas clicks are used only for selecting shapes, not for drawing new ones.
        /// </summary>
        private bool _selectMode = false;

        /// <summary>
        /// Shape wrapper currently being drawn for non-polygon tools (rectangle, ellipse, line).
        /// </summary>
        private IShape _currentShape;

        /// <summary>
        /// Currently selected WPF shape (for moving, resizing and property editing).
        /// </summary>
        private Shape _selectedShape;

        /// <summary>
        /// Mouse position at the beginning of the current operation (draw/drag/resize).
        /// </summary>
        private Point _startPoint;

        /// <summary>
        /// True while a new shape is being drawn (for non-polygon tools).
        /// </summary>
        private bool _isDrawing;

        /// <summary>
        /// True while a selected shape is being dragged.
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// True while the selected shape is being resized via the resize handle.
        /// </summary>
        private bool _isResizing;

        /// <summary>
        /// Last mouse position observed during drag/resize.
        /// </summary>
        private Point _lastMousePos;

        /// <summary>
        /// Indicates that during the current operation something actually changed
        /// (shape moved, resized, or drawn). Used to avoid undo snapshots for
        /// simple selection clicks.
        /// </summary>
        private bool _hasDraggedOrResizedOrDrawn = false;

        /// <summary>
        /// Visual resize handle (small ellipse) attached to the bottom-right corner
        /// of the currently selected shape.
        /// </summary>
        private Ellipse _resizeHandle;

        #endregion

        #region Fields: zoom state

        /// <summary>
        /// Current zoom factor applied to ZoomContainer.
        /// 1.0 = 100%, 2.0 = 200% etc.
        /// </summary>
        private double _canvasScale = 1.0;

        /// <summary>
        /// Guard flag to avoid feedback loops when updating ZoomBox from code.
        /// </summary>
        private bool _internalZoomSet = false;

        #endregion

        #region Fields: undo and property panel

        /// <summary>
        /// Undo service storing up to N serialized canvas snapshots.
        /// Only real modifications (add/delete/move/resize/color, etc.)
        /// are recorded as undo steps.
        /// </summary>
        private readonly UndoService _undo = new UndoService(maxSteps: 5);

        /// <summary>
        /// Guard flag indicating that property text boxes are being updated by code,
        /// so that events (GotFocus/LostFocus) should not trigger logic.
        /// </summary>
        private bool _internalSet = false;

        /// <summary>
        /// Currently active property TextBox (X, Y, Width, Height, Fill, Stroke).
        /// Used to restore text if the user leaves it empty.
        /// </summary>
        private TextBox _activeBox = null;

        /// <summary>
        /// Original text in the active property TextBox before user editing.
        /// </summary>
        private string _originalText = null;

        #endregion

        #region Fields: free polygon drawing

        /// <summary>
        /// True while a free polygon is being constructed (multi-click polyline).
        /// </summary>
        private bool _isDrawingPolygon = false;

        /// <summary>
        /// Polyline used to show the permanent edges between vertices during polygon drawing.
        /// This is only a helper while drawing and is not used as the final shape.
        /// </summary>
        private Polyline _activePolyline = null;

        /// <summary>
        /// Solid preview line ("rubber band") from the last vertex to the current mouse position.
        /// </summary>
        private Line _polygonPreviewLine = null;

        /// <summary>
        /// Dashed preview line between the first and the last polygon vertices to indicate
        /// where the polygon will be closed.
        /// </summary>
        private Line _closingPreviewLine = null;

        /// <summary>
        /// List of all polygon vertices (in canvas coordinates) being created.
        /// </summary>
        private readonly List<Point> _polygonPoints = new();

        /// <summary>
        /// Fill color for the final polygon shape (applied only when the polygon is closed).
        /// </summary>
        private Color _polygonFillColor = Colors.Transparent;

        /// <summary>
        /// Stroke color for both preview and final polygon.
        /// </summary>
        private Color _polygonStrokeColor = Colors.Black;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            // Initialize zoom transform on the container that hosts the canvas.
            ZoomContainer.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
            UpdateZoomBox();

            // Record the initial (empty) canvas state as an undo baseline.
            SaveState(force: true);

            // Ensure the canvas has a minimum size even if no shapes are present.
            UpdateCanvasExtent();
        }

        #endregion

        #region Tool selection handlers

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            _selectMode = true;
            Deselect();
        }

        private void BtnRect_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            _selectMode = false;
            _currentTool = ShapeType.Rectangle;
            Deselect();
        }

        private void BtnEllipse_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            _selectMode = false;
            _currentTool = ShapeType.Ellipse;
            Deselect();
        }

        private void BtnLine_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            _selectMode = false;
            _currentTool = ShapeType.Line;
            Deselect();
        }

        private void BtnPolygon_Click(object sender, RoutedEventArgs e)
        {
            // Enter free polygon drawing mode.
            CancelPolygonDrawing();
            _selectMode = false;
            _currentTool = ShapeType.Polygon;
            Deselect();
        }

        #endregion

        #region Canvas mouse handlers (draw / drag / resize)

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(MainCanvas);

            // 1) Free polygon mode: handle clicks as vertices
            if (!_selectMode && _currentTool == ShapeType.Polygon)
            {
                HandlePolygonClick(_startPoint);
                return;
            }

            // 2) Clicked on an existing shape: select and possibly start dragging
            if (e.OriginalSource is Shape s && MainCanvas.Children.Contains(s))
            {
                Select(s);
                _isDragging = true;
                _hasDraggedOrResizedOrDrawn = false; // prevent simple selection from being treated as an edit
                _lastMousePos = _startPoint;
                Mouse.Capture(MainCanvas);
                return;
            }

            // 3) Selection mode: click on empty area clears selection, no new shape is created
            if (_selectMode)
            {
                Deselect();
                return;
            }

            // 4) Drawing a new non-polygon shape (rectangle, ellipse, line)
            _isDrawing = true;
            _hasDraggedOrResizedOrDrawn = false;

            _currentShape = ShapeFactory.CreateShape(_currentTool);
            if (_currentShape == null)
            {
                return;
            }

            // Determine fill and stroke using current property panel values.
            Color fillColor = Colors.LightBlue;
            Color strokeColor = Colors.Black;

            try
            {
                if (!string.IsNullOrWhiteSpace(FillColorBox.Text))
                    fillColor = (Color)ColorConverter.ConvertFromString(FillColorBox.Text);
            }
            catch
            {
                // Ignore parse errors and keep the default fill color.
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(StrokeColorBox.Text))
                    strokeColor = (Color)ColorConverter.ConvertFromString(StrokeColorBox.Text);
            }
            catch
            {
                // Ignore parse errors and keep the default stroke color.
            }

            _currentShape.SetFill(new SolidColorBrush(fillColor));
            _currentShape.SetStroke(new SolidColorBrush(strokeColor), 2);
            MainCanvas.Children.Add(_currentShape.ShapeElement);
            Mouse.Capture(MainCanvas);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MainCanvas);

            // Polygon in-progress: update preview "rubber-band" line from last vertex to mouse.
            if (_isDrawingPolygon && _polygonPreviewLine != null && !_isDragging && !_isResizing)
            {
                _polygonPreviewLine.X2 = pos.X;
                _polygonPreviewLine.Y2 = pos.Y;
                return;
            }

            // New non-polygon shape: update geometry while dragging.
            if (_isDrawing && _currentShape != null)
            {
                _currentShape.Draw(_startPoint, pos);
                _hasDraggedOrResizedOrDrawn = true;
                return;
            }

            // Dragging selected shape: move it according to mouse delta.
            if (_isDragging && _selectedShape != null && !_isResizing)
            {
                double dx = pos.X - _lastMousePos.X;
                double dy = pos.Y - _lastMousePos.Y;

                if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                {
                    MoveShape(_selectedShape, dx, dy);
                    _hasDraggedOrResizedOrDrawn = true;
                    _lastMousePos = pos;
                    UpdatePropBoxes();
                    UpdateHandle();
                }

                return;
            }

            // Resizing selected shape via resize handle.
            if (_isResizing && _selectedShape != null)
            {
                double dx = pos.X - _lastMousePos.X;
                double dy = pos.Y - _lastMousePos.Y;

                if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                {
                    ResizeShapeBy(_selectedShape, dx, dy);
                    _hasDraggedOrResizedOrDrawn = true;
                    _lastMousePos = pos;
                    UpdatePropBoxes();
                    UpdateHandle();
                }

                return;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Finish any ongoing drawing/dragging/resizing operation.
            if (_isDrawing || _isDragging || _isResizing)
            {
                bool changed =
                    _isDrawing ||                // creating a new shape always counts as a change
                    _isResizing ||               // resizing is a change
                    (_isDragging && _hasDraggedOrResizedOrDrawn); // pure selection click is not a change

                _isDrawing = false;
                _isDragging = false;
                _isResizing = false;
                _currentShape = null;
                Mouse.Capture(null);

                if (changed)
                {
                    // Only store an undo snapshot when something actually changed.
                    SaveState();
                    UpdateCanvasExtent();
                }

                _hasDraggedOrResizedOrDrawn = false;
            }
        }

        #endregion

        #region Free polygon construction

        /// <summary>
        /// Handles mouse clicks when the free polygon tool is active.
        /// - First click starts a new polygon.
        /// - Subsequent clicks add vertices.
        /// - Clicking close to any existing vertex closes the polygon.
        /// </summary>
        private void HandlePolygonClick(Point clickPos)
        {
            if (!_isDrawingPolygon)
            {
                StartPolygon(clickPos);
                return;
            }

            const double closeThreshold = 10.0;

            // If user clicks close to any existing vertex, close the polygon.
            foreach (var pt in _polygonPoints)
            {
                double dx = pt.X - clickPos.X;
                double dy = pt.Y - clickPos.Y;
                if (dx * dx + dy * dy <= closeThreshold * closeThreshold)
                {
                    FinishPolygon();
                    return;
                }
            }

            // Otherwise, treat this as a new vertex.
            AddPolygonPoint(clickPos);
        }

        /// <summary>
        /// Starts the process of drawing a new free polygon from the given first vertex.
        /// Creates a Polyline for edges, a solid preview line from last vertex to mouse,
        /// and a dashed preview line between first and last vertex.
        /// </summary>
        private void StartPolygon(Point firstPoint)
        {
            CancelPolygonDrawing();
            Deselect();

            _isDrawingPolygon = true;
            _polygonPoints.Clear();
            _polygonPoints.Add(firstPoint);

            // Resolve fill color for final polygon (applied only when closed).
            _polygonFillColor = Colors.Transparent;
            try
            {
                if (!string.IsNullOrWhiteSpace(FillColorBox.Text))
                    _polygonFillColor = (Color)ColorConverter.ConvertFromString(FillColorBox.Text);
            }
            catch
            {
                _polygonFillColor = Colors.Transparent;
            }

            // Resolve stroke color for previews and final polygon.
            _polygonStrokeColor = Colors.Black;
            try
            {
                if (!string.IsNullOrWhiteSpace(StrokeColorBox.Text))
                    _polygonStrokeColor = (Color)ColorConverter.ConvertFromString(StrokeColorBox.Text);
            }
            catch
            {
                _polygonStrokeColor = Colors.Black;
            }

            // Polyline for the permanent edges between vertices during drawing.
            _activePolyline = new Polyline
            {
                Stroke = new SolidColorBrush(_polygonStrokeColor),
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            _activePolyline.Points.Add(firstPoint);
            MainCanvas.Children.Add(_activePolyline);

            // Solid "rubber-band" line from the last vertex to the current mouse position.
            _polygonPreviewLine = new Line
            {
                X1 = firstPoint.X,
                Y1 = firstPoint.Y,
                X2 = firstPoint.X,
                Y2 = firstPoint.Y,
                Stroke = new SolidColorBrush(_polygonStrokeColor),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            MainCanvas.Children.Add(_polygonPreviewLine);

            // Dashed preview line between the first and last vertices to illustrate closure.
            _closingPreviewLine = new Line
            {
                X1 = firstPoint.X,
                Y1 = firstPoint.Y,
                X2 = firstPoint.X,
                Y2 = firstPoint.Y,
                Stroke = new SolidColorBrush(_polygonStrokeColor),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            MainCanvas.Children.Add(_closingPreviewLine);
        }

        /// <summary>
        /// Adds a new vertex to the active polygon and updates preview lines accordingly.
        /// </summary>
        private void AddPolygonPoint(Point p)
        {
            if (!_isDrawingPolygon || _activePolyline == null)
                return;

            _polygonPoints.Add(p);
            _activePolyline.Points.Add(p);

            // Update rubber-band preview line start to the new last vertex.
            if (_polygonPreviewLine != null)
            {
                _polygonPreviewLine.X1 = p.X;
                _polygonPreviewLine.Y1 = p.Y;
                _polygonPreviewLine.X2 = p.X;
                _polygonPreviewLine.Y2 = p.Y;
            }

            // Update dashed closure preview line between first and last vertices.
            if (_closingPreviewLine != null && _polygonPoints.Count >= 2)
            {
                var first = _polygonPoints[0];
                var last = _polygonPoints[_polygonPoints.Count - 1];
                _closingPreviewLine.X1 = first.X;
                _closingPreviewLine.Y1 = first.Y;
                _closingPreviewLine.X2 = last.X;
                _closingPreviewLine.Y2 = last.Y;
            }
        }

        /// <summary>
        /// Finalizes the current polygon:
        /// - Removes temporary preview visuals
        /// - Creates a closed Polygon shape with fill
        /// - Saves an undo step
        /// </summary>
        private void FinishPolygon()
        {
            if (!_isDrawingPolygon || _activePolyline == null || _polygonPoints.Count < 2)
            {
                CancelPolygonDrawing();
                return;
            }

            // Remove preview visuals from the canvas.
            if (_polygonPreviewLine != null && MainCanvas.Children.Contains(_polygonPreviewLine))
                MainCanvas.Children.Remove(_polygonPreviewLine);
            if (_closingPreviewLine != null && MainCanvas.Children.Contains(_closingPreviewLine))
                MainCanvas.Children.Remove(_closingPreviewLine);
            if (MainCanvas.Children.Contains(_activePolyline))
                MainCanvas.Children.Remove(_activePolyline);

            _polygonPreviewLine = null;
            _closingPreviewLine = null;

            // Create the final closed polygon shape.
            var poly = new Polygon
            {
                Stroke = new SolidColorBrush(_polygonStrokeColor),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(_polygonFillColor)
            };

            foreach (var pt in _polygonPoints)
                poly.Points.Add(pt);

            MainCanvas.Children.Add(poly);

            _isDrawingPolygon = false;
            _activePolyline = null;
            _polygonPoints.Clear();

            SaveState();
            UpdateCanvasExtent();
        }

        /// <summary>
        /// Cancels any in-progress polygon drawing and removes any temporary preview shapes.
        /// Does not affect already finished shapes on the canvas.
        /// </summary>
        private void CancelPolygonDrawing()
        {
            if (_polygonPreviewLine != null && MainCanvas.Children.Contains(_polygonPreviewLine))
                MainCanvas.Children.Remove(_polygonPreviewLine);
            if (_closingPreviewLine != null && MainCanvas.Children.Contains(_closingPreviewLine))
                MainCanvas.Children.Remove(_closingPreviewLine);
            if (_activePolyline != null && MainCanvas.Children.Contains(_activePolyline))
                MainCanvas.Children.Remove(_activePolyline);

            _polygonPreviewLine = null;
            _closingPreviewLine = null;
            _activePolyline = null;
            _isDrawingPolygon = false;
            _polygonPoints.Clear();
        }

        #endregion

        #region Shape movement and resizing

        /// <summary>
        /// Translates a shape by the given delta.
        /// Supports Line, Polygon and basic shapes (Rectangle/Ellipse).
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
                    pg.Points[i] = new Point(pg.Points[i].X + dx, pg.Points[i].Y + dy);
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
        /// Resizes a shape based on mouse delta.
        /// For polygons, performs a scaling around the shape's bounding box.
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
                if (b.Width < 1 || b.Height < 1) return;

                double newW = Math.Max(5, b.Width + dx);
                double newH = Math.Max(5, b.Height + dy);
                ScalePolygonTo(pg, b, newW, newH);
            }
        }

        #endregion

        #region Selection and resize handle management

        /// <summary>
        /// Selects the specified shape and attaches a resize handle to it.
        /// </summary>
        private void Select(Shape s)
        {
            Deselect();
            _selectedShape = s;
            AddResizeHandle();
            UpdatePropBoxes();
        }

        /// <summary>
        /// Clears the current selection and removes the resize handle if present.
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
        /// Creates and positions a resize handle for the currently selected shape.
        /// The handle is placed at the bottom-right corner of the shape's bounds.
        /// </summary>
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
                _hasDraggedOrResizedOrDrawn = false;
                _lastMousePos = e.GetPosition(MainCanvas);
                Mouse.Capture(MainCanvas);
            };

            UpdateHandle();
            MainCanvas.Children.Add(_resizeHandle);
        }

        /// <summary>
        /// Updates the position of the resize handle to follow the selected shape's bounds.
        /// </summary>
        private void UpdateHandle()
        {
            if (_resizeHandle == null || _selectedShape == null) return;

            Rect b = GetBounds(_selectedShape);
            Canvas.SetLeft(_resizeHandle, b.Right - _resizeHandle.Width / 2);
            Canvas.SetTop(_resizeHandle, b.Bottom - _resizeHandle.Height / 2);
        }

        #endregion

        #region Property panel (X/Y/Width/Height/Colors)

        /// <summary>
        /// Synchronizes the property panel TextBoxes with the currently selected shape.
        /// </summary>
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
                this.Focus();
                e.Handled = true;
            }
        }

        private void PropBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitProp(sender as TextBox);
        }

        /// <summary>
        /// Validates and applies a change from a property TextBox (position, size, or color).
        /// Triggers an undo snapshot only for real modifications.
        /// </summary>
        private void CommitProp(TextBox tb)
        {
            if (_internalSet || tb == null) return;

            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                // Restore previous value if the user leaves the field empty.
                _internalSet = true;
                tb.Text = _originalText ?? tb.Text;
                _internalSet = false;
                _activeBox = null;
                _originalText = null;
                return;
            }

            try
            {
                if (_selectedShape == null) return;

                Rect b = GetBounds(_selectedShape);

                if (tb == XBox && double.TryParse(tb.Text, out double nx))
                {
                    MoveShape(_selectedShape, nx - b.X, 0);
                }
                else if (tb == YBox && double.TryParse(tb.Text, out double ny))
                {
                    MoveShape(_selectedShape, 0, ny - b.Y);
                }
                else if (tb == WidthBox && double.TryParse(tb.Text, out double w))
                {
                    ApplySizeToShape(_selectedShape, b, Math.Max(5, w), b.Height);
                }
                else if (tb == HeightBox && double.TryParse(tb.Text, out double h))
                {
                    ApplySizeToShape(_selectedShape, b, b.Width, Math.Max(5, h));
                }
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
                SaveState();
                UpdateCanvasExtent();
            }
            finally
            {
                _activeBox = null;
                _originalText = null;
            }
        }

        /// <summary>
        /// Applies a new size to the given shape using bounding box information.
        /// For polygons, scales all vertices to fit the requested width/height.
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
                ln.X2 = ln.X1 + targetW * Math.Sign((ln.X2 - ln.X1) == 0 ? 1 : (ln.X2 - ln.X1));
                ln.Y2 = ln.Y1 + targetH * Math.Sign((ln.Y2 - ln.Y1) == 0 ? 1 : (ln.Y2 - ln.Y1));
            }
            else if (s is Polygon pg)
            {
                ScalePolygonTo(pg, currentBounds, targetW, targetH);
            }
        }

        /// <summary>
        /// Returns the bounding rectangle for the given shape.
        /// Supports Rectangle, Ellipse, Line, and Polygon.
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
        /// Uniformly scales a polygon from its current bounding box to the desired target
        /// width/height, using the top-left corner of the current bounds as the origin.
        /// </summary>
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

        #endregion

        #region Zoom (buttons + numeric text box)

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _canvasScale = Math.Min(5.0, _canvasScale + 0.1);
            ZoomContainer.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
            UpdateZoomBox();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _canvasScale = Math.Max(0.2, _canvasScale - 0.1);
            ZoomContainer.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
            UpdateZoomBox();
        }

        /// <summary>
        /// Updates ZoomBox.Text to reflect the current zoom factor in percent.
        /// </summary>
        private void UpdateZoomBox()
        {
            if (ZoomBox == null) return;
            _internalZoomSet = true;
            ZoomBox.Text = Math.Round(_canvasScale * 100).ToString();
            _internalZoomSet = false;
        }

        /// <summary>
        /// Parses and applies a zoom value from ZoomBox.Text.
        /// Accepted range: 20% - 500%.
        /// </summary>
        private void ApplyZoomFromBox()
        {
            if (_internalZoomSet) return;
            if (ZoomBox == null) return;

            if (string.IsNullOrWhiteSpace(ZoomBox.Text))
            {
                UpdateZoomBox();
                return;
            }

            if (!double.TryParse(ZoomBox.Text, out double percent))
            {
                // Invalid input: restore previous value.
                UpdateZoomBox();
                return;
            }

            // Clamp zoom between 20% and 500%.
            percent = Math.Max(20.0, Math.Min(500.0, percent));
            _canvasScale = percent / 100.0;
            ZoomContainer.LayoutTransform = new ScaleTransform(_canvasScale, _canvasScale);
            UpdateZoomBox();
        }

        private void ZoomBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ZoomBox?.SelectAll();
        }

        private void ZoomBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyZoomFromBox();
                this.Focus();
                e.Handled = true;
            }
        }

        private void ZoomBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyZoomFromBox();
        }

        #endregion

        #region Delete, Undo, Save / Load (JSON, SVG)

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShape != null)
            {
                MainCanvas.Children.Remove(_selectedShape);
                Deselect();
                SaveState();
                UpdateCanvasExtent();
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            Deselect();

            var prev = _undo.Undo();
            if (prev != null)
            {
                SerializationService.Deserialize(MainCanvas, prev);
                Deselect();
                UpdateCanvasExtent();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Ensure no selection visuals or polygon previews are persisted.
            Deselect();
            CancelPolygonDrawing();
            RemoveResizeHandlesIfAny();

            string json = SerializationService.Serialize(MainCanvas);
            SerializationService.SaveToFile("project.json", json);
            MessageBox.Show("Project has been saved to VectorEditor\\bin\\Saved (JSON).");
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            CancelPolygonDrawing();
            Deselect();

            try
            {
                string json = SerializationService.LoadFromFile("project.json");
                SerializationService.Deserialize(MainCanvas, json);
                Deselect();
                _undo.ResetWith(json);
                UpdateCanvasExtent();
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
                // Remove selection visuals before exporting to SVG.
                Deselect();
                CancelPolygonDrawing();
                RemoveResizeHandlesIfAny();

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
        /// Removes any resize handles currently attached to shapes.
        /// Used before serialization to keep the exported scene clean.
        /// </summary>
        private void RemoveResizeHandlesIfAny()
        {
            var handles = MainCanvas.Children
                .OfType<Ellipse>()
                .Where(el => el.Width == 10 && el.Height == 10 && el.Cursor == Cursors.SizeNWSE)
                .ToList();

            foreach (var h in handles)
                MainCanvas.Children.Remove(h);
        }

        /// <summary>
        /// Serializes current canvas content and pushes it onto the undo stack.
        /// </summary>
        private void SaveState(bool force = false)
        {
            string state = SerializationService.Serialize(MainCanvas);
            _undo.Save(state, force);
        }

        /// <summary>
        /// Adjusts the canvas size to tightly fit all shapes with an additional margin.
        /// Ensures a minimum size when there are no shapes.
        /// </summary>
        private void UpdateCanvasExtent()
        {
            if (MainCanvas == null) return;

            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            bool any = false;

            foreach (var child in MainCanvas.Children.OfType<Shape>())
            {
                Rect b = GetBounds(child);
                if (b.IsEmpty) continue;

                if (!any)
                {
                    minX = b.X; minY = b.Y;
                    maxX = b.Right; maxY = b.Bottom;
                    any = true;
                }
                else
                {
                    minX = Math.Min(minX, b.X);
                    minY = Math.Min(minY, b.Y);
                    maxX = Math.Max(maxX, b.Right);
                    maxY = Math.Max(maxY, b.Bottom);
                }
            }

            const double margin = 50;
            double width = any ? (maxX - minX + margin) : 800;
            double height = any ? (maxY - minY + margin) : 600;

            MainCanvas.Width = Math.Max(width, 800);
            MainCanvas.Height = Math.Max(height, 600);
        }

        #endregion
    }
}
