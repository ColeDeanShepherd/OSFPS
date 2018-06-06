using System.Collections.Generic;
using UnityEngine;

public struct Bone
{
    public static Bone? FindBoneByName(Bone skeleton, string boneName)
    {
        if (skeleton.Name == boneName) return skeleton;

        if (skeleton.Children != null)
        {
            foreach (var child in skeleton.Children)
            {
                var foundBone = FindBoneByName(child, boneName);
                if (foundBone != null) return foundBone;
            }
        }

        return null;
    }
    public static GameObject CreateSkeletonObject(Bone skeletonRoot, Vector3 localPosition, float thickness)
    {
        var skeletonObject = CreateBoneObject(skeletonRoot, thickness);
        var skeletonRootEndOffsetFromStart = skeletonRoot.EndOffsetFromStart;

        foreach (var childBone in skeletonRoot.Children)
        {
            var childObject = CreateSkeletonObject(childBone, skeletonRoot.Length * Vector3.forward, thickness);
            childObject.transform.SetParent(skeletonObject.transform);
        }

        skeletonObject.transform.localPosition = localPosition + skeletonRoot.StartOffset;
        skeletonObject.transform.localRotation = skeletonRoot.LocalOrientation;

        return skeletonObject;
    }
    public static GameObject CreateBoneObject(Bone bone, float thickness)
    {
        var boneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boneObject.name = bone.Name;

        var meshFilter = boneObject.GetComponent<MeshFilter>();

        // create bone mesh
        var boneMesh = MeshExtensions.CopyMesh(meshFilter.mesh);
        MeshExtensions.ScaleMesh(boneMesh, new Vector3(thickness / 2, thickness / 2, bone.Length));

        MeshExtensions.TranslateMesh(boneMesh, new Vector3(0, 0, bone.Length / 2));

        meshFilter.mesh = boneMesh;

        boneObject.GetComponent<MeshFilter>();

        Object.DestroyImmediate(boneObject.GetComponent<BoxCollider>());

        return boneObject;
    }

    public string Name;
    public float Length;
    public Quaternion LocalOrientation;
    public Vector3 StartOffset;
    public List<Bone> Children;

    public Vector3 EndOffsetFromStart
    {
        get
        {
            return LocalOrientation * Vector3.forward;
        }
    }
}