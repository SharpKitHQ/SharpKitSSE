using SharpKit.Renderer;

namespace SharpKit.Controls;

public sealed class Dx11Panel : Panel
{
    private readonly TriangleRenderer _renderer;
    private Point _lastMousePosition;
    private bool _orbiting;
    private bool _panning;
    
    public Dx11Panel(TriangleRenderer renderer)
    {
        _renderer = renderer;
        BackColor = System.Drawing.Color.Black;
        SetStyle(ControlStyles.Opaque, true);
    }
    
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        _lastMousePosition = e.Location;

        if (e.Button == MouseButtons.Left)
            _orbiting = true;
        else if (e.Button == MouseButtons.Middle)
            _panning = true;

        if (_orbiting || _panning)
            Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_orbiting && !_panning)
            return;

        float dx = e.X - _lastMousePosition.X;
        float dy = e.Y - _lastMousePosition.Y;

        _lastMousePosition = e.Location;

        if (_orbiting)
            _renderer.Orbit(dx, dy);
        else if (_panning)
            _renderer.Pan(dx, dy);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left)
            _orbiting = false;
        else if (e.Button == MouseButtons.Middle)
            _panning = false;

        if (!_orbiting && !_panning)
            Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        
        _renderer.Zoom(e.Delta / 120f);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Direct3D owns the contents of this window.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Do not draw GDI content over the Direct3D output.
    }
}