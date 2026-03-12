using System.Numerics;
using System.Runtime.InteropServices;

namespace D3DShared;

/// <summary>
/// Standard vertex with position and normal
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;

    public Vertex(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }
}

/// <summary>
/// Constant buffer data for standard 3-light rendering
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ConstantBufferData
{
    public Matrix4x4 WorldViewProj;
    public Matrix4x4 World;
    public Vector4 Light1Direction;
    public Vector4 Light1Color;
    public Vector4 Light2Direction;
    public Vector4 Light2Color;
    public Vector4 Light3Direction;
    public Vector4 Light3Color;
    public Vector4 ObjectColor;
}
