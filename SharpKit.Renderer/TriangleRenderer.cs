using System.Numerics;
using System.Runtime.InteropServices;
using DrawingColor = System.Drawing.Color;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Skyrim;
using NiflySharp;
using NiflySharp.Blocks;
using NiflySharp.Structs;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using Color4 = Vortice.Mathematics.Color4;

namespace SharpKit.Renderer;

public sealed class TriangleRenderer : IDisposable
{
    private readonly ILogger<TriangleRenderer> _log;
    private readonly CachedBSAFileProvider _bsaFileProvider;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _renderTarget;
    private ID3D11Texture2D? _depthTexture;
    private ID3D11DepthStencilView? _depthView;

    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _indexBuffer;
    private ID3D11Buffer? _sceneBuffer;

    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11DepthStencilState? _depthState;
    private ID3D11DepthStencilState? _transparentDepthState;
    private ID3D11BlendState? _alphaBlendState;
    private ID3D11RasterizerState? _rasterizerState;
    private ID3D11SamplerState? _diffuseSampler;
    private readonly Dictionary<string, (ID3D11Texture2D Texture, ID3D11ShaderResourceView View)> _textureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unavailableTexturePaths = new(StringComparer.OrdinalIgnoreCase);
    private ID3D11Texture2D? _whiteTexture;
    private ID3D11ShaderResourceView? _whiteTextureView;

    private bool _initialized;
    private bool _disposed;
    private string? _dataFolderPath;

    private uint _indexCount;
    private uint _width;
    private uint _height;
    private readonly List<(string TexturePath, Vector4 Tint, bool IsTransparent, uint Start, uint Count)> _drawRanges = [];

    // Orbit camera. Skyrim assets are Z-up.
    private float _orbitYaw = -MathF.PI / 4f;
    private float _orbitPitch = 0.35f;
    private float _cameraDistance = 5f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _modelRadius = 1f;
    private Vector3 _lightDirection = Vector3.Normalize(new Vector3(-0.4f, 0.7f, 1f));
    private Vector3 _ambientLightColor = new(0.25f);
    private Vector3 _directionalLightColor = new(1f, 0.95f, 0.9f);

    // private readonly List<(Vector3 A, Vector3 B, Vector3 C)> _modelTriangles = new();

    private const float FieldOfView = MathF.PI / 4f;
    private const float NearPlane = 0.01f;

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Vector4 Tint;

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 tint)
        {
            Position = position;
            Normal = normal;
            TexCoord = texCoord;
            Tint = tint;
        }
    }

    // Must match the HLSL constant-buffer layout (128 bytes).
    [StructLayout(LayoutKind.Sequential)]
    private struct SceneConstants
    {
        public Matrix4x4 WorldViewProjection;
        public Vector4 LightDirection;
        public Vector4 CameraPosition;
        public Vector4 AmbientLightColor;
        public Vector4 DirectionalLightColor;
    }

    public TriangleRenderer(ILogger<TriangleRenderer> log)
    {
        _log = log;
        _bsaFileProvider = new(_log);
        
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Meshes0.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Meshes1.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures0.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures1.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures2.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures3.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures4.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures5.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures6.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures7.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data", "Skyrim - Textures8.bsa"));
    }

    private struct SceneObject
    {
        public Guid Id;
        public List<(Vector3 A, Vector3 B, Vector3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC)> ModelTris;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale;
        public Matrix4x4 NifTransform;
        public string TexturePath;
        public Vector4 Tint;
        public bool IsTransparent;

        public Vector2 UVScale;
        public Vector2 UVOffset;
    }

    private List<SceneObject> _sceneObjects = [];

    public void Initialize(nint hwnd, uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
            throw new InvalidOperationException("Already initialized.");

        if (hwnd == 0)
            throw new ArgumentException("HWND cannot be zero.", nameof(hwnd));

        if (width == 0 || height == 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Render dimensions must be positive.");

        _width = width;
        _height = height;

        try
        {
            var swapChainDesc = new SwapChainDescription
            {
                BufferCount = 2,
                BufferDescription = new ModeDescription
                {
                    Width = width,
                    Height = height,
                    Format = Format.R8G8B8A8_UNorm,
                    RefreshRate = new Rational(60, 1)
                },
                BufferUsage = Usage.RenderTargetOutput,
                OutputWindow = hwnd,
                SampleDescription = new SampleDescription(1, 0),
                Windowed = true,
                SwapEffect = SwapEffect.Discard
            };

            FeatureLevel[] featureLevels =
            [
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0
            ];

            D3D11CreateDeviceAndSwapChain(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                featureLevels,
                swapChainDesc,
                out _swapChain,
                out _device,
                out _,
                out _context).CheckError();

            CreateRenderTargets(width, height);

            string shaderPath = Path.Combine(
                AppContext.BaseDirectory, "Shaders", "Model.hlsl");

            var vs = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_4_0");
            var ps = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_4_0");

            _vertexShader = _device!.CreateVertexShader(vs.Span);
            _pixelShader = _device.CreatePixelShader(ps.Span);

            InputElementDescription[] inputElements =
            [
                new("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
                new("COLOR", 0, Format.R32G32B32A32_Float, 32, 0)
            ];

            _inputLayout = _device.CreateInputLayout(inputElements, vs.Span);

            _sceneBuffer = _device.CreateBuffer(
                new SceneConstants[1], BindFlags.ConstantBuffer);
            _diffuseSampler = _device.CreateSamplerState(new SamplerDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunc = ComparisonFunction.Never,
                MinLOD = 0,
                MaxLOD = float.MaxValue
            });
            CreateWhiteTexture();

            _depthState = _device.CreateDepthStencilState(new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.Less,
                StencilEnable = false
            });
            _transparentDepthState = _device.CreateDepthStencilState(new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthFunc = ComparisonFunction.Less,
                StencilEnable = false
            });

            _rasterizerState = _device.CreateRasterizerState(new RasterizerDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Back,
                FrontCounterClockwise = true,
                DepthClipEnable = true
            });
            _alphaBlendState = _device.CreateBlendState(BlendDescription.NonPremultiplied);

            _initialized = true;
            Render();
        }
        catch
        {
            DisposeResources();
            throw;
        }
    }

    public void LoadCell(ICellGetter cell, IGameEnvironment env)
    {
        _dataFolderPath = env.DataFolderPath;
        _log.LogInformation("Loading cell {Name}", cell.Name?.String);
        ApplyCellLighting(cell, env);

        var allGetters = cell.Temporary.Concat(cell.Persistent);
        
        foreach (var p in allGetters)
        {
            
            if(p is IPlacedObjectGetter po) 
            {
                if(po.Base.TryResolve(env.LinkCache, out var b))
                {
                    if (b is IModeledGetter stat)
                    {
                        _log.LogInformation("Found static {ID}", b.EditorID);

                        var placement = po.Placement;
                        var position = new Vector3(
                            placement?.Position.X ?? 0,
                            placement?.Position.Y ?? 0,
                            placement?.Position.Z ?? 0);
                        var rotation = new Vector3(
                            placement?.Rotation.X ?? 0,
                            placement?.Rotation.Y ?? 0,
                            placement?.Rotation.Z ?? 0);
                        float scale = po.Scale ?? 1f;
                        
                        var fullModelPath = Path.Combine(env.DataFolderPath, "meshes", stat.Model?.File.GivenPath ?? "");
                        if (File.Exists(fullModelPath))
                        {
                            LoadModel(fullModelPath, position, rotation, scale);
                        }
                        else
                        {
                            var stream = _bsaFileProvider.GetFileFromAny(Path.Combine("meshes", stat.Model?.File.GivenPath ?? ""));
                            if(stream is not null)
                                LoadModel(stream, position, rotation, scale);
                        }
                    }
                }
            }
        }

        // Center the camera on all loaded models after the cell is fully loaded.
        CenterCamera();
    }

    public void LoadModel(string path, Vector3 position)
        => LoadModel(path, position, Vector3.Zero, 1f);

    public void LoadModel(string path, Vector3 position, Vector3 rotation, float scale)
    {
        if (File.Exists(path))
        {
            var fstream = File.OpenRead(path);
            LoadModel(fstream, position, rotation, scale);
        }
    }
    
    public void LoadModel(Stream fileStream, Vector3 position)
        => LoadModel(fileStream, position, Vector3.Zero, 1f);

    public void LoadModel(Stream fileStream, Vector3 position, Vector3 rotation, float scale)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
            throw new InvalidOperationException("Call Initialize before LoadModel.");

        var nif = new NifFile();
        int result = nif.Load(fileStream);

        if (result != 0)
            throw new InvalidOperationException($"Failed to load NIF (error {result}).");

        // _modelTriangles.Clear();

        int shapeCount = 0;
        int renderableTriangleCount = 0;
        int sourceVertexCount = 0;
        int sourceTriangleCount = 0;

        var nifTransforms = new Dictionary<INiShape, Matrix4x4>(ReferenceEqualityComparer.Instance);
        void CollectTransforms(NiAVObject node, Matrix4x4 parentTransform)
        {
            Matrix4x4 localTransform = CreateNifTransform(node.Translation, node.Rotation, node.Scale);
            Matrix4x4 accumulatedTransform = localTransform * parentTransform;
            if (node is INiShape nodeShape)
                nifTransforms[nodeShape] = accumulatedTransform;

            if (node is NiNode niNode)
            {
                foreach (int childIndex in niNode.Children.Indices)
                {
                    if (nif.GetBlock(childIndex) is NiAVObject child)
                        CollectTransforms(child, accumulatedTransform);
                }
            }
        }

        foreach (var rootNode in nif.GetRootNodes())
            CollectTransforms(rootNode, Matrix4x4.Identity);

        foreach (var shape in nif.GetShapes())
        {
            shapeCount++;
            var obj = new SceneObject
            {
                Id = Guid.NewGuid(),
                ModelTris = [],
                Position = position,
                Rotation = rotation,
                Scale = scale,
                NifTransform = nifTransforms.GetValueOrDefault(
                    shape, CreateNifTransform(shape.Translation, shape.Rotation, shape.Scale)),
                UVScale = Vector2.One,
                Tint = Vector4.One
            };

            var alphaProperty = shape.HasAlphaProperty
                ? nif.GetBlock<NiAlphaProperty>(shape.AlphaPropertyRef)
                : null;
            obj.IsTransparent = alphaProperty?.Flags.AlphaBlend == true;

            var shader = shape.HasShaderProperty ? nif.GetShader(shape) : null;
            if (shader is not null)
            {
                obj.UVScale = new Vector2(shader.UVScale.U, shader.UVScale.V);
                obj.UVOffset = new Vector2(shader.UVOffset.U, shader.UVOffset.V);

                if (shader is BSEffectShaderProperty effectShader)
                {
                    string effectTexturePath = effectShader.SourceTexture.Content;
                    if (alphaProperty is not null && string.IsNullOrWhiteSpace(effectTexturePath))
                    {
                        // Creation Kit displays textureless effect-shader marker meshes as red translucent proxies.
                        obj.Tint = new Vector4(1f, 0f, 0f, 0.4f);
                        obj.IsTransparent = true;
                    }
                    else
                    {
                        var color = effectShader.BaseColor;
                        float falloffOpacity = (effectShader.FalloffStartOpacity + effectShader.FalloffStopOpacity) * 0.5f;
                        float alpha = float.IsFinite(falloffOpacity) && falloffOpacity > 0f
                            ? color.A * falloffOpacity
                            : color.A;

                        obj.Tint = new Vector4(
                            color.R * effectShader.BaseColorScale,
                            color.G * effectShader.BaseColorScale,
                            color.B * effectShader.BaseColorScale,
                            alpha);
                        obj.IsTransparent |= color.A < 0.999f || falloffOpacity < 0.999f || alphaProperty is not null;
                    }

                    if (!string.IsNullOrWhiteSpace(effectTexturePath))
                        obj.TexturePath = effectTexturePath;
                }
            }

            Vector3[] positions;
            Vector2[] uvs;
            IEnumerable<Triangle> triangles;

            if (shape is BSTriShape bsShape)
            {
                if (shader is not null)
                {
                    if (shader.HasTextureSet)
                    {
                        var textureSet = nif.GetBlock<BSShaderTextureSet>(shader.TextureSetRef);
                        if (textureSet.Textures.Count >= 1)
                        {
                            var diffuse = textureSet.Textures[0].Content;
                            obj.TexturePath = diffuse;
                        }
                    }

                }
                
                positions = bsShape.VertexPositions.ToArray();
                uvs = bsShape.UVs.Select(uv => new Vector2(uv.U, uv.V)).ToArray();
                triangles = bsShape.Triangles;
            }
            else
            {
                var data = shape.GeometryData;
                if (data == null)
                    continue;

                positions = data.Vertices?.ToArray() ?? Array.Empty<Vector3>();
                uvs = [];
                triangles = data.Triangles ?? Enumerable.Empty<Triangle>();
            }

            sourceVertexCount += positions.Length;

            foreach (var tri in triangles)
            {
                sourceTriangleCount++;

                int i0 = tri.V1;
                int i1 = tri.V2;
                int i2 = tri.V3;

                if ((uint)i0 >= (uint)positions.Length ||
                    (uint)i1 >= (uint)positions.Length ||
                    (uint)i2 >= (uint)positions.Length)
                    continue;

                Vector3 a = positions[i0];
                Vector3 b = positions[i1];
                Vector3 c = positions[i2];

                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                    continue;

                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f)
                    continue;

                Vector2 uvA = (uint)i0 < (uint)uvs.Length ? uvs[i0] : Vector2.Zero;
                Vector2 uvB = (uint)i1 < (uint)uvs.Length ? uvs[i1] : Vector2.Zero;
                Vector2 uvC = (uint)i2 < (uint)uvs.Length ? uvs[i2] : Vector2.Zero;
                obj.ModelTris.Add((a, b, c, uvA, uvB, uvC));
            }

            if (obj.ModelTris.Count > 0)
            {
                renderableTriangleCount += obj.ModelTris.Count;
                _sceneObjects.Add(obj);
            }
        }

        if (renderableTriangleCount == 0)
        {
            throw new InvalidOperationException(
                $"No valid triangles found in NIF. " +
                $"Shapes={shapeCount}, Vertices={sourceVertexCount}, " +
                $"SourceTriangles={sourceTriangleCount}.");
        }
        UploadModelMeshes();
        CenterCamera();

        _log.LogInformation(
            "Loaded NIF: {Shapes} shapes, {Vertices} source vertices, {Triangles} triangles.",
            shapeCount, sourceVertexCount, _indexCount / 3);

        Render();
    }

    /// <summary>
    /// Centers the camera on the combined bounds of all loaded scene objects and adjusts the zoom distance.
    /// </summary>
    public void CenterCamera()
    {
        if (!_initialized || _disposed || _sceneObjects.Count == 0)
            return;

        // Compute the combined bounding box of all scene objects in world space.
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);

        foreach (var obj in _sceneObjects)
        {
            foreach (var tri in obj.ModelTris)
            {
                Vector3 a = TransformPoint(obj, tri.A);
                Vector3 b = TransformPoint(obj, tri.B);
                Vector3 c = TransformPoint(obj, tri.C);

                min = Vector3.Min(min, a);
                min = Vector3.Min(min, b);
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, a);
                max = Vector3.Max(max, b);
                max = Vector3.Max(max, c);
            }
        }

        // Set camera target to the center of all bounds.
        _cameraTarget = (min + max) * 0.5f;

        // Compute the radius needed to frame all objects.
        Vector3 extent = (max - min) * 0.5f;
        float diagonal = extent.Length();
        _modelRadius = MathF.Max(diagonal, 0.01f);

        // Frame with padding based on field of view.
        _cameraDistance = _modelRadius / MathF.Sin(FieldOfView * 0.5f) * 1.5f;

        Render();
    }

    /// <summary>
    /// Changes camera angles only. The mesh buffers are not rebuilt while orbiting.
    /// </summary>
    public void Orbit(float deltaX, float deltaY)
    {
        if (!_initialized || _disposed)
            return;

        const float sensitivity = 0.003f;

        _orbitYaw -= deltaX * sensitivity;
        _orbitPitch = Math.Clamp(
            _orbitPitch + deltaY * sensitivity,
            -MathF.PI / 2f + 0.01f,
            MathF.PI / 2f - 0.01f);

        Render();
    }
    
    public void Pan(float deltaX, float deltaY)
    {
        if (!_initialized || _disposed)
            return;

        // Screen-space panning: deltaX moves left/right on screen, deltaY moves up/down on screen.
        // Compute the camera's right and up vectors in screen space.
        float cosPitch = MathF.Cos(_orbitPitch);
        Vector3 forward = Vector3.Normalize(new Vector3(
            cosPitch * MathF.Cos(_orbitYaw),
            cosPitch * MathF.Sin(_orbitYaw),
            MathF.Sin(_orbitPitch)));

        // Right vector is perpendicular to forward and world up (Z)
        Vector3 right = Vector3.Normalize(
            Vector3.Cross(forward, Vector3.UnitZ));

        // Up vector completes the orthogonal basis
        Vector3 up = Vector3.Normalize(
            Vector3.Cross(right, forward));

        // Pan scale proportional to camera distance, normalized by screen size
        float aspect = (float)_width / Math.Max(1u, _height);
        float panScale = _cameraDistance * 0.001f;

        // Pan in screen space: X moves along right, Y moves along up
        _cameraTarget += right * (deltaX * panScale);
        _cameraTarget += up * (deltaY * panScale);

        Render();
    }
    
    public void Zoom(float delta)
    {
        if (!_initialized || _disposed)
            return;

        // Positive delta zooms in; negative delta zooms out.
        _cameraDistance *= MathF.Exp(-delta * 0.1f);

        // Keep the camera from getting too close or too far away.
        _cameraDistance = Math.Clamp(
            _cameraDistance,
            _modelRadius * 0.05f,
            _modelRadius * 100f);

        Render();
    }

    public void Render()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
            return;

        _context!.OMSetRenderTargets(_renderTarget, _depthView);
        _context.RSSetViewport(new Viewport(0, 0, _width, _height));
        _context.ClearRenderTargetView(
            _renderTarget!, new Color4(0.04f, 0.04f, 0.07f, 1f));
        _context.ClearDepthStencilView(
            _depthView!, DepthStencilClearFlags.Depth, 1f, 0);

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(_inputLayout);
        _context.IASetVertexBuffer(0, _vertexBuffer, (uint)Marshal.SizeOf<Vertex>());
        _context.IASetIndexBuffer(_indexBuffer, Format.R32_UInt, 0);

        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_pixelShader);
        _context.PSSetSampler(0, _diffuseSampler);
        _context.VSSetConstantBuffer(0, _sceneBuffer);
        unsafe
        {
            float* blendFactor = stackalloc float[4];
            _context.OMSetBlendState(_alphaBlendState, blendFactor, uint.MaxValue);
        }
        _context.RSSetState(_rasterizerState);

        if (_indexCount > 0)
        {
            UpdateCameraConstants();
            foreach (var range in _drawRanges.Where(range => !range.IsTransparent)
                         .Concat(_drawRanges.Where(range => range.IsTransparent)))
            {
                _context.OMSetDepthStencilState(
                    range.IsTransparent ? _transparentDepthState : _depthState, 0);
                SetDiffuseTexture(range.TexturePath);
                _context.DrawIndexed(range.Count, range.Start, 0);
            }
        }

        _swapChain!.Present(1, PresentFlags.None);
    }

    public void Resize(uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized || width == 0 || height == 0)
            return;

        _context!.OMSetRenderTargets(
            Array.Empty<ID3D11RenderTargetView>(), null);

        _renderTarget?.Dispose();
        _renderTarget = null;
        _depthView?.Dispose();
        _depthView = null;
        _depthTexture?.Dispose();
        _depthTexture = null;

        _swapChain!.ResizeBuffers(
            0, width, height, Format.Unknown, SwapChainFlags.None);

        _width = width;
        _height = height;
        CreateRenderTargets(width, height);
        Render();
    }

    private void UploadModelMeshes()
    {
        var vertices = new List<Vertex>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);
        var indices = new List<uint>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);

        var objectsByTexture = _sceneObjects
            .GroupBy(obj => (TexturePath: obj.TexturePath ?? string.Empty, obj.Tint, obj.IsTransparent))
            .ToList();
        foreach (var obj in objectsByTexture.SelectMany(group => group))
        {
            _modelRadius = 0f;

            // Duplicate each face's vertices so every face has a flat normal.
            foreach (var tri in obj.ModelTris)
            {
                Vector3 a = TransformPoint(obj, tri.A);
                Vector3 b = TransformPoint(obj, tri.B);
                Vector3 c = TransformPoint(obj, tri.C);

                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f)
                    continue;

                Vector3 normal = Vector3.Normalize(cross);

                _modelRadius = MathF.Max(_modelRadius, a.Length());
                _modelRadius = MathF.Max(_modelRadius, b.Length());
                _modelRadius = MathF.Max(_modelRadius, c.Length());

                uint start = (uint)vertices.Count;
                vertices.Add(new Vertex(a, normal, tri.UvA * obj.UVScale + obj.UVOffset, obj.Tint));
                vertices.Add(new Vertex(b, normal, tri.UvB * obj.UVScale + obj.UVOffset, obj.Tint));
                vertices.Add(new Vertex(c, normal, tri.UvC * obj.UVScale + obj.UVOffset, obj.Tint));

                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            if (vertices.Count == 0)
                throw new InvalidOperationException("The NIF contained no renderable triangles.");

            _modelRadius = MathF.Max(_modelRadius, 0.01f);

            // Frame the model with a little padding.
            _cameraDistance = _modelRadius / MathF.Sin(FieldOfView * 0.5f) * 1.25f;

        }

        var newVertexBuffer = _device!.CreateBuffer(vertices.ToArray(), BindFlags.VertexBuffer);
        var newIndexBuffer = _device.CreateBuffer(indices.ToArray(), BindFlags.IndexBuffer);
        _context!.ClearState();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _vertexBuffer = newVertexBuffer;
        _indexBuffer = newIndexBuffer;
        _indexCount = (uint)indices.Count;
        _drawRanges.Clear();
        uint startIndex = 0;
        foreach (var group in objectsByTexture)
        {
            uint count = (uint)group.Sum(obj => obj.ModelTris.Count) * 3;
            _drawRanges.Add((group.Key.TexturePath, group.Key.Tint, group.Key.IsTransparent, startIndex, count));
            startIndex += count;
        }
    }

    private void SetDiffuseTexture(string? nifTexturePath)
    {
        _context!.PSSetShaderResource(0, _whiteTextureView);

        string relativePath = (nifTexturePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        string textureRelativePath = relativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : "textures/" + relativePath;
        if (_unavailableTexturePaths.Contains(textureRelativePath)) return;
        if (_textureCache.TryGetValue(textureRelativePath, out var cachedTexture))
        {
            _context.PSSetShaderResource(0, cachedTexture.View);
            return;
        }

        string loosePath = Path.Combine(
            _dataFolderPath ?? string.Empty,
            textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Stream? stream = File.Exists(loosePath)
            ? File.OpenRead(loosePath)
            : _bsaFileProvider.GetFileFromAny(textureRelativePath);
        if (stream is null)
        {
            _log.LogDebug("Texture {TexturePath} was not found", relativePath);
            _unavailableTexturePaths.Add(textureRelativePath);
            return;
        }

        using (stream)
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            byte[] bytes = memory.ToArray();
            if (bytes.Length < 128 || BitConverter.ToUInt32(bytes, 0) != 0x20534444)
            {
                _log.LogWarning("Texture {TexturePath} is not a supported DDS", relativePath);
                _unavailableTexturePaths.Add(textureRelativePath);
                return;
            }

            int height = checked((int)BitConverter.ToUInt32(bytes, 12));
            int width = checked((int)BitConverter.ToUInt32(bytes, 16));
            string fourCc = System.Text.Encoding.ASCII.GetString(bytes, 84, 4);
            Format format = fourCc switch
            {
                "DXT1" => Format.BC1_UNorm,
                "DXT3" => Format.BC2_UNorm,
                "DXT5" => Format.BC3_UNorm,
                _ => Format.Unknown
            };
            if (format == Format.Unknown || width <= 0 || height <= 0)
            {
                _log.LogWarning("DDS format {Format} for {TexturePath} is unsupported", fourCc, relativePath);
                _unavailableTexturePaths.Add(textureRelativePath);
                return;
            }

            int blockBytes = format == Format.BC1_UNorm ? 8 : 16;
            int rowPitch = Math.Max(1, (width + 3) / 4) * blockBytes;
            int dataSize = rowPitch * Math.Max(1, (height + 3) / 4);
            if (bytes.Length < 128 + dataSize)
            {
                _unavailableTexturePaths.Add(textureRelativePath);
                return;
            }

            var description = new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1,
                Format = format, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
            };
            unsafe
            {
                fixed (byte* textureData = &bytes[128])
                {
                    var initialData = new SubresourceData((IntPtr)textureData, (uint)rowPitch, (uint)dataSize);
                    var texture = _device!.CreateTexture2D(description, initialData);
                    var view = _device.CreateShaderResourceView(texture);
                    _textureCache.Add(textureRelativePath, (texture, view));
                    _context.PSSetShaderResource(0, view);
                }
            }
        }
    }

    private void CreateWhiteTexture()
    {
        byte[] pixel = [255, 255, 255, 255];
        var description = new Texture2DDescription
        {
            Width = 1, Height = 1, MipLevels = 1, ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
        };
        unsafe
        {
            fixed (byte* data = pixel)
            {
                var initialData = new SubresourceData((IntPtr)data, 4, 4);
                _whiteTexture = _device!.CreateTexture2D(description, initialData);
            }
        }
        _whiteTextureView = _device!.CreateShaderResourceView(_whiteTexture);
    }

    private void UpdateCameraConstants()
    {
        float aspect = (float)_width / Math.Max(1u, _height);

        float cosPitch = MathF.Cos(_orbitPitch);
        Vector3 cameraOffset = new(
            _cameraDistance * cosPitch * MathF.Cos(_orbitYaw),
            _cameraDistance * cosPitch * MathF.Sin(_orbitYaw),
            _cameraDistance * MathF.Sin(_orbitPitch));

        Vector3 cameraPosition = _cameraTarget + cameraOffset;

        Matrix4x4 view = Matrix4x4.CreateLookAt(
            cameraPosition,
            _cameraTarget,
            Vector3.UnitZ);

        float farPlane = MathF.Max(_cameraDistance + _modelRadius * 4f, 100f);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView, MathF.Max(aspect, 0.01f), NearPlane, farPlane);

        // System.Numerics uses row-vector composition; HLSL uses mul(vector, matrix).
        Matrix4x4 worldViewProjection = view * projection;

        var constants = new SceneConstants
        {
            WorldViewProjection = worldViewProjection,
            LightDirection = new Vector4(_lightDirection, 0f),
            CameraPosition = new Vector4(cameraPosition, 1f),
            AmbientLightColor = new Vector4(_ambientLightColor, 1f),
            DirectionalLightColor = new Vector4(_directionalLightColor, 1f)
        };

        _context!.UpdateSubresource(in constants, _sceneBuffer!);
    }

    private void ApplyCellLighting(ICellGetter cell, IGameEnvironment env)
    {
        var lighting = cell.Lighting;
        ILightingTemplateGetter? template = null;
        if (cell.LightingTemplate.TryResolve(env.LinkCache, out var resolvedTemplate))
            template = resolvedTemplate;

        if (lighting is null && template is null)
            return;

        bool inheritAmbient = lighting is null ||
            (lighting.Inherits & CellLighting.Inherit.AmbientColor) != 0;
        bool inheritDirectionalColor = lighting is null ||
            (lighting.Inherits & CellLighting.Inherit.DirectionalColor) != 0;
        bool inheritDirection = lighting is null ||
            (lighting.Inherits & CellLighting.Inherit.DirectionalRotation) != 0;

        DrawingColor ambientColor = inheritAmbient && template is not null
            ? template.AmbientColor
            : lighting?.AmbientColor ?? template?.AmbientColor ?? DrawingColor.FromArgb(64, 64, 64);
        DrawingColor directionalColor = inheritDirectionalColor && template is not null
            ? template.DirectionalColor
            : lighting?.DirectionalColor ?? template?.DirectionalColor ?? DrawingColor.White;

        int directionXY = inheritDirection && template is not null
            ? template.DirectionalRotationXY
            : lighting?.DirectionalRotationXY ?? template?.DirectionalRotationXY ?? 0;
        int directionZ = inheritDirection && template is not null
            ? template.DirectionalRotationZ
            : lighting?.DirectionalRotationZ ?? template?.DirectionalRotationZ ?? 0;

        _ambientLightColor = ToRgb(ambientColor);
        _directionalLightColor = ToRgb(directionalColor);

        // The directional rotations are azimuth and elevation in degrees.
        float azimuth = directionXY * (MathF.PI / 180f);
        float elevation = directionZ * (MathF.PI / 180f);
        _lightDirection = Vector3.Normalize(new Vector3(
            MathF.Cos(azimuth) * MathF.Cos(elevation),
            MathF.Sin(azimuth) * MathF.Cos(elevation),
            MathF.Sin(elevation)));

        _log.LogDebug(
            "Cell lighting: ambient={Ambient}, directional={Directional}, rotation=({RotationXY}, {RotationZ})",
            _ambientLightColor, _directionalLightColor, directionXY, directionZ);
    }

    private static Vector3 ToRgb(DrawingColor color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f);

    private void CreateRenderTargets(uint width, uint height)
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _renderTarget = _device!.CreateRenderTargetView(backBuffer);

        var depthDesc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D32_Float,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil
        };

        _depthTexture = _device.CreateTexture2D(depthDesc);
        _depthView = _device.CreateDepthStencilView(_depthTexture);

        _context!.RSSetViewport(new Viewport(0, 0, width, height));
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static Vector3 TransformPoint(SceneObject obj, Vector3 point)
    {
        // Skyrim REFR rotations are clockwise radians. System.Numerics uses
        // counterclockwise rotations and row-vector matrix composition.
        Matrix4x4 rotation =
            Matrix4x4.CreateRotationZ(-obj.Rotation.Z) *
            Matrix4x4.CreateRotationY(-obj.Rotation.Y) *
            Matrix4x4.CreateRotationX(-obj.Rotation.X);
        Vector3 nifPoint = Vector3.Transform(point, obj.NifTransform);
        return Vector3.Transform(nifPoint * obj.Scale, rotation) + obj.Position;
    }

    private static Matrix4x4 CreateNifTransform(Vector3 translation, NiflySharp.Structs.Matrix33 rotation, float scale)
    {
        var rotationMatrix = new Matrix4x4(
            rotation.M11, rotation.M12, rotation.M13, 0f,
            rotation.M21, rotation.M22, rotation.M23, 0f,
            rotation.M31, rotation.M32, rotation.M33, 0f,
            0f, 0f, 0f, 1f);

        return Matrix4x4.CreateScale(scale) * rotationMatrix * Matrix4x4.CreateTranslation(translation);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeResources();
        GC.SuppressFinalize(this);
    }

    private void DisposeResources()
    {
        _initialized = false;

        _context?.ClearState();
        _context?.Flush();

        _renderTarget?.Dispose();
        _renderTarget = null;

        _depthView?.Dispose();
        _depthView = null;
        _depthTexture?.Dispose();
        _depthTexture = null;

        _inputLayout?.Dispose();
        _inputLayout = null;

        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        _sceneBuffer?.Dispose();
        _sceneBuffer = null;

        _depthState?.Dispose();
        _depthState = null;
        _transparentDepthState?.Dispose();
        _transparentDepthState = null;
        _alphaBlendState?.Dispose();
        _alphaBlendState = null;
        _rasterizerState?.Dispose();
        _rasterizerState = null;
        _diffuseSampler?.Dispose();
        _diffuseSampler = null;
        foreach (var texture in _textureCache.Values)
        {
            texture.View.Dispose();
            texture.Texture.Dispose();
        }
        _textureCache.Clear();
        _whiteTextureView?.Dispose();
        _whiteTextureView = null;
        _whiteTexture?.Dispose();
        _whiteTexture = null;

        _vertexShader?.Dispose();
        _vertexShader = null;
        _pixelShader?.Dispose();
        _pixelShader = null;

        _swapChain?.Dispose();
        _swapChain = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
    }
}
