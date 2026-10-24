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
    private uint _width;
    private uint _height;
    private readonly List<(string TexturePath, string EmissiveTexturePath, Vector4 Tint, Vector4 Specular, Vector4 Emissive, float Glossiness, bool HasEmissiveMap, bool IsTransparent, Vector3 SortCenter, uint Start, uint Count)> _drawRanges = [];
    private Guid? _selectedObjectId;
    
    private float _orbitYaw = -MathF.PI / 4f;
    private float _orbitPitch = 0.35f;
    private float _cameraDistance = 5f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _modelRadius = 1f;
    private Vector3 _lightDirection = Vector3.Normalize(new Vector3(-0.4f, 0.7f, 1f));
    private Vector3 _ambientLightColor = new(0.25f);
    private Vector3 _directionalLightColor = new(1f, 0.95f, 0.9f);
    private readonly Vector3[] _ambientLightCube =
    [
        new(0.25f), new(0.25f), new(0.25f),
        new(0.25f), new(0.25f), new(0.25f)
    ];
    private readonly List<PlacedPointLight> _placedLights = [];

    private const int MaxPointLights = 32;

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
        public Vector4 Specular;
        public Vector4 Emissive;
        public Vector4 Material;

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 tint,
            Vector4 specular, Vector4 emissive, float glossiness, bool hasEmissiveMap)
        {
            Position = position;
            Normal = normal;
            TexCoord = texCoord;
            Tint = tint;
            Specular = specular;
            Emissive = emissive;
            Material = new Vector4(glossiness, hasEmissiveMap ? 1f : 0f, 0f, 0f);
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
        public List<(Vector3 A, Vector3 B, Vector3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC,
            Vector4 ColorA, Vector4 ColorB, Vector4 ColorC)> ModelTris;
        public Vector3 Position;
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public Vector3 Rotation;
        public float Scale;
        public Matrix4x4 NifTransform;
        public string TexturePath;
        public string EmissiveTexturePath;
        public Vector4 Tint;
        public Vector4 VertexColorMultiplier;
        public Vector4 Specular;
        public Vector4 Emissive;
        public float Glossiness;
        public bool IsTransparent;

        public Vector2 UVScale;
        public Vector2 UVOffset;

        public IPlacedGetter? ObjectRef;
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
                new("COLOR", 0, Format.R32G32B32A32_Float, 32, 0),
                new("TEXCOORD", 2, Format.R32G32B32A32_Float, 48, 0),
                new("TEXCOORD", 3, Format.R32G32B32A32_Float, 64, 0),
                new("TEXCOORD", 4, Format.R32G32B32A32_Float, 80, 0)
            ];

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
        ApplyCellLighting(cell, env);

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
                string lightModelPath = placedLight.Model?.File.GivenPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lightModelPath))
                    loadJobs.Add(() => LoadModelAsset(lightModelPath, position, rotation, po.Scale ?? 1f, env, false, p));
                loadJobs.Add(() => LoadLightMarker(placedLight, position, rotation, env));
            }
            else if (b is IModeledGetter stat)
            {
                string modelPath = stat.Model?.File.GivenPath ?? string.Empty;
                bool isXMarker = IsXMarkerEditorId(b.EditorID);
                float scale = po.Scale ?? 1f;
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

        string normalizedPath = relativeModelPath.Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        string loosePath = Path.Combine(env.DataFolderPath, "meshes", normalizedPath);
        if (File.Exists(loosePath))
        {
            LoadModel(loosePath, position, rotation, scale,
                isXMarker || IsXMarkerModel(relativeModelPath), Vector4.One, objectRef);
            return;
        }

        string archivePath = Path.Combine("meshes", relativeModelPath.Replace('\\', '/'));
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
        Vector3 markerRotation = Vector3.Zero;
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

        Vector3 combinedRotation = rotation + markerRotation;
        bool hasTranslucentShell = markerFile is "marker_lightshadow.nif"
            or "marker_spotlight.nif" or "marker_halfomni.nif";
        Vector4 markerTint = new(ToLinearRgb(light.Color), hasTranslucentShell ? 2.5f : 1f);
        string markerPath = Path.Combine("markers", markerFile);
        string loosePath = Path.Combine(env.DataFolderPath, "meshes", markerPath);
        if (File.Exists(loosePath))
        {
            LoadModel(loosePath, position, combinedRotation, markerScale, false, markerTint, null);
            return;
        }

        string[] archiveCandidates =
        [
            $"meshes/{markerPath.Replace('\\', '/')}",
            markerPath.Replace('\\', '/'),
            $"meshes/{markerFile}"
        ];
        foreach (string archiveCandidate in archiveCandidates)
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
        IPlacedGetter? objectRef)
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

        Guid loadedObjectId = Guid.NewGuid();
        foreach (var shape in nif.GetShapes())
        {
            shapeCount++;
            var obj = new SceneObject
            {
                Id = loadedObjectId,
                ModelTris = [],
                Position = position,
                BoundsMin = new Vector3(float.PositiveInfinity),
                BoundsMax = new Vector3(float.NegativeInfinity),
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
                Glossiness = 32f,
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
                    string effectTexturePath = effectShader.SourceTexture.Content;
                    if (isXMarker && alphaProperty is not null && string.IsNullOrWhiteSpace(effectTexturePath))
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
                else if (shader is BSLightingShaderProperty lightingShader)
                {
                    Vector3 specularColor = ToLinearRgb(lightingShader.SpecularColor);
                    float specularStrength = float.IsFinite(lightingShader.SpecularStrength)
                        ? MathF.Max(lightingShader.SpecularStrength, 0f)
                        : 0f;
                    obj.Specular = new Vector4(specularColor, specularStrength);

                    Vector3 emissiveColor = ToLinearRgb(lightingShader.EmissiveColor);
                    float emissiveMultiple = float.IsFinite(lightingShader.EmissiveMultiple)
                        ? MathF.Max(lightingShader.EmissiveMultiple, 0f)
                        : 0f;
                    obj.Emissive = new Vector4(emissiveColor, emissiveMultiple);
                    obj.Glossiness = float.IsFinite(lightingShader.Glossiness)
                        ? Math.Clamp(lightingShader.Glossiness, 1f, 256f)
                        : 32f;

                    float opacity = float.IsFinite(lightingShader.Alpha)
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
                vertexColors = data.VertexColors
                    .Select(color => new Vector4(color.R, color.G, color.B, color.A)).ToArray();
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
                Vector4 colorA = (uint)i0 < (uint)vertexColors.Length ? vertexColors[i0] : Vector4.One;
                Vector4 colorB = (uint)i1 < (uint)vertexColors.Length ? vertexColors[i1] : Vector4.One;
                Vector4 colorC = (uint)i2 < (uint)vertexColors.Length ? vertexColors[i2] : Vector4.One;
                if (isXMarker)
                {
                    // Proxy opacity is specified by the XMarker effect shader tint.
                    colorA.W = colorB.W = colorC.W = 1f;
                }
                obj.IsTransparent |= colorA.W < 0.999f || colorB.W < 0.999f || colorC.W < 0.999f;
                obj.ModelTris.Add((a, b, c, uvA, uvB, uvC, colorA, colorB, colorC));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, TransformPoint(obj, a));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, TransformPoint(obj, b));
                obj.BoundsMin = Vector3.Min(obj.BoundsMin, TransformPoint(obj, c));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, TransformPoint(obj, a));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, TransformPoint(obj, b));
                obj.BoundsMax = Vector3.Max(obj.BoundsMax, TransformPoint(obj, c));
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
            float cosPitch = MathF.Cos(_orbitPitch);
            Vector3 cameraPosition = _cameraTarget + new Vector3(
                _cameraDistance * cosPitch * MathF.Cos(_orbitYaw),
                _cameraDistance * cosPitch * MathF.Sin(_orbitYaw),
                _cameraDistance * MathF.Sin(_orbitPitch));
            foreach (var range in _drawRanges.Where(range => !range.IsTransparent)
                         .Concat(_drawRanges.Where(range => range.IsTransparent)
                             .OrderByDescending(range => Vector3.DistanceSquared(range.SortCenter, cameraPosition))))
            {
                _context.OMSetDepthStencilState(
                    range.IsTransparent ? _transparentDepthState : _depthState, 0);
                SetTexture(range.TexturePath, 0);
                SetTexture(range.EmissiveTexturePath, 1);
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
        var vertices = new List<Vertex>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);
        var indices = new List<uint>(_sceneObjects.Select(so => so.ModelTris.Count).Sum() * 3);

        var objectsByTexture = _sceneObjects
            .GroupBy(obj => (TexturePath: obj.TexturePath ?? string.Empty,
                EmissiveTexturePath: obj.EmissiveTexturePath ?? string.Empty, obj.Tint,
                obj.Specular, obj.Emissive, obj.Glossiness,
                HasEmissiveMap: !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), obj.IsTransparent))
            .ToList();
        var drawRanges = new List<(string TexturePath, string EmissiveTexturePath, Vector4 Tint,
            Vector4 Specular, Vector4 Emissive, float Glossiness, bool HasEmissiveMap,
            bool IsTransparent, Vector3 SortCenter, uint Start, uint Count)>();
        foreach (var obj in objectsByTexture.SelectMany(group => group))
        {
            uint objectStartIndex = (uint)indices.Count;
            Vector3 sortCenter = Vector3.Zero;
            int sortPointCount = 0;
            _modelRadius = 0f;

            // Duplicate each face's vertices so every face has a flat normal.
            foreach (var tri in obj.ModelTris)
            {
                Vector3 a = TransformPoint(obj, tri.A);
                Vector3 b = TransformPoint(obj, tri.B);
                Vector3 c = TransformPoint(obj, tri.C);
                sortCenter += a + b + c;
                sortPointCount += 3;

                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f)
                    continue;

                Vector3 normal = Vector3.Normalize(cross);

                _modelRadius = MathF.Max(_modelRadius, a.Length());
                _modelRadius = MathF.Max(_modelRadius, b.Length());
                _modelRadius = MathF.Max(_modelRadius, c.Length());

                uint start = (uint)vertices.Count;
                vertices.Add(new Vertex(a, normal, tri.UvA * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorA,
                    obj.Specular, obj.Emissive, obj.Glossiness, !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath)));
                vertices.Add(new Vertex(b, normal, tri.UvB * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorB,
                    obj.Specular, obj.Emissive, obj.Glossiness, !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath)));
                vertices.Add(new Vertex(c, normal, tri.UvC * obj.UVScale + obj.UVOffset,
                    obj.Tint * obj.VertexColorMultiplier * tri.ColorC,
                    obj.Specular, obj.Emissive, obj.Glossiness, !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath)));

                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            uint objectIndexCount = (uint)indices.Count - objectStartIndex;
            if (objectIndexCount > 0)
            {
                drawRanges.Add((obj.TexturePath ?? string.Empty, obj.EmissiveTexturePath ?? string.Empty,
                    obj.Tint, obj.Specular, obj.Emissive, obj.Glossiness,
                    !string.IsNullOrWhiteSpace(obj.EmissiveTexturePath), obj.IsTransparent,
                    sortPointCount > 0 ? sortCenter / sortPointCount : obj.Position,
                    objectStartIndex, objectIndexCount));
            }

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
        _drawRanges.AddRange(drawRanges);
    }

    private void SetTexture(string? nifTexturePath, uint slot)
    {
        _context!.PSSetShaderResource(slot, slot == 0 ? _whiteTextureView : _blackTextureView);

        string relativePath = (nifTexturePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        string textureRelativePath = relativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : "textures/" + relativePath;
        if (_unavailableTexturePaths.Contains(textureRelativePath)) return;
        if (_textureCache.TryGetValue(textureRelativePath, out var cachedTexture))
        {
            _context.PSSetShaderResource(slot, cachedTexture.View);
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
            int maxMipLevels = 1 + (int)MathF.Floor(MathF.Log2(Math.Max(width, height)));
            int mipLevels = Math.Clamp((int)BitConverter.ToUInt32(bytes, 28), 1, maxMipLevels);
            var mipDescriptions = new (int Width, int Height, int RowPitch, int DataSize)[mipLevels];
            int dataOffset = 128;
            bool mipDataIsValid = true;
            for (int level = 0; level < mipLevels; level++)
            {
                int mipWidth = Math.Max(1, width >> level);
                int mipHeight = Math.Max(1, height >> level);
                int rowPitch = Math.Max(1, (mipWidth + 3) / 4) * blockBytes;
                int dataSize = rowPitch * Math.Max(1, (mipHeight + 3) / 4);
                mipDescriptions[level] = (mipWidth, mipHeight, rowPitch, dataSize);
                if (dataOffset > bytes.Length - dataSize)
                {
                    mipDataIsValid = false;
                    break;
                }
                dataOffset += dataSize;
            }
            if (!mipDataIsValid)
            {
                _unavailableTexturePaths.Add(textureRelativePath);
                return;
            }

            var description = new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, MipLevels = (uint)mipLevels, ArraySize = 1,
                Format = format, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
            };
            unsafe
            {
                fixed (byte* textureData = bytes)
                {
                    var initialData = new SubresourceData[mipLevels];
                    int mipOffset = 128;
                    for (int level = 0; level < mipLevels; level++)
                    {
                        var mip = mipDescriptions[level];
                        initialData[level] = new SubresourceData(
                            (IntPtr)(textureData + mipOffset), (uint)mip.RowPitch, (uint)mip.DataSize);
                        mipOffset += mip.DataSize;
                    }
                    var texture = _device!.CreateTexture2D(description, initialData);
                    var view = _device.CreateShaderResourceView(texture);
                    _textureCache.Add(textureRelativePath, (texture, view));
                    _context.PSSetShaderResource(slot, view);
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

    public void SelectObjectAt(float pixelX, float pixelY, float viewportWidth, float viewportHeight)
    {
        if (viewportWidth <= 0f || viewportHeight <= 0f || _sceneObjects.Count == 0)
            return;

        float cosPitch = MathF.Cos(_orbitPitch);
        Vector3 cameraPosition = _cameraTarget + new Vector3(
            _cameraDistance * cosPitch * MathF.Cos(_orbitYaw),
            _cameraDistance * cosPitch * MathF.Sin(_orbitYaw),
            _cameraDistance * MathF.Sin(_orbitPitch));
        Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPosition, _cameraTarget, Vector3.UnitZ);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView, viewportWidth / viewportHeight, NearPlane,
            MathF.Max(_cameraDistance + _modelRadius * 4f, 100f));
        if (!Matrix4x4.Invert(view * projection, out Matrix4x4 inverseViewProjection))
            return;

        float ndcX = 2f * pixelX / viewportWidth - 1f;
        float ndcY = 1f - 2f * pixelY / viewportHeight;
        Vector3 nearPoint = Unproject(ndcX, ndcY, 0f, inverseViewProjection);
        Vector3 farPoint = Unproject(ndcX, ndcY, 1f, inverseViewProjection);
        Vector3 rayDirection = Vector3.Normalize(farPoint - nearPoint);

        float closestDistance = float.PositiveInfinity;
        Guid? hitObjectId = null;
        foreach (var obj in _sceneObjects)
        {
            if (!RayIntersectsBounds(cameraPosition, rayDirection, obj.BoundsMin, obj.BoundsMax))
                continue;

            foreach (var tri in obj.ModelTris)
            {
                Vector3 a = TransformPoint(obj, tri.A);
                Vector3 b = TransformPoint(obj, tri.B);
                Vector3 c = TransformPoint(obj, tri.C);
                if (RayIntersectsTriangle(cameraPosition, rayDirection, a, b, c, out float distance) &&
                    distance < closestDistance)
                {
                    closestDistance = distance;
                    hitObjectId = obj.Id;
                }
            }
        }

        _selectedObjectId = hitObjectId;
        UpdateSelectionBoundsBuffer();
        Render();
    }

    private static Vector3 Unproject(float x, float y, float z, Matrix4x4 inverseViewProjection)
    {
        Vector4 point = Vector4.Transform(new Vector4(x, y, z, 1f), inverseViewProjection);
        return new Vector3(point.X, point.Y, point.Z) / point.W;
    }

    private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max)
    {
        float tMin = 0f;
        float tMax = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            float lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            float hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }

            float t1 = (lo - o) / d;
            float t2 = (hi - o) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return false;
        }
        return true;
    }

    private static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction,
        Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        const float epsilon = 1e-7f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        // The rasterizer keeps counter-clockwise front faces and culls back faces.
        // Moller-Trumbore's determinant is positive for those faces when the ray
        // travels from the camera toward the front of the triangle.
        if (determinant <= epsilon)
        {
            distance = 0f;
            return false;
        }

        float inverseDeterminant = 1f / determinant;
        Vector3 t = origin - a;
        float u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < 0f || u > 1f)
        {
            distance = 0f;
            return false;
        }

        Vector3 q = Vector3.Cross(t, edge1);
        float v = Vector3.Dot(direction, q) * inverseDeterminant;
        if (v < 0f || u + v > 1f)
        {
            distance = 0f;
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        return distance >= 0f;
    }

    private void UpdateSelectionBoundsBuffer()
    {
        _selectionVertexBuffer?.Dispose();
        _selectionVertexBuffer = null;
        _selectionVertexCount = 0;
        if (_selectedObjectId is not Guid selectedId)
            return;

        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        foreach (var obj in _sceneObjects.Where(obj => obj.Id == selectedId))
        {
            min = Vector3.Min(min, obj.BoundsMin);
            max = Vector3.Max(max, obj.BoundsMax);
        }
        if (!IsFinite(min) || !IsFinite(max))
            return;

        Vector3[] corners =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
            new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z),
            new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z)
        ];
        int[] edges = [0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7];
        Vector4 tint = new(1f, 0.75f, 0.05f, 1f);
        Vector4 emissive = new(1f, 0.65f, 0f, 4f);
        Vertex[] vertices = edges.Select(index => new Vertex(corners[index], Vector3.UnitZ,
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
        byte[] pixel = [0, 0, 0, 255];
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

        var selectedLights = _placedLights
            .OrderByDescending(light =>
            {
                float distanceSquared = Vector3.DistanceSquared(light.Position, _cameraTarget);
                float radiusSquared = light.Radius * light.Radius;
                float luminance = Vector3.Dot(light.Color, new Vector3(0.2126f, 0.7152f, 0.0722f));
                return luminance * radiusSquared / (distanceSquared + radiusSquared + 1f);
            })
            .Take(MaxPointLights)
            .ToArray();

        unsafe
        {
            var constants = new SceneConstants
            {
                WorldViewProjection = worldViewProjection,
                LightDirection = new Vector4(_lightDirection, 0f),
                CameraPosition = new Vector4(cameraPosition, 1f),
                AmbientLightColor = new Vector4(_ambientLightColor, 1f),
                DirectionalLightColor = new Vector4(_directionalLightColor, 1f),
                PointLightCount = new Vector4(selectedLights.Length, 0f, 0f, 0f)
            };

            for (int i = 0; i < _ambientLightCube.Length; i++)
            {
                int offset = i * 4;
                constants.AmbientLightCube[offset] = _ambientLightCube[i].X;
                constants.AmbientLightCube[offset + 1] = _ambientLightCube[i].Y;
                constants.AmbientLightCube[offset + 2] = _ambientLightCube[i].Z;
                constants.AmbientLightCube[offset + 3] = 1f;
            }

            for (int i = 0; i < selectedLights.Length; i++)
            {
                var light = selectedLights[i];
                int offset = i * 4;
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

    private void ApplyCellLighting(ICellGetter cell, IGameEnvironment env)
    {
        _ambientLightColor = new Vector3(0.25f);
        _directionalLightColor = new Vector3(1f, 0.95f, 0.9f);
        _lightDirection = Vector3.Normalize(new Vector3(-0.4f, 0.7f, 1f));
        Array.Fill(_ambientLightCube, _ambientLightColor);

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

        _ambientLightColor = ToLinearRgb(ambientColor);
        _directionalLightColor = ToLinearRgb(directionalColor);

        var templateAmbientColors = template?.DirectionalAmbientColors ?? template?.AmbientColors;
        var ambientColors = inheritAmbient && templateAmbientColors is not null
            ? templateAmbientColors
            : lighting?.AmbientColors ?? templateAmbientColors;
        SetAmbientLightCube(ambientColors, _ambientLightColor);

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

    private void SetAmbientLightCube(IAmbientColorsGetter? colors, Vector3 fallback)
    {
        if (colors is null)
        {
            Array.Fill(_ambientLightCube, fallback);
            return;
        }

        float scale = MathF.Max(colors.Scale, 0f);
        _ambientLightCube[0] = ToLinearRgb(colors.DirectionalXPlus) * scale;
        _ambientLightCube[1] = ToLinearRgb(colors.DirectionalXMinus) * scale;
        _ambientLightCube[2] = ToLinearRgb(colors.DirectionalYPlus) * scale;
        _ambientLightCube[3] = ToLinearRgb(colors.DirectionalYMinus) * scale;
        _ambientLightCube[4] = ToLinearRgb(colors.DirectionalZPlus) * scale;
        _ambientLightCube[5] = ToLinearRgb(colors.DirectionalZMinus) * scale;
    }

    private static Vector3 ToLinearRgb(DrawingColor color) => new(
        SrgbToLinear(color.R / 255f),
        SrgbToLinear(color.G / 255f),
        SrgbToLinear(color.B / 255f));

    private static Vector3 ToLinearRgb(NiflySharp.Structs.Color3 color) => new(
        MathF.Max(color.R, 0f),
        MathF.Max(color.G, 0f),
        MathF.Max(color.B, 0f));

    private static Vector3 ToLinearRgb(NiflySharp.Structs.Color4 color) => new(
        MathF.Max(color.R, 0f),
        MathF.Max(color.G, 0f),
        MathF.Max(color.B, 0f));

    private static float SrgbToLinear(float value) => value <= 0.04045f
        ? value / 12.92f
        : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

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
