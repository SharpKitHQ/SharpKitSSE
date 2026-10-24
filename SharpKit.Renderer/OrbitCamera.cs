using System.Numerics;

namespace SharpKit.Renderer;

/// <summary>Orbit camera state and camera movement for the model viewport.</summary>
internal sealed class OrbitCamera
{
    private const float FieldOfView = MathF.PI / 4f;
    private const float NearPlane = 0.01f;

    public float Yaw { get; private set; } = -MathF.PI / 4f;
    public float Pitch { get; private set; } = 0.35f;
    public float Distance { get; private set; } = 5f;
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public float ModelRadius { get; private set; } = 1f;

    public Vector3 Position
    {
        get
        {
            var cosPitch = MathF.Cos(Pitch);
            return Target + new Vector3(
                Distance * cosPitch * MathF.Cos(Yaw),
                Distance * cosPitch * MathF.Sin(Yaw),
                Distance * MathF.Sin(Pitch));
        }
    }

    public void Frame(Vector3 min, Vector3 max, float padding = 1.5f)
    {
        Target = (min + max) * 0.5f;
        ModelRadius = MathF.Max(((max - min) * 0.5f).Length(), 0.01f);
        Distance = ModelRadius / MathF.Sin(FieldOfView * 0.5f) * padding;
    }

    public void FrameRadius(float radius, float padding = 1.25f)
    {
        ModelRadius = MathF.Max(radius, 0.01f);
        Distance = ModelRadius / MathF.Sin(FieldOfView * 0.5f) * padding;
    }

    public void Orbit(float deltaX, float deltaY)
    {
        const float sensitivity = 0.003f;
        Yaw -= deltaX * sensitivity;
        Pitch = Math.Clamp(Pitch + deltaY * sensitivity,
            -MathF.PI / 2f + 0.01f, MathF.PI / 2f - 0.01f);
    }

    public void Pan(float deltaX, float deltaY)
    {
        var forward = Vector3.Normalize(new Vector3(
            MathF.Cos(Pitch) * MathF.Cos(Yaw),
            MathF.Cos(Pitch) * MathF.Sin(Yaw),
            MathF.Sin(Pitch)));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var panScale = Distance * 0.001f;
        Target += right * (deltaX * panScale) + up * (deltaY * panScale);
    }

    public void Zoom(float delta)
    {
        Distance *= MathF.Exp(-delta * 0.1f);
        Distance = Math.Clamp(Distance, ModelRadius * 0.05f, ModelRadius * 100f);
    }

    public Matrix4x4 CreateViewProjection(float aspect)
    {
        var view = Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitZ);
        var farPlane = MathF.Max(Distance + ModelRadius * 4f, 100f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView, MathF.Max(aspect, 0.01f), NearPlane, farPlane);
        return view * projection;
    }
}
