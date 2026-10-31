using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Renderer;
using WinFormsPanel = System.Windows.Forms.Panel;

namespace SharpKit.Controls;

/// <summary>A WPF control that displays a single Skyrim NIF model preview.</summary>
public sealed class ModelPreviewControl : System.Windows.Controls.UserControl
{
    private sealed class RenderHostPanel : WinFormsPanel
    {
        public RenderHostPanel()
        {
            SetStyle(System.Windows.Forms.ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs e)
        {
        }
    }

    public static readonly DependencyProperty ModelPathProperty = DependencyProperty.Register(
        nameof(ModelPath), typeof(string), typeof(ModelPreviewControl),
        new PropertyMetadata(null, OnModelPathChanged));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(ModelPreviewControl),
        new PropertyMetadata(false, OnShowGridChanged));

    public static readonly DependencyProperty TextureSetOverridesProperty = DependencyProperty.Register(
        nameof(TextureSetOverrides), typeof(IList<ITextureSetGetter>), typeof(ModelPreviewControl),
        new PropertyMetadata(null, OnTextureSetOverridesChanged));

    private readonly WinFormsPanel _panel;
    private readonly PluginFileManager _pluginFileManager;
    private readonly ILogger<MainRenderer> _logger;
    private PreviewRenderer? _renderer;
    private INotifyCollectionChanged? _observedTextureSetOverrides;
    private bool _isLoaded;
    private System.Drawing.Point _lastDragPosition;
    private System.Windows.Forms.MouseButtons _dragButton;

    public ModelPreviewControl()
    {
        var services = App.ServiceProvider
            ?? throw new InvalidOperationException("The application services are not available yet.");
        _pluginFileManager = services.GetRequiredService<PluginFileManager>();
        _logger = services.GetRequiredService<ILogger<MainRenderer>>();

        _panel = new RenderHostPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(87, 89, 92)
        };
        _panel.MouseDown += OnPanelMouseDown;
        _panel.MouseMove += OnPanelMouseMove;
        _panel.MouseUp += OnPanelMouseUp;
        _panel.MouseWheel += OnPanelMouseWheel;
        _panel.SizeChanged += OnPanelSizeChanged;
        Content = new WindowsFormsHost { Child = _panel };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? ModelPath
    {
        get => (string?)GetValue(ModelPathProperty);
        set => SetValue(ModelPathProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public IList<ITextureSetGetter?>? TextureSetOverrides
    {
        get => (IList<ITextureSetGetter?>?)GetValue(TextureSetOverridesProperty);
        set => SetValue(TextureSetOverridesProperty, value);
    }

    private static void OnModelPathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ModelPreviewControl)dependencyObject;
        if (control._isLoaded)
            control.LoadModel((string?)e.NewValue);
    }

    private static void OnShowGridChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ModelPreviewControl)dependencyObject;
        if (control._renderer is not null)
            control._renderer.ShowGrid = (bool)e.NewValue;
    }

    private static void OnTextureSetOverridesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (ModelPreviewControl)dependencyObject;
        if (control._observedTextureSetOverrides is not null)
            control._observedTextureSetOverrides.CollectionChanged -= control.OnTextureSetOverridesCollectionChanged;

        control._observedTextureSetOverrides = e.NewValue as INotifyCollectionChanged;
        if (control._observedTextureSetOverrides is not null)
            control._observedTextureSetOverrides.CollectionChanged += control.OnTextureSetOverridesCollectionChanged;

        if (control._isLoaded)
            control.LoadModel(control.ModelPath);
    }

    private void OnTextureSetOverridesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isLoaded)
            LoadModel(ModelPath);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        CreateRenderer();
        LoadModel(ModelPath);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _renderer?.Dispose();
        _renderer = null;
    }

    private void CreateRenderer()
    {
        if (_renderer is not null || _panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
            return;

        var dataFolderPath = _pluginFileManager.GetEnv().DataFolderPath;
        _renderer = new PreviewRenderer(_logger, dataFolderPath);
        _renderer.Initialize(_panel.Handle, (uint)_panel.ClientSize.Width, (uint)_panel.ClientSize.Height);
        _renderer.ShowGrid = ShowGrid;
    }

    private void LoadModel(string? path)
    {
        if (!_isLoaded || _panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
            return;

        try
        {
            CreateRenderer();
            if (_renderer is null)
                return;

            if (string.IsNullOrWhiteSpace(path))
                _renderer.ClearModel();
            else
                _renderer.LoadNif(path, TextureSetOverrides);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not load preview model {ModelPath}", path);
            _renderer?.ClearModel();
        }
    }

    private void OnPanelSizeChanged(object? sender, EventArgs e)
    {
        if (_isLoaded && _renderer is null)
        {
            CreateRenderer();
            LoadModel(ModelPath);
        }

        if (_renderer is null || _panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
            return;

        _renderer.Resize((uint)_panel.ClientSize.Width, (uint)_panel.ClientSize.Height);
    }

    private void OnPanelMouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button is not (System.Windows.Forms.MouseButtons.Left
            or System.Windows.Forms.MouseButtons.Middle
            or System.Windows.Forms.MouseButtons.Right))
            return;

        _panel.Focus();
        _panel.Capture = true;
        _dragButton = e.Button;
        _lastDragPosition = e.Location;
    }

    private void OnPanelMouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (_dragButton == System.Windows.Forms.MouseButtons.None || e.Button == System.Windows.Forms.MouseButtons.None)
            return;

        var deltaX = e.X - _lastDragPosition.X;
        var deltaY = e.Y - _lastDragPosition.Y;
        _lastDragPosition = e.Location;

        if (_dragButton == System.Windows.Forms.MouseButtons.Left)
            _renderer?.Orbit(deltaX, deltaY);
        else
            _renderer?.Pan(deltaX, deltaY);
    }

    private void OnPanelMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != _dragButton)
            return;

        _dragButton = System.Windows.Forms.MouseButtons.None;
        _panel.Capture = false;
    }

    private void OnPanelMouseWheel(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        _renderer?.Zoom(e.Delta / (float)System.Windows.Forms.SystemInformation.MouseWheelScrollDelta);
    }
}
