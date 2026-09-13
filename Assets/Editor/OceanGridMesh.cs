using UnityEngine;
using UnityEngine.Rendering;

// A flat, evenly subdivided XZ grid centered on the origin. Unity's own Plane primitive only has a
// 10x10 quad grid (121 vertices) baked in, which is too coarse for Ocean.shader's vertex displacement
// to read as waves rather than a tilting slab — this builds a grid at whatever resolution the wave
// wavelengths need (roughly: quad size should be well under the shortest wavelength in play).
public static class OceanGridMesh
{
    // sizeX/sizeZ: world-space extents (metres). segmentsX/segmentsZ: quads per axis (vertex count is
    // (segments+1) per axis), so higher means finer waves but more triangles — pick the smallest
    // resolution that still reads as smooth curves at the camera distances this is used at.
    public static Mesh Build(float sizeX, float sizeZ, int segmentsX, int segmentsZ)
    {
        segmentsX = Mathf.Max(1, segmentsX);
        segmentsZ = Mathf.Max(1, segmentsZ);
        var vertsX = segmentsX + 1;
        var vertsZ = segmentsZ + 1;
        var vertexCount = vertsX * vertsZ;

        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        for (var z = 0; z < vertsZ; z++)
        {
            for (var x = 0; x < vertsX; x++)
            {
                var i = z * vertsX + x;
                var u = (float)x / segmentsX;
                var v = (float)z / segmentsZ;
                vertices[i] = new Vector3((u - 0.5f) * sizeX, 0f, (v - 0.5f) * sizeZ);
                // The shader derives its own flat-shaded normal from screen-space derivatives, so this
                // is only a fallback for anything else that reads mesh.normals (e.g. a future collider
                // query or a different shader) — up is the only sane default for a water plane.
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(u, v);
            }
        }

        var triangles = new int[segmentsX * segmentsZ * 6];
        var t = 0;
        for (var z = 0; z < segmentsZ; z++)
        {
            for (var x = 0; x < segmentsX; x++)
            {
                var i = z * vertsX + x;
                triangles[t++] = i;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + vertsX + 1;
            }
        }

        var mesh = new Mesh { name = $"Ocean_{sizeX:0}x{sizeZ:0}_{segmentsX}x{segmentsZ}" };
        if (vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
