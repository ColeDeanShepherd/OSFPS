using UnityEngine;

public static class MeshExtensions
{
    public static Mesh CopyMesh(Mesh mesh)
    {
        return new Mesh
        {
            name = $"{mesh.name} - Copy",
            vertices = mesh.vertices,
            normals = mesh.normals,
            tangents = mesh.tangents,
            uv = mesh.uv,
            uv2 = mesh.uv2,
            uv3 = mesh.uv3,
            uv4 = mesh.uv4,
            colors32 = mesh.colors32,
            triangles = mesh.triangles,
            boneWeights = mesh.boneWeights,
            bindposes = mesh.bindposes,
            indexFormat = mesh.indexFormat,
            subMeshCount = mesh.subMeshCount,
            hideFlags = mesh.hideFlags,
            bounds = mesh.bounds
        };
    }
    public static void TranslateMesh(Mesh mesh, Vector3 translation)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            meshVertices[i] += translation;
        }

        mesh.vertices = meshVertices;
    }
    public static void ScaleMesh(Mesh mesh, Vector3 scale)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            var vertex = meshVertices[i];
            meshVertices[i] = new Vector3(scale.x * vertex.x, scale.y * vertex.y, scale.z * vertex.z);
        }

        mesh.vertices = meshVertices;
    }
    public static void RotateMesh(Mesh mesh, Quaternion rotation)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            var vertex = meshVertices[i];
            meshVertices[i] = rotation * vertex;
        }

        mesh.vertices = meshVertices;
    }
}