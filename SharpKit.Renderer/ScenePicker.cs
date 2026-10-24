using System.Numerics;

namespace SharpKit.Renderer;

internal static class ScenePicker
{
    public static Guid? Pick(IReadOnlyList<SceneObject> sceneObjects, OrbitCamera camera,
        float pixelX, float pixelY, float viewportWidth, float viewportHeight)
    {
        if (viewportWidth <= 0f || viewportHeight <= 0f || sceneObjects.Count == 0)
            return null;
        if (!Matrix4x4.Invert(camera.CreateViewProjection(viewportWidth / viewportHeight),
                out var inverseViewProjection))
            return null;

        var ndcX = 2f * pixelX / viewportWidth - 1f;
        var ndcY = 1f - 2f * pixelY / viewportHeight;
        var nearPoint = Unproject(ndcX, ndcY, 0f, inverseViewProjection);
        var farPoint = Unproject(ndcX, ndcY, 1f, inverseViewProjection);
        var rayDirection = Vector3.Normalize(farPoint - nearPoint);
        var rayOrigin = camera.Position;

        var closestDistance = float.PositiveInfinity;
        Guid? hitObjectId = null;
        foreach (var obj in sceneObjects)
        {
            if (!RayIntersectsBounds(rayOrigin, rayDirection, obj.BoundsMin, obj.BoundsMax))
                continue;
            foreach (var tri in obj.ModelTris)
            {
                if (RayIntersectsTriangle(rayOrigin, rayDirection, obj.TransformPoint(tri.A),
                        obj.TransformPoint(tri.B), obj.TransformPoint(tri.C), out var distance) &&
                    distance < closestDistance)
                {
                    closestDistance = distance;
                    hitObjectId = obj.Id;
                }
            }
        }
        return hitObjectId;
    }

    private static Vector3 Unproject(float x, float y, float z, Matrix4x4 inverseViewProjection)
    {
        var p = Vector4.Transform(new Vector4(x, y, z, 1f), inverseViewProjection);
        return new Vector3(p.X, p.Y, p.Z) / p.W;
    }

    private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max)
    {
        var tMin = 0f;
        var tMax = float.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            var d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            var lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            var hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }
            var t1 = (lo - o) / d;
            var t2 = (hi - o) / d;
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
        var edge1 = b - a;
        var edge2 = c - a;
        var p = Vector3.Cross(direction, edge2);
        var determinant = Vector3.Dot(edge1, p);
        // Match the renderer's front-face winding and back-face culling.
        if (determinant <= epsilon)
        {
            distance = 0f;
            return false;
        }
        var inverseDeterminant = 1f / determinant;
        var t = origin - a;
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < 0f || u > 1f)
        {
            distance = 0f;
            return false;
        }
        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(direction, q) * inverseDeterminant;
        if (v < 0f || u + v > 1f)
        {
            distance = 0f;
            return false;
        }
        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        return distance >= 0f;
    }
}
