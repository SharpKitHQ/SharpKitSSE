using System.Drawing;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Skyrim;
using SkyrimCellLighting = Mutagen.Bethesda.Skyrim.CellLighting;

namespace SharpKit.Renderer;

internal sealed class CellLightingState
{
    public Vector3 Direction { get; private init; } = Vector3.Normalize(new Vector3(-0.4f, 0.7f, 1f));
    public Vector3 Ambient { get; private init; } = new(0.25f);
    public Vector3 Directional { get; private init; } = new(1f, 0.95f, 0.9f);
    public Vector3[] AmbientCube { get; private init; } = Enumerable.Repeat(new Vector3(0.25f), 6).ToArray();

    public static CellLightingState Resolve(ICellGetter cell, IGameEnvironment env, ILogger logger)
    {
        var ambient = new Vector3(0.25f);
        var directional = new Vector3(1f, 0.95f, 0.9f);
        var direction = Vector3.Normalize(new Vector3(-0.4f, 0.7f, 1f));
        var ambientCube = Enumerable.Repeat(ambient, 6).ToArray();

        var lighting = cell.Lighting;
        ILightingTemplateGetter? template = null;
        if (cell.LightingTemplate.TryResolve(env.LinkCache, out var resolvedTemplate))
            template = resolvedTemplate;
        if (lighting is null && template is null)
            return new CellLightingState { Direction = direction, Ambient = ambient,
                Directional = directional, AmbientCube = ambientCube };

        var inheritAmbient = lighting is null || (lighting.Inherits & SkyrimCellLighting.Inherit.AmbientColor) != 0;
        var inheritDirectionalColor = lighting is null || (lighting.Inherits & SkyrimCellLighting.Inherit.DirectionalColor) != 0;
        var inheritDirection = lighting is null || (lighting.Inherits & SkyrimCellLighting.Inherit.DirectionalRotation) != 0;
        var ambientColor = inheritAmbient && template is not null
            ? template.AmbientColor : lighting?.AmbientColor ?? template?.AmbientColor ?? Color.FromArgb(64, 64, 64);
        var directionalColor = inheritDirectionalColor && template is not null
            ? template.DirectionalColor : lighting?.DirectionalColor ?? template?.DirectionalColor ?? Color.White;
        var directionXY = inheritDirection && template is not null
            ? template.DirectionalRotationXY : lighting?.DirectionalRotationXY ?? template?.DirectionalRotationXY ?? 0;
        var directionZ = inheritDirection && template is not null
            ? template.DirectionalRotationZ : lighting?.DirectionalRotationZ ?? template?.DirectionalRotationZ ?? 0;

        ambient = ToLinearRgb(ambientColor);
        directional = ToLinearRgb(directionalColor);
        var templateAmbient = template?.DirectionalAmbientColors ?? template?.AmbientColors;
        var ambientColors = inheritAmbient && templateAmbient is not null
            ? templateAmbient : lighting?.AmbientColors ?? templateAmbient;
        if (ambientColors is null)
            Array.Fill(ambientCube, ambient);
        else
        {
            var scale = MathF.Max(ambientColors.Scale, 0f);
            ambientCube =
            [
                ToLinearRgb(ambientColors.DirectionalXPlus) * scale,
                ToLinearRgb(ambientColors.DirectionalXMinus) * scale,
                ToLinearRgb(ambientColors.DirectionalYPlus) * scale,
                ToLinearRgb(ambientColors.DirectionalYMinus) * scale,
                ToLinearRgb(ambientColors.DirectionalZPlus) * scale,
                ToLinearRgb(ambientColors.DirectionalZMinus) * scale
            ];
        }

        var azimuth = directionXY * (MathF.PI / 180f);
        var elevation = directionZ * (MathF.PI / 180f);
        direction = Vector3.Normalize(new Vector3(MathF.Cos(azimuth) * MathF.Cos(elevation),
            MathF.Sin(azimuth) * MathF.Cos(elevation), MathF.Sin(elevation)));
        logger.LogDebug("Cell lighting: ambient={Ambient}, directional={Directional}, rotation=({RotationXY}, {RotationZ})",
            ambient, directional, directionXY, directionZ);
        return new CellLightingState { Direction = direction, Ambient = ambient,
            Directional = directional, AmbientCube = ambientCube };
    }

    private static Vector3 ToLinearRgb(Color color) => new(
        SrgbToLinear(color.R / 255f), SrgbToLinear(color.G / 255f), SrgbToLinear(color.B / 255f));

    private static Vector3 ToLinearRgb(NiflySharp.Structs.Color3 color) => new(
        MathF.Max(color.R, 0f), MathF.Max(color.G, 0f), MathF.Max(color.B, 0f));

    private static float SrgbToLinear(float value) => value <= 0.04045f
        ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
