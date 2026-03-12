using System.Globalization;
using System.Numerics;

namespace D3DShared;

/// <summary>
/// Result of loading an OBJ file
/// </summary>
public class ObjMeshData
{
    public Vertex[] Vertices { get; init; } = Array.Empty<Vertex>();
    public uint[] Indices { get; init; } = Array.Empty<uint>();

    /// <summary>
    /// All unique vertices (deduplicated by position) for picking/collision
    /// </summary>
    public List<(Vector3 Position, Vector3 Normal)> UniqueVertices { get; init; } = new();

    /// <summary>
    /// Triangle indices referencing UniqueVertices
    /// </summary>
    public List<(int V0, int V1, int V2)> Triangles { get; init; } = new();

    /// <summary>
    /// Model bounds
    /// </summary>
    public Vector3 BoundsMin { get; init; }
    public Vector3 BoundsMax { get; init; }
    public Vector3 Center => (BoundsMin + BoundsMax) / 2;
    public float Height => BoundsMax.Y - BoundsMin.Y;
}

/// <summary>
/// OBJ file loader
/// </summary>
public static class ObjLoader
{
    /// <summary>
    /// Load an OBJ file and return mesh data
    /// </summary>
    /// <param name="objPath">Path to the OBJ file</param>
    /// <param name="scale">Scale factor to apply to positions</param>
    /// <param name="flipNormals">Whether to flip normals (for models with inverted normals)</param>
    /// <param name="centerModel">Whether to center the model at origin</param>
    public static ObjMeshData Load(string objPath, float scale = 1.0f, bool flipNormals = false, bool centerModel = true)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var vertices = new List<Vertex>();
        var indices = new List<uint>();

        // Parse OBJ file
        foreach (var line in File.ReadLines(objPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture) * scale,
                        float.Parse(parts[2], CultureInfo.InvariantCulture) * scale,
                        float.Parse(parts[3], CultureInfo.InvariantCulture) * scale));
                    break;

                case "vn" when parts.Length >= 4:
                    normals.Add(new Vector3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)));
                    break;

                case "f" when parts.Length >= 4:
                    // Parse face indices (can be v, v/vt, v/vt/vn, or v//vn)
                    var faceVerts = new List<(int pos, int norm)>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var indicesStr = parts[i].Split('/');
                        int posIdx = int.Parse(indicesStr[0]) - 1; // OBJ is 1-indexed
                        int normIdx = -1;
                        if (indicesStr.Length >= 3 && !string.IsNullOrEmpty(indicesStr[2]))
                            normIdx = int.Parse(indicesStr[2]) - 1;
                        faceVerts.Add((posIdx, normIdx));
                    }

                    // Triangulate face (fan triangulation)
                    for (int i = 1; i < faceVerts.Count - 1; i++)
                    {
                        var v0 = faceVerts[0];
                        var v1 = faceVerts[i];
                        var v2 = faceVerts[i + 1];

                        // Calculate face normal if not provided
                        Vector3 p0 = positions[v0.pos];
                        Vector3 p1 = positions[v1.pos];
                        Vector3 p2 = positions[v2.pos];
                        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(p2 - p0, p1 - p0));

                        void AddVertex((int pos, int norm) v)
                        {
                            Vector3 n = v.norm >= 0 && v.norm < normals.Count
                                ? normals[v.norm]
                                : faceNormal;
                            if (flipNormals)
                                n = -n;
                            indices.Add((uint)vertices.Count);
                            vertices.Add(new Vertex(positions[v.pos], n));
                        }

                        // Clockwise winding for CullCounterClockwise
                        AddVertex(v0);
                        AddVertex(v2);
                        AddVertex(v1);
                    }
                    break;
            }
        }

        // Calculate bounds
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }

        // Center the model if requested
        if (centerModel)
        {
            Vector3 center = (min + max) / 2;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                vertices[i] = new Vertex(v.Position - center, v.Normal);
            }
            // Update bounds
            min -= center;
            max -= center;
        }

        // Create unique vertices list for picking (deduplicated)
        var uniqueVertices = new List<(Vector3 Position, Vector3 Normal)>();
        var positionToIndex = new Dictionary<(int, int, int), int>();

        foreach (var v in vertices)
        {
            var key = ((int)(v.Position.X * 10000), (int)(v.Position.Y * 10000), (int)(v.Position.Z * 10000));
            if (!positionToIndex.ContainsKey(key))
            {
                positionToIndex[key] = uniqueVertices.Count;
                uniqueVertices.Add((v.Position, v.Normal));
            }
        }

        // Create triangle list for ray intersection (using unique vertex indices)
        var triangles = new List<(int V0, int V1, int V2)>();
        for (int i = 0; i < indices.Count; i += 3)
        {
            var vert0 = vertices[(int)indices[i]];
            var vert1 = vertices[(int)indices[i + 1]];
            var vert2 = vertices[(int)indices[i + 2]];

            var k0 = ((int)(vert0.Position.X * 10000), (int)(vert0.Position.Y * 10000), (int)(vert0.Position.Z * 10000));
            var k1 = ((int)(vert1.Position.X * 10000), (int)(vert1.Position.Y * 10000), (int)(vert1.Position.Z * 10000));
            var k2 = ((int)(vert2.Position.X * 10000), (int)(vert2.Position.Y * 10000), (int)(vert2.Position.Z * 10000));

            triangles.Add((positionToIndex[k0], positionToIndex[k1], positionToIndex[k2]));
        }

        return new ObjMeshData
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            UniqueVertices = uniqueVertices,
            Triangles = triangles,
            BoundsMin = min,
            BoundsMax = max
        };
    }
}
