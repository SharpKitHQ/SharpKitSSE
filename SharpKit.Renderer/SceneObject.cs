using System.Numerics;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Renderer;

internal struct SceneObject
{
    public Guid Id;
    public List<(Vector3 A, Vector3 B, Vector3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC,
        Vector4 ColorA, Vector4 ColorB, Vector4 ColorC)> ModelTris;
    public Vector3 Position;
    public Vector3 BoundsMin;
    public Vector3 BoundsMax;
    public Vector3 LocalBoundsMin;
    public Vector3 LocalBoundsMax;
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

    public readonly Vector3 TransformPoint(Vector3 point)
    {
        // Skyrim REFR rotations are clockwise radians. System.Numerics uses
        // counterclockwise rotations and row-vector matrix composition.
        var rotation =
            Matrix4x4.CreateRotationZ(-Rotation.Z) *
            Matrix4x4.CreateRotationY(-Rotation.Y) *
            Matrix4x4.CreateRotationX(-Rotation.X);
        var nifPoint = Vector3.Transform(point, NifTransform);
        return Vector3.Transform(nifPoint * Scale, rotation) + Position;
    }
}
