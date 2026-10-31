using System.Numerics;
using System.Runtime.InteropServices;
using BCnEncoder.Decoder;
using BCnEncoder.Shared.ImageFiles;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
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

public sealed class MainRenderer : IDisposable
{
    private readonly ILogger<MainRenderer> _log;
    private readonly CachedBSAFileProvider _bsaFileProvider;
    private readonly bool _previewMode;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _renderTarget;
    private ID3D11Texture2D? _depthTexture;
    private ID3D11DepthStencilView? _depthView;

    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _indexBuffer;
    private ID3D11Buffer? _selectionVertexBuffer;
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
    private readonly Dictionary<string, (ID3D11Texture2D Texture, ID3D11ShaderResourceView View)> _environmentTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unavailableTexturePaths = new(StringComparer.OrdinalIgnoreCase);
    private ID3D11Texture2D? _whiteTexture;
    private ID3D11ShaderResourceView? _whiteTextureView;
    private ID3D11Texture2D? _blackTexture;
    private ID3D11ShaderResourceView? _blackTextureView;

    private bool _initialized;
    private bool _disposed;
    private bool _bulkLoading;
    private readonly object _sceneObjectsLock = new();
    private string? _dataFolderPath;

    private uint _indexCount;
    private uint _selectionVertexCount;
    private uint? _previewGridIndexStart;
    private bool _showPreviewGrid;
    private uint _width;
    private uint _height;
    private readonly List<(string TexturePath, string EmissiveTexturePath, string EnvironmentMapPath, string EnvironmentMaskPath, Vector4 Tint, Vector4 Specular, Vector4 Emissive, float Glossiness, float EnvironmentMapScale, bool HasEmissiveMap, bool IsTransparent, Vector3 SortCenter, uint Start, uint Count)> _drawRanges = [];
    private Guid? _selectedObjectId;
    
    private readonly OrbitCamera _camera = new();
    private CellLightingState _cellLighting = new();
    private readonly List<PlacedPointLight> _placedLights = [];

    private const int MaxPointLights = 32;

    // private readonly List<(Vector3 A, Vector3 B, Vector3 C)> _modelTriangles = new();


    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Vector4 Tint;
        public Vector4 Specular;
        public Vector4 Emissive;
        public Vector4 Material;

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 tint,
            Vector4 specular, Vector4 emissive, float glossiness, bool hasEmissiveMap,
            bool isGrid = false, float environmentMapScale = 0f)
        {
            Position = position;
            Normal = normal;
            TexCoord = texCoord;
            Tint = tint;
            Specular = specular;
            Emissive = emissive;
            Material = new Vector4(glossiness, hasEmissiveMap ? 1f : 0f,
                isGrid ? 1f : 0f, environmentMapScale);
        }
    }

    // Must match the HLSL constant-buffer layout (1264 bytes).
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct SceneConstants
    {
        public Matrix4x4 WorldViewProjection;
        public Vector4 LightDirection;
        public Vector4 CameraPosition;
        public Vector4 AmbientLightColor;
        public Vector4 DirectionalLightColor;
        public fixed float AmbientLightCube[24];
        public Vector4 PointLightCount;
        public fixed float PointLightPositionRadius[MaxPointLights * 4];
        public fixed float PointLightColorFalloff[MaxPointLights * 4];
    }

    private readonly record struct PlacedPointLight(
        Vector3 Position, Vector3 Color, float Radius, float FalloffExponent);

    public MainRenderer(ILogger<MainRenderer> log, string dataDirPath)
        : this(log, dataDirPath, false)
    {
    }

    internal MainRenderer(ILogger<MainRenderer> log, string dataDirPath, bool previewMode)
    {
        _log = log;
        _previewMode = previewMode;
        _bsaFileProvider = new(_log);
        
        
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Meshes0.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Meshes1.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures0.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures1.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures2.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures3.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures4.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures5.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures6.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures7.bsa"));
        _bsaFileProvider.OpenArchive(Path.Combine(dataDirPath, "Skyrim - Textures8.bsa"));
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

            var featureLevels = new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0
            };

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

            var shaderPath = Path.Combine(
                AppContext.BaseDirectory, "Shaders", "Model.hlsl");

            var vs = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_4_0");
            var ps = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_4_0");

            _vertexShader = _device!.CreateVertexShader(vs.Span);
            _pixelShader = _device.CreatePixelShader(ps.Span);

            var inputElements = new InputElementDescription[]
            {
                new("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
                new("COLOR", 0, Format.R32G32B32A32_Float, 32, 0),
                new("TEXCOORD", 2, Format.R32G32B32A32_Float, 48, 0),
                new("TEXCOORD", 3, Format.R32G32B32A32_Float, 64, 0),
                new("TEXCOORD", 4, Format.R32G32B32A32_Float, 80, 0)
            };

            _inputLayout = _device.CreateInputLayout(inputElements, vs.Span);

            _sceneBuffer = _device.CreateBuffer(
                new SceneConstants[1], BindFlags.ConstantBuffer);
            _diffuseSampler = _device.CreateSamplerState(new SamplerDescription
            {
                Filter = Filter.Anisotropic,
                MaxAnisotropy = 16,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunc = ComparisonFunction.Never,
                MinLOD = 0,
                MaxLOD = float.MaxValue
            });
            CreateWhiteTexture();
            CreateBlackTexture();

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
                CullMode = _previewMode ? CullMode.None : CullMode.Back,
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
        // A renderer instance can be reused when the selected cell changes. Drop
        // all cell-owned CPU and GPU state before adding the next cell's objects.
        _context?.ClearState();
        _sceneObjects.Clear();
        _placedLights.Clear();
        _drawRanges.Clear();
        _selectedObjectId = null;
        _indexCount = 0;
        _selectionVertexCount = 0;
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        _selectionVertexBuffer?.Dispose();
        _selectionVertexBuffer = null;

        _dataFolderPath = env.DataFolderPath;
        _log.LogInformation("Loading cell {Name}", cell.Name?.String);
        _cellLighting = CellLightingState.Resolve(cell, env, _log);

        var loadJobs = new List<Action>();
        foreach (var p in cell.Temporary.Concat(cell.Persistent))
        {
            if (p is not IPlacedObjectGetter po || !po.Base.TryResolve(env.LinkCache, out var b))
                continue;

            if (b is ILightGetter light && light.Radius > 0)
            {
                var lightPosition = po.Placement?.Position;
                _placedLights.Add(new PlacedPointLight(
                    new Vector3(lightPosition?.X ?? 0f, lightPosition?.Y ?? 0f, lightPosition?.Z ?? 0f),
                    ToLinearRgb(light.Color), light.Radius, MathF.Max(light.FalloffExponent, 0.1f)));
            }

            var placement = po.Placement;
            var position = new Vector3(placement?.Position.X ?? 0, placement?.Position.Y ?? 0, placement?.Position.Z ?? 0);
            var rotation = new Vector3(placement?.Rotation.X ?? 0, placement?.Rotation.Y ?? 0, placement?.Rotation.Z ?? 0);
            if (b is ILightGetter placedLight)
            {
                var lightModelPath = placedLight.Model?.File.GivenPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lightModelPath))
                    loadJobs.Add(() => LoadModelAsset(lightModelPath, position, rotation, po.Scale ?? 1f, env, false, p));
                loadJobs.Add(() => LoadLightMarker(placedLight, position, rotation, env));
            }
            else if (b is IModeledGetter stat)
            {
                var modelPath = stat.Model?.File.GivenPath ?? string.Empty;
                var isXMarker = IsXMarkerEditorId(b.EditorID);
                var scale = po.Scale ?? 1f;
                loadJobs.Add(() => LoadModelAsset(modelPath, position, rotation, scale, env, isXMarker, p));
            }
        }

        _bulkLoading = true;
        try
        {
            Parallel.ForEach(loadJobs, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, job => job());
        }
        finally
        {
            _bulkLoading = false;
        }

        if (_sceneObjects.Count > 0)
        {
            UploadModelMeshes();
            CenterCamera();
        }
        else
        {
            Render();
        }
    }

    private static bool IsXMarkerModel(string modelPath) =>
        Path.GetFileName(modelPath.Replace('\\', '/'))
            .StartsWith("markerx", StringComparison.OrdinalIgnoreCase);

    private static bool IsXMarkerEditorId(string? editorId) =>
        editorId?.StartsWith("XMarker", StringComparison.OrdinalIgnoreCase) == true;

    private void LoadModelAsset(string relativeModelPath, 
        Vector3 position, 
        Vector3 rotation, 
        float scale,
        IGameEnvironment env, 
        bool isXMarker = false,
        IPlacedGetter? objectRef = null)
    {
        if (string.IsNullOrWhiteSpace(relativeModelPath))
            return;

        var normalizedPath = relativeModelPath.Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        var loosePath = Path.Combine(env.DataFolderPath, "meshes", normalizedPath);
        if (File.Exists(loosePath))
        {
            LoadModel(loosePath, position, rotation, scale,
                isXMarker || IsXMarkerModel(relativeModelPath), Vector4.One, objectRef);
            return;
        }

        var archivePath = Path.Combine("meshes", relativeModelPath.Replace('\\', '/'));
        var stream = _bsaFileProvider.GetFileFromAny(archivePath);
        if (stream is not null)
            LoadModel(stream, position, rotation, scale,
                isXMarker || IsXMarkerModel(relativeModelPath), Vector4.One, objectRef);
    }

    private void LoadLightMarker(ILightGetter light, Vector3 position, Vector3 rotation,
        IGameEnvironment env)
    {
        string markerFile;
        float markerScale;
        var markerRotation = Vector3.Zero;
        var flags = light.Flags;

        if (flags.HasFlag(Light.Flag.ShadowHemisphere))
        {
            markerFile = "marker_halfomni.nif";
            markerScale = 0.25f;
            markerRotation.Y = MathF.PI / 2f;
        }
        else if (flags.HasFlag(Light.Flag.ShadowSpotlight))
        {
            markerFile = "marker_spotlight.nif";
            markerScale = 1f;
            markerRotation = new Vector3(-MathF.PI, 0f, -MathF.PI);
        }
        else if (flags.HasFlag(Light.Flag.SpotLight))
        {
            markerFile = "marker_spotlightnoshadow.nif";
            markerScale = 1f;
            markerRotation = new Vector3(-MathF.PI, 0f, -MathF.PI);
        }
        else if (flags.HasFlag(Light.Flag.ShadowOmnidirectional))
        {
            markerFile = "marker_lightshadow.nif";
            markerScale = 0.25f;
        }
        else
        {
            markerFile = "marker_light.nif";
            markerScale = 0.25f;
        }

        var combinedRotation = rotation + markerRotation;
        var hasTranslucentShell = markerFile is "marker_lightshadow.nif"
            or "marker_spotlight.nif" or "marker_halfomni.nif";
        var markerTint = new Vector4(ToLinearRgb(light.Color), hasTranslucentShell ? 2.5f : 1f);
        var markerPath = Path.Combine("markers", markerFile);
        var loosePath = Path.Combine(env.DataFolderPath, "meshes", markerPath);
        if (File.Exists(loosePath))
        {
            LoadModel(loosePath, position, combinedRotation, markerScale, false, markerTint, null);
            return;
        }

        var archiveCandidates = new[]
        {
            $"meshes/{markerPath.Replace('\\', '/')}",
            markerPath.Replace('\\', '/'),
            $"meshes/{markerFile}"
        };
        foreach (var archiveCandidate in archiveCandidates)
        {
            var stream = _bsaFileProvider.GetFileFromAny(archiveCandidate);
            if (stream is null)
                continue;

            LoadModel(stream, position, combinedRotation, markerScale, false, markerTint, null);
            return;
        }

        var filenameMatch = _bsaFileProvider.GetFileFromAnyByFileName(markerFile);
        if (filenameMatch is not null)
        {
            LoadModel(filenameMatch, position, combinedRotation, markerScale, false, markerTint, null);
            return;
        }
    }

    public void LoadModel(string path, Vector3 position, IPlacedGetter? objectRef)
        => LoadModel(path, position, Vector3.Zero, 1f, objectRef);

    internal void LoadPreviewModel(
        string path,
        string dataFolderPath,
        IList<ITextureSetGetter?>? textureSetOverrides = null)
    {
        if (!_previewMode)
            throw new InvalidOperationException("Preview models require a preview renderer.");

        _dataFolderPath = dataFolderPath;
        ClearPreviewModel();

        var normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (normalizedPath.StartsWith($"meshes{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            normalizedPath = normalizedPath[("meshes".Length + 1)..];
        var loosePath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(dataFolderPath, "meshes", normalizedPath);
        if (File.Exists(loosePath))
        {
            using var looseStream = File.OpenRead(loosePath);
            LoadModel(looseStream, Vector3.Zero, Vector3.Zero, 1f, false, Vector4.One, null,
                textureSetOverrides);
            return;
        }

        var archivePath = normalizedPath.Replace(Path.DirectorySeparatorChar, '/');
        if (!archivePath.StartsWith("meshes/", StringComparison.OrdinalIgnoreCase))
            archivePath = "meshes/" + archivePath;
        using var stream = _bsaFileProvider.GetFileFromAny(archivePath)
            ?? throw new FileNotFoundException("The preview NIF was not found in the Data\\Meshes folder or loaded archives.", path);
        LoadModel(stream, Vector3.Zero, Vector3.Zero, 1f, false, Vector4.One, null,
            textureSetOverrides);
    }

    internal void ClearPreviewModel()
    {
        if (!_previewMode)
            throw new InvalidOperationException("Preview models require a preview renderer.");

        _context?.ClearState();
        _sceneObjects.Clear();
        _drawRanges.Clear();
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        if (_initialized)
        {
            UploadModelMeshes();
            Render();
        }
    }

    internal void SetPreviewGridVisible(bool visible)
    {
        if (!_previewMode)
            throw new InvalidOperationException("The preview grid is only available in the preview renderer.");

        if (_showPreviewGrid == visible)
            return;

        _showPreviewGrid = visible;
        Render();
    }

    internal bool IsPreviewGridVisible => _showPreviewGrid;

    public void LoadModel(string path, Vector3 position, Vector3 rotation, float scale, IPlacedGetter? objectRef)
        => LoadModel(path, position, rotation, scale, IsXMarkerModel(path), Vector4.One, objectRef);

    private void LoadModel(string path, Vector3 position, Vector3 rotation, float scale,
        bool isXMarker, Vector4 vertexColorMultiplier, IPlacedGetter? objectRef)
    {
        if (File.Exists(path))
        {
            var fstream = File.OpenRead(path);
            LoadModel(fstream, position, rotation, scale, isXMarker, vertexColorMultiplier, objectRef);
        }
    }
    
    public void LoadModel(Stream fileStream, Vector3 position, IPlacedGetter? objectRef)
        => LoadModel(fileStream, position, Vector3.Zero, 1f, objectRef);

    public void LoadModel(Stream fileStream, Vector3 position, Vector3 rotation, float scale, IPlacedGetter? objectRef)
        => LoadModel(fileStream, position, rotation, scale, false, Vector4.One, objectRef);

    private void LoadModel(Stream fileStream,
        Vector3 position,
        Vector3 rotation,
        float scale,
        bool isXMarker,
        Vector4 vertexColorMultiplier,
        IPlacedGetter? objectRef,
        IList<ITextureSetGetter?>? textureSetOverrides = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
            throw new InvalidOperationException("Call Initialize before LoadModel.");

        var nif = new NifFile();
        var result = nif.Load(fileStream);

        if (result != 0)
            throw new InvalidOperationException($"Failed to load NIF (error {result}).");

        // _modelTriangles.Clear();

        var shapeCount = 0;
        var renderableTriangleCount = 0;
        var sourceVertexCount = 0;
        var sourceTriangleCount = 0;

        var nifTransforms = new Dictionary<INiShape, Matrix4x4>(ReferenceEqualityComparer.Instance);
        void CollectTransforms(NiAVObject node, Matrix4x4 parentTransform)
        {
            var localTransform = CreateNifTransform(node.Translation, node.Rotation, node.Scale);
            var accumulatedTransform = localTransform * parentTransform;
            if (node is INiShape nodeShape)
                nifTransforms[nodeShape] = accumulatedTransform;

            if (node is NiNode niNode)
            {
                foreach (var childIndex in niNode.Children.Indices)
                {
                    if (nif.GetBlock(childIndex) is NiAVObject child)
                        CollectTransforms(child, accumulatedTransform);
                }
            }
        }

        foreach (var rootNode in nif.GetRootNodes())
            CollectTransforms(rootNode, Matrix4x4.Identity);

        var loadedObjectId = Guid.NewGuid();
        var shapeIndex = 0;
        foreach (var shape in nif.GetShapes())
        {
            var currentShapeIndex = shapeIndex++;
            shapeCount++;
            var obj = new SceneObject
            {
                Id = loadedObjectId,
                ModelTris = [],
                Position = position,
                BoundsMin = new Vector3(float.PositiveInfinity),
                BoundsMax = new Vector3(float.NegativeInfinity),
                LocalBoundsMin = new Vector3(float.PositiveInfinity),
                LocalBoundsMax = new Vector3(float.NegativeInfinity),
                Rotation = rotation,
                Scale = scale,
                NifTransform = nifTransforms.GetValueOrDefault(
                    shape, CreateNifTransform(shape.Translation, shape.Rotation, shape.Scale)),
                UVScale = Vector2.One,
                Tint = Vector4.One,
                VertexColorMultiplier = vertexColorMultiplier,
                Specular = new Vector4(1f, 1f, 1f, 0.04f),
                Emissive = Vector4.Zero,
                EmissiveTexturePath = string.Empty,
                EnvironmentMapPath = string.Empty,
                EnvironmentMaskPath = string.Empty,
                Glossiness = 32f,
                EnvironmentMapScale = 0f,
                ObjectRef = objectRef
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
                    var effectTexturePath = effectShader.SourceTexture.Content;
                    if (isXMarker && alphaProperty is not null && string.IsNullOrWhiteSpace(effectTexturePath))
                    {
                        // Creation Kit displays textureless effect-shader marker meshes as red translucent proxies.
                        obj.Tint = new Vector4(1f, 0f, 0f, 0.4f);
                        obj.IsTransparent = true;
                    }
                    else
                    {
                        var color = effectShader.BaseColor;
                        var falloffOpacity = (effectShader.FalloffStartOpacity + effectShader.FalloffStopOpacity) * 0.5f;
                        var alpha = float.IsFinite(falloffOpacity) && falloffOpacity > 0f
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
                else if (shader is BSLightingShaderProperty lightingShader)
                {
                    var specularColor = ToLinearRgb(lightingShader.SpecularColor);
                    var specularStrength = float.IsFinite(lightingShader.SpecularStrength)
                        ? MathF.Max(lightingShader.SpecularStrength, 0f)
                        : 0f;
                    obj.Specular = new Vector4(specularColor, specularStrength);

                    var emissiveColor = ToLinearRgb(lightingShader.EmissiveColor);
                    var emissiveMultiple = float.IsFinite(lightingShader.EmissiveMultiple)
                        ? MathF.Max(lightingShader.EmissiveMultiple, 0f)
                        : 0f;
                    obj.Emissive = new Vector4(emissiveColor, emissiveMultiple);
                    obj.EnvironmentMapScale = lightingShader.HasEnvironmentMapping &&
                                              float.IsFinite(lightingShader.EnvironmentMapScale)
                        ? MathF.Max(lightingShader.EnvironmentMapScale, 0f)
                        : 0f;
                    obj.Glossiness = float.IsFinite(lightingShader.Glossiness)
                        ? Math.Clamp(lightingShader.Glossiness, 1f, 256f)
                        : 32f;

                    var opacity = float.IsFinite(lightingShader.Alpha)
                        ? Math.Clamp(lightingShader.Alpha, 0f, 1f)
                        : 1f;
                    obj.Tint.W *= opacity;
                    obj.IsTransparent |= opacity < 0.999f;
                }
            }

            Vector3[] positions;
            Vector2[] uvs;
            Vector4[] vertexColors;
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
                        if (textureSet.Textures.Count >= 3)
                            obj.EmissiveTexturePath = textureSet.Textures[2].Content;
                        if (textureSet.Textures.Count >= 5)
                            obj.EnvironmentMapPath = textureSet.Textures[4].Content;
                        if (textureSet.Textures.Count >= 6)
                            obj.EnvironmentMaskPath = textureSet.Textures[5].Content;
                    }

                }
                
                positions = bsShape.VertexPositions.ToArray();
                uvs = bsShape.UVs.Select(uv => new Vector2(uv.U, uv.V)).ToArray();
                vertexColors = bsShape.VertexColors
                    .Select(color => new Vector4(color.R, color.G, color.B, color.A)).ToArray();
                triangles = bsShape.Triangles;
            }
            else
            {
                var data = shape.GeometryData;
                if (data == null)
                    continue;

                positions = data.Vertices?.ToArray() ?? Array.Empty<Vector3>();
                uvs = [];
                vertexColors = data.VertexColors?
                    .Select(color => new Vector4(color.R, color.G, color.B, color.A))
                    .ToArray() ?? Array.Empty<Vector4>();
                triangles = data.Triangles ?? Enumerable.Empty<Triangle>();
            }

            if (textureSetOverrides is not null && currentShapeIndex < textureSetOverrides.Count)
            {
                var textureSetOverride = textureSetOverrides[currentShapeIndex];
                if (textureSetOverride is not null)
                {
                    obj.TexturePath = textureSetOverride.Diffuse?.GivenPath ?? string.Empty;
                    obj.EmissiveTexturePath = textureSetOverride.GlowOrDetailMap?.GivenPath ?? string.Empty;
                    obj.EnvironmentMapPath = textureSetOverride.Environment?.GivenPath ?? string.Empty;
                    obj.EnvironmentMaskPath = textureSetOverride.EnvironmentMaskOrSubsurfaceTint?.GivenPath ?? string.Empty;
                }
            }

            sourceVertexCount += positions.Length;
            var vertexNormals = new Vector3[positions.Length];
            foreach (var tri in triangles)
            {
                var i0 = tri.V1;
                var i1 = tri.V2;
                var i2 = tri.V3;
                if ((uint)i0 >= (uint)positions.Length ||
                    (uint)i1 >= (uint)positions.Length ||
                    (uint)i2 >= (uint)positions.Length)
                    continue;

                var a = positions[i0];
                var b = positions[i1];
                var c = positions[i2];
                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                    continue;

                var weightedNormal = Vector3.Cross(b - a, c - a);
                if (weightedNormal.LengthSquared() < 1e-12f)
                    continue;

                vertexNormals[i0] += weightedNormal;
                vertexNormals[i1] += weightedNormal;
                vertexNormals[i2] += weightedNormal;
            }
            for (var i = 0; i < vertexNormals.Length; i++)
            {
                if (vertexNormals[i].LengthSquared() > 1e-12f)
                    vertexNormals[i] = Vector3.Normalize(vertexNormals[i]);
            }

            foreach (var tri in triangles)
            {
                sourceTriangleCount++;

                var i0 = tri.V1;
                var i1 = tri.V2;
                var i2 = tri.V3;

                if ((uint)i0 >= (uint)positions.Length ||
                    (uint)i1 >= (uint)positions.Length ||
                    (uint)i2 >= (uint)positions.Length)
                    continue;

                var a = positions[i0];
                var b = positions[i1];
                var c = positions[i2];

                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                    continue;

                var cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f)
                    continue;

                var uvA = (uint)i0 < (uint)uvs.Length ? uvs[i0] : Vector2.Zero;
                var uvB = (uint)i1 < (uint)uvs.Length ? uvs[i1] : Vector2.Zero;
                var uvC = (uint)i2 < (uint)uvs.Length ? uvs[i2] : Vector2.Zero;
                var colorA = (uint)i0 < (uint)vertexColors.Length ? vertexColors[i0] : Vector4.One;
                var colorB = (uint)i1 < (uint)vertexColors.Length ? vertexColors[i1] : Vector4.One;
                var colorC = (uint)i2 < (uint)vertexColors.Length ? vertexColors[i2] : Vector4.One;
                if (isXMarker)
                {
                    // Proxy opacity is specified by the XMarker effect shader tint.
                    colorA.W = colorB.W = colorC.W = 1f;
                }
                obj.IsTransparent |= colorA.W < 0.999f || colorB.W < 0.999f || colorC.W < 0.999f;
                var fallbackNormal = Vector3.Normalize(cross);
                var normalA = vertexNormals[i0].LengthSquared() > 1e-12f ? vertexNormals[i0] : fallbackNormal;
                var normalB = vertexNormals[i1].LengthSquared() > 1e-12f ? vertexNormals[i1] : fallbackNormal;
                var normalC = vertexNormals[i2].LengthSquared() > 1e-12f ? vertexNormals[i2] : fallbackNormal;
                obj.ModelTris.Add((a, b, c, uvA, uvB, uvC,
                    normalA, normalB, normalC, colorA, colorB, colorC));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, obj.TransformPoint(a));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, obj.TransformPoint(b));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, obj.TransformPoint(c));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, obj.TransformPoint(a));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, obj.TransformPoint(b));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, obj.TransformPoint(c));
                obj.LocalBoundsMin = Vector3.Min(obj.LocalBoundsMin, Vector3.Transform(a, obj.NifTransform) * obj.Scale);
                obj.LocalBoundsMin = Vector3.Min(obj.LocalBoundsMin, Vector3.Transform(b, obj.NifTransform) * obj.Scale);
                obj.LocalBoundsMin = Vector3.Min(obj.LocalBoundsMin, Vector3.Transform(c, obj.NifTransform) * obj.Scale);
                obj.LocalBoundsMax = Vector3.Max(obj.LocalBoundsMax, Vector3.Transform(a, obj.NifTransform) * obj.Scale);
                obj.LocalBoundsMax = Vector3.Max(obj.LocalBoundsMax, Vector3.Transform(b, obj.NifTransform) * obj.Scale);
                obj.LocalBoundsMax = Vector3.Max(obj.LocalBoundsMax, Vector3.Transform(c, obj.NifTransform) * obj.Scale);
            }

            if (obj.ModelTris.Count > 0)
            {
                renderableTriangleCount += obj.ModelTris.Count;
                lock (_sceneObjectsLock)
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
        if (!_bulkLoading)
        {
            UploadModelMeshes();
            CenterCamera();
        }

        _log.LogTrace(
            "Loaded NIF: {Shapes} shapes, {Vertices} source vertices, {Triangles} triangles.",
            shapeCount, sourceVertexCount, renderableTriangleCount);

        if (!_bulkLoading)
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
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);

        foreach (var obj in _sceneObjects)
        {
            foreach (var tri in obj.ModelTris)
            {
                var a = obj.TransformPoint(tri.A);
                var b = obj.TransformPoint(tri.B);
                var c = obj.TransformPoint(tri.C);

                min = Vector3.Min(min, a);
                min = Vector3.Min(min, b);
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, a);
                max = Vector3.Max(max, b);
                max = Vector3.Max(max, c);
            }
        }

        // Set camera target to the center of all bounds.
        _camera.Frame(min, max);

        Render();
    }

    /// <summary>
    /// Changes camera angles only. The mesh buffers are not rebuilt while orbiting.
    /// </summary>
    public void Orbit(float deltaX, float deltaY)
    {
        if (!_initialized || _disposed)
            return;

        _camera.Orbit(deltaX, deltaY);

        Render();
    }
    
    public void Pan(float deltaX, float deltaY)
    {
        if (!_initialized || _disposed)
            return;

        _camera.Pan(deltaX, deltaY);

        Render();
    }
    
    public void Zoom(float delta)
    {
        if (!_initialized || _disposed)
            return;

        _camera.Zoom(delta);

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
            _renderTarget!, _previewMode
                ? new Color4(0.34f, 0.35f, 0.36f, 1f)
                : new Color4(0.04f, 0.04f, 0.07f, 1f));
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
        _context.PSSetConstantBuffer(0, _sceneBuffer);
        unsafe
        {
            float* blendFactor = stackalloc float[4];
            _context.OMSetBlendState(_alphaBlendState, blendFactor, uint.MaxValue);
        }
        _context.RSSetState(_rasterizerState);

        if (_indexCount > 0)
        {
            UpdateCameraConstants();
            var cameraPosition = _camera.Position;
            foreach (var range in _drawRanges.Where(range => !range.IsTransparent &&
                                                              (_showPreviewGrid || range.Start != _previewGridIndexStart))
                         .Concat(_drawRanges.Where(range => range.IsTransparent)
                             .Where(range => _showPreviewGrid || range.Start != _previewGridIndexStart)
                             .OrderByDescending(range => Vector3.DistanceSquared(range.SortCenter, cameraPosition))))
            {
                _context.OMSetDepthStencilState(
                    range.IsTransparent ? _transparentDepthState : _depthState, 0);
                SetTexture(range.TexturePath, 0);
                SetTexture(range.EmissiveTexturePath, 1);
                SetTexture(range.EnvironmentMaskPath, 2);
                SetEnvironmentMap(range.EnvironmentMapPath, 3);
                _context.DrawIndexed(range.Count, range.Start, 0);
            }

            DrawSelectionBounds();
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
        _previewGridIndexStart = null;
        var vertices = new List<Vertex>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);
        var indices = new List<uint>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);

        var objectsByTexture = _sceneObjects
            .GroupBy(obj => (TexturePath: obj.TexturePath ?? string.Empty,
                EmissiveTexturePath: obj.EmissiveTexturePath ?? string.Empty,
                EnvironmentMapPath: obj.EnvironmentMapPath ?? string.Empty,
                EnvironmentMaskPath: obj.EnvironmentMaskPath ?? string.Empty, obj.Tint,
                obj.Specular, obj.Emissive, obj.Glossiness,
                obj.EnvironmentMapScale,
                HasEmissiveMap: !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), obj.IsTransparent))
            .ToList();
        var drawRanges = new List<(string TexturePath, string EmissiveTexturePath, string EnvironmentMapPath,
            string EnvironmentMaskPath, Vector4 Tint, Vector4 Specular, Vector4 Emissive,
            float Glossiness, float EnvironmentMapScale, bool HasEmissiveMap,
            bool IsTransparent, Vector3 SortCenter, uint Start, uint Count)>();
        foreach (var obj in objectsByTexture.SelectMany(group => group))
        {
            var objectStartIndex = (uint)indices.Count;
            var sortCenter = Vector3.Zero;
            var sortPointCount = 0;
            var modelRadius = 0f;

            // Duplicate each face's vertices so every face has a flat normal.
            foreach (var tri in obj.ModelTris)
            {
                var a = obj.TransformPoint(tri.A);
                var b = obj.TransformPoint(tri.B);
                var c = obj.TransformPoint(tri.C);
                sortCenter += a + b + c;
                sortPointCount += 3;

                var cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f)
                    continue;

                modelRadius = MathF.Max(modelRadius, a.Length());
                modelRadius = MathF.Max(modelRadius, b.Length());
                modelRadius = MathF.Max(modelRadius, c.Length());

                var start = (uint)vertices.Count;
                vertices.Add(new Vertex(a, obj.TransformNormal(tri.NormalA), tri.UvA * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorA,
                    obj.Specular, obj.Emissive, obj.Glossiness,
                    !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), false, obj.EnvironmentMapScale));
                vertices.Add(new Vertex(b, obj.TransformNormal(tri.NormalB), tri.UvB * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorB,
                    obj.Specular, obj.Emissive, obj.Glossiness,
                    !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), false, obj.EnvironmentMapScale));
                vertices.Add(new Vertex(c, obj.TransformNormal(tri.NormalC), tri.UvC * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorC,
                    obj.Specular, obj.Emissive, obj.Glossiness,
                    !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), false, obj.EnvironmentMapScale));

                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            var objectIndexCount = (uint)indices.Count - objectStartIndex;
            if (objectIndexCount > 0)
            {
                drawRanges.Add((obj.TexturePath ?? string.Empty, obj.EmissiveTexturePath ?? string.Empty,
                    obj.EnvironmentMapPath ?? string.Empty, obj.EnvironmentMaskPath ?? string.Empty,
                    obj.Tint, obj.Specular, obj.Emissive, obj.Glossiness, obj.EnvironmentMapScale,
                    !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), obj.IsTransparent,
                    sortPointCount > 0 ? sortCenter / sortPointCount : obj.Position,
                    objectStartIndex, objectIndexCount));
            }

            _camera.FrameRadius(modelRadius);

        }

        if (_previewMode)
        {
            var center = _camera.Target;
            var floor = _sceneObjects.Count == 0
                ? 0f
                : _sceneObjects.Min(obj => obj.BoundsMin.Z)
                  - MathF.Max((_sceneObjects.Max(obj => obj.BoundsMax.Z) - _sceneObjects.Min(obj => obj.BoundsMin.Z)) * 0.01f, 0.01f)
                  + 0.002f;
            var indexStart = (uint)indices.Count;
            _previewGridIndexStart = indexStart;
            const float halfSize = 100000f;
            var first = (uint)vertices.Count;
            var gridTint = Vector4.One;
            var gridMaterial = Vector4.Zero;
            vertices.Add(new Vertex(new Vector3(center.X - halfSize, center.Y - halfSize, floor),
                Vector3.UnitZ, Vector2.Zero, gridTint, gridMaterial, gridMaterial, 1f, false, true));
            vertices.Add(new Vertex(new Vector3(center.X + halfSize, center.Y - halfSize, floor),
                Vector3.UnitZ, Vector2.Zero, gridTint, gridMaterial, gridMaterial, 1f, false, true));
            vertices.Add(new Vertex(new Vector3(center.X + halfSize, center.Y + halfSize, floor),
                Vector3.UnitZ, Vector2.Zero, gridTint, gridMaterial, gridMaterial, 1f, false, true));
            vertices.Add(new Vertex(new Vector3(center.X - halfSize, center.Y + halfSize, floor),
                Vector3.UnitZ, Vector2.Zero, gridTint, gridMaterial, gridMaterial, 1f, false, true));
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 3);
            drawRanges.Add((string.Empty, string.Empty, string.Empty, string.Empty,
                gridTint, gridMaterial, gridMaterial, 1f, 0f, false, false,
                new Vector3(center.X, center.Y, floor), indexStart, 6));
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
        _drawRanges.AddRange(drawRanges);
    }

    private void SetTexture(string? nifTexturePath, uint slot)
    {
        // A missing environment mask should leave the cubemap unmasked, not hide it.
        _context!.PSSetShaderResource(
            slot, slot is 0 or 2 ? _whiteTextureView : _blackTextureView);

        var relativePath = (nifTexturePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var textureRelativePath = relativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : "textures/" + relativePath;
        if (_unavailableTexturePaths.Contains(textureRelativePath)) return;
        if (_textureCache.TryGetValue(textureRelativePath, out var cachedTexture))
        {
            _context.PSSetShaderResource(slot, cachedTexture.View);
            return;
        }

        var loosePath = Path.Combine(
            _dataFolderPath ?? string.Empty,
            textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var stream = File.Exists(loosePath)
            ? File.OpenRead(loosePath)
            : _bsaFileProvider.GetFileFromAny(textureRelativePath);
        if (stream is null)
        {
            _log.LogDebug("Texture {TexturePath} was not found", relativePath);
            _unavailableTexturePaths.Add(textureRelativePath);
            return;
        }

        using (stream)
        {
            var dds = DdsFile.Load(stream);
            var decoded = new BcDecoder().Decode2D(dds);
            var decodedPixels = decoded.Span;
            if (decodedPixels.Width <= 0 || decodedPixels.Height <= 0)
            {
                _log.LogWarning("Texture {TexturePath} has invalid dimensions", relativePath);
                _unavailableTexturePaths.Add(textureRelativePath);
                return;
            }

            var width = decodedPixels.Width;
            var height = decodedPixels.Height;
            var pixels = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = decodedPixels[y, x];
                    var offset = (y * width + x) * 4;
                    pixels[offset] = pixel.r;
                    pixels[offset + 1] = pixel.g;
                    pixels[offset + 2] = pixel.b;
                    pixels[offset + 3] = pixel.a;
                }
            }

            var description = new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
            };
            unsafe
            {
                fixed (byte* textureData = pixels)
                {
                    var initialData = new SubresourceData((IntPtr)textureData, (uint)(width * 4), (uint)pixels.Length);
                    var texture = _device!.CreateTexture2D(description, [initialData]);
                    var view = _device.CreateShaderResourceView(texture);
                    _textureCache.Add(textureRelativePath, (texture, view));
                    _context.PSSetShaderResource(slot, view);
                }
            }
        }
    }

    private void SetEnvironmentMap(string? nifTexturePath, uint slot)
    {
        var relativePath = (nifTexturePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            _context!.PSSetShaderResource(slot, null!);
            return;
        }

        var textureRelativePath = relativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : "textures/" + relativePath;
        if (_environmentTextureCache.TryGetValue(textureRelativePath, out var cachedTexture))
        {
            _context!.PSSetShaderResource(slot, cachedTexture.View);
            return;
        }

        var loosePath = Path.Combine(
            _dataFolderPath ?? string.Empty,
            textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var stream = File.Exists(loosePath)
            ? File.OpenRead(loosePath)
            : _bsaFileProvider.GetFileFromAny(textureRelativePath);
        if (stream is null)
        {
            _log.LogDebug("Environment map {TexturePath} was not found", relativePath);
            _context!.PSSetShaderResource(slot, null!);
            return;
        }

        using (stream)
        {
            var dds = DdsFile.Load(stream);
            if (dds.Faces.Count != 6 || dds.Faces.Any(face => face.MipMaps.Length == 0))
            {
                _log.LogWarning("Environment map {TexturePath} is not a six-face DDS cubemap", relativePath);
                _context!.PSSetShaderResource(slot, null!);
                return;
            }

            var decoder = new BcDecoder();
            if (!decoder.IsSupportedFormat(dds))
            {
                _log.LogWarning("Environment map {TexturePath} has an unsupported DDS format", relativePath);
                _context!.PSSetShaderResource(slot, null!);
                return;
            }

            var width = checked((int)dds.Faces[0].Width);
            var height = checked((int)dds.Faces[0].Height);
            var format = decoder.GetFormat(dds);
            var facePixels = new byte[6][];
            for (var faceIndex = 0; faceIndex < dds.Faces.Count; faceIndex++)
            {
                var face = dds.Faces[faceIndex];
                if (face.Width != (uint)width || face.Height != (uint)height)
                {
                    _log.LogWarning("Environment map {TexturePath} has inconsistent cubemap face dimensions", relativePath);
                    _context!.PSSetShaderResource(slot, null!);
                    return;
                }

                var decodedFace = decoder.DecodeRaw2D(
                    face.MipMaps[0].Data, width, height, format).Span;
                var pixels = new byte[checked(width * height * 4)];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = decodedFace[y, x];
                        var offset = (y * width + x) * 4;
                        pixels[offset] = pixel.r;
                        pixels[offset + 1] = pixel.g;
                        pixels[offset + 2] = pixel.b;
                        pixels[offset + 3] = pixel.a;
                    }
                }
                facePixels[faceIndex] = pixels;
            }

            var description = new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 6,
                Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource,
                MiscFlags = ResourceOptionFlags.TextureCube
            };
            var pinnedPixels = facePixels.Select(pixels => GCHandle.Alloc(pixels, GCHandleType.Pinned)).ToArray();
            try
            {
                var initialData = pinnedPixels.Select((handle, index) => new SubresourceData(
                    handle.AddrOfPinnedObject(), (uint)(width * 4), (uint)facePixels[index].Length)).ToArray();
                var texture = _device!.CreateTexture2D(description, initialData);
                var viewDescription = new ShaderResourceViewDescription(
                    ShaderResourceViewDimension.TextureCube, Format.R8G8B8A8_UNorm,
                    0, 1, 0, 6, BufferExtendedShaderResourceViewFlags.None);
                var view = _device.CreateShaderResourceView(texture, viewDescription);
                _environmentTextureCache.Add(textureRelativePath, (texture, view));
                _context!.PSSetShaderResource(slot, view);
            }
            finally
            {
                foreach (var handle in pinnedPixels)
                    handle.Free();
            }
        }
    }

    private void CreateWhiteTexture()
    {
        var pixel = new byte[] { 255, 255, 255, 255 };
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

    public void SelectObjectAt(float pixelX, float pixelY, float viewportWidth, float viewportHeight)
    {
        _selectedObjectId = ScenePicker.Pick(_sceneObjects, _camera,
            pixelX, pixelY, viewportWidth, viewportHeight);
        UpdateSelectionBoundsBuffer();
        Render();
    }
    private void UpdateSelectionBoundsBuffer()
    {
        _selectionVertexBuffer?.Dispose();
        _selectionVertexBuffer = null;
        _selectionVertexCount = 0;
        if (_selectedObjectId is not Guid selectedId)
            return;

        var selectedObjects = _sceneObjects.Where(obj => obj.Id == selectedId).ToArray();
        if (selectedObjects.Length == 0)
            return;

        var first = selectedObjects[0];
        var min = first.LocalBoundsMin;
        var max = first.LocalBoundsMax;
        foreach (var obj in selectedObjects.Skip(1))
        {
            min = Vector3.Min(min, obj.LocalBoundsMin);
            max = Vector3.Max(max, obj.LocalBoundsMax);
        }
        if (!IsFinite(min) || !IsFinite(max))
            return;

        var localCorners = new Vector3[]
        {
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
            new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z),
            new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z)
        };
        var rotation =
            Matrix4x4.CreateRotationZ(-first.Rotation.Z) *
            Matrix4x4.CreateRotationY(-first.Rotation.Y) *
            Matrix4x4.CreateRotationX(-first.Rotation.X);
        var corners = localCorners.Select(corner => Vector3.Transform(corner, rotation) + first.Position).ToArray();
        var edges = new[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
        var tint = new Vector4(1f, 0.75f, 0.05f, 1f);
        var emissive = new Vector4(1f, 0.65f, 0f, 4f);
        var vertices = edges.Select(index => new Vertex(corners[index], Vector3.UnitZ,
            Vector2.Zero, tint, Vector4.Zero, emissive, 1f, false)).ToArray();
        _selectionVertexBuffer = _device!.CreateBuffer(vertices, BindFlags.VertexBuffer);
        _selectionVertexCount = (uint)vertices.Length;
    }

    private void DrawSelectionBounds()
    {
        if (_selectionVertexBuffer is null || _selectionVertexCount == 0)
            return;

        _context!.IASetPrimitiveTopology(PrimitiveTopology.LineList);
        _context.IASetVertexBuffer(0, _selectionVertexBuffer, (uint)Marshal.SizeOf<Vertex>());
        _context.OMSetDepthStencilState(_depthState, 0);
        SetTexture(null, 0);
        SetTexture(null, 1);
        _context.Draw(_selectionVertexCount, 0);
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetVertexBuffer(0, _vertexBuffer, (uint)Marshal.SizeOf<Vertex>());
    }

    private void CreateBlackTexture()
    {
        var pixel = new byte[] { 0, 0, 0, 255 };
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
                _blackTexture = _device!.CreateTexture2D(description, initialData);
            }
        }
        _blackTextureView = _device!.CreateShaderResourceView(_blackTexture);
    }

    private void UpdateCameraConstants()
    {
        var aspect = (float)_width / Math.Max(1u, _height);
        var cameraPosition = _camera.Position;
        // System.Numerics uses row-vector composition; HLSL uses mul(vector, matrix).
        var worldViewProjection = _camera.CreateViewProjection(aspect);

        var selectedLights = _placedLights
            .OrderByDescending(light =>
            {
                var distanceSquared = Vector3.DistanceSquared(light.Position, _camera.Target);
                var radiusSquared = light.Radius * light.Radius;
                var luminance = Vector3.Dot(light.Color, new Vector3(0.2126f, 0.7152f, 0.0722f));
                return luminance * radiusSquared / (distanceSquared + radiusSquared + 1f);
            })
            .Take(MaxPointLights)
            .ToArray();

        unsafe
        {
            var constants = new SceneConstants
            {
                WorldViewProjection = worldViewProjection,
                LightDirection = new Vector4(_previewMode
                    ? Vector3.Normalize(new Vector3(-0.45f, -0.35f, 0.82f))
                    : _cellLighting.Direction, 0f),
                CameraPosition = new Vector4(cameraPosition, 1f),
                AmbientLightColor = new Vector4(_previewMode ? new Vector3(0.38f) : _cellLighting.Ambient, 1f),
                DirectionalLightColor = new Vector4(_previewMode ? new Vector3(1.8f, 1.75f, 1.68f) : _cellLighting.Directional, 1f),
                PointLightCount = new Vector4(_previewMode ? 0 : selectedLights.Length, 0f, 0f, 0f)
            };

            for (var i = 0; i < _cellLighting.AmbientCube.Length; i++)
            {
                var offset = i * 4;
                var ambient = _previewMode ? new Vector3(0.38f) : _cellLighting.AmbientCube[i];
                constants.AmbientLightCube[offset] = ambient.X;
                constants.AmbientLightCube[offset + 1] = ambient.Y;
                constants.AmbientLightCube[offset + 2] = ambient.Z;
                constants.AmbientLightCube[offset + 3] = 1f;
            }

            for (var i = 0; i < selectedLights.Length; i++)
            {
                var light = selectedLights[i];
                var offset = i * 4;
                constants.PointLightPositionRadius[offset] = light.Position.X;
                constants.PointLightPositionRadius[offset + 1] = light.Position.Y;
                constants.PointLightPositionRadius[offset + 2] = light.Position.Z;
                constants.PointLightPositionRadius[offset + 3] = light.Radius;
                constants.PointLightColorFalloff[offset] = light.Color.X;
                constants.PointLightColorFalloff[offset + 1] = light.Color.Y;
                constants.PointLightColorFalloff[offset + 2] = light.Color.Z;
                constants.PointLightColorFalloff[offset + 3] = light.FalloffExponent;
            }

            _context!.UpdateSubresource(in constants, _sceneBuffer!);
        }
    }

    private static Vector3 ToLinearRgb(NiflySharp.Structs.Color3 color) => new(
        MathF.Max(color.R, 0f),
        MathF.Max(color.G, 0f),
        MathF.Max(color.B, 0f));

    private static Vector3 ToLinearRgb(NiflySharp.Structs.Color4 color) => new(
        MathF.Max(color.R, 0f),
        MathF.Max(color.G, 0f),
        MathF.Max(color.B, 0f));

    private static Vector3 ToLinearRgb(System.Drawing.Color color) => new(
        SrgbToLinear(color.R / 255f), SrgbToLinear(color.G / 255f), SrgbToLinear(color.B / 255f));

    private static float SrgbToLinear(float value) => value <= 0.04045f
        ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

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
        _selectionVertexBuffer?.Dispose();
        _selectionVertexBuffer = null;
        _selectionVertexCount = 0;
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
        foreach (var texture in _environmentTextureCache.Values)
        {
            texture.View.Dispose();
            texture.Texture.Dispose();
        }
        _environmentTextureCache.Clear();
        _whiteTextureView?.Dispose();
        _whiteTextureView = null;
        _whiteTexture?.Dispose();
        _whiteTexture = null;
        _blackTextureView?.Dispose();
        _blackTextureView = null;
        _blackTexture?.Dispose();
        _blackTexture = null;

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
