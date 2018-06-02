using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimationExperiment : MonoBehaviour
{
    public const double InchesInFoot = 12;
    public const double CentimetersInInch = 2.54;
    public const double CentimetersInMeter = 100;
    public const double FeetInMeter = CentimetersInMeter / CentimetersInInch / InchesInFoot;
    public const double InchesInMeter = CentimetersInMeter / CentimetersInInch;

    public struct Bone
    {
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

    private void Start()
    {
        var boneThickness = 0.1f;

        var humanSkeleton = new Bone
        {
            Name = "root",
            Length = 0,
            LocalOrientation = Quaternion.AngleAxis(-90, Vector3.right),
            Children = new List<Bone>()
        };

        var torsoBone = new Bone
        {
            Name = "torso",
            Length = (float)HumanTorsoLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        humanSkeleton.Children.Add(torsoBone);

        var neckBone = new Bone
        {
            Name = "neck",
            Length = (float)HumanNeckLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        torsoBone.Children.Add(neckBone);

        var headBone = new Bone
        {
            Name = "head",
            Length = (float)HumanHeadHeight,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        neckBone.Children.Add(headBone);

        var leftArmBoneChain = CreateHumanArmBoneChain(isLeft: true);
        torsoBone.Children.Add(leftArmBoneChain);
        var rightArmBoneChain = CreateHumanArmBoneChain(isLeft: false);
        torsoBone.Children.Add(rightArmBoneChain);

        var leftLegBoneChain = CreateHumanLegBoneChain(isLeft: true);
        humanSkeleton.Children.Add(leftLegBoneChain);
        var rightLegBoneChain = CreateHumanLegBoneChain(isLeft: false);
        humanSkeleton.Children.Add(rightLegBoneChain);

        CreateSkeletonObject(humanSkeleton, Vector3.zero, boneThickness);
    }

    public const double HumanHeight = 1.9;
    public const double HumanHeadHeight = HumanHeight / 8;
    public const double HumanNeckLength = HumanHeadHeight / 3;
    public const double HumanTorsoLength = 2 * HumanHeadHeight;
    public const double HumanUpperArmLength = (5.0 / 3) * HumanHeadHeight;
    public const double HumanLowerArmLength = (5.0 / 4) * HumanHeadHeight;
    public const double HumanHandLength = (3.0 / 4) * HumanHeadHeight;
    public const double HumanUpperLegLength = (5.0 / 3) * HumanHeadHeight;
    public const double HumanUpperLegToHipsLength = (7.0 / 3) * HumanHeadHeight;
    public const double HumanLowerLegLength = (7.0 / 3) * HumanHeadHeight;
    public const double HumanFootLength = HumanHeadHeight;

    private Bone CreateHumanArmBoneChain(bool isLeft)
    {
        var namePrefix = isLeft ? "left" : "right";
        var upperArmDirection = isLeft ? Vector3.left : Vector3.right;

        var upperArmBone = new Bone
        {
            Name = $"{namePrefix}UpperArm",
            Length = (float)HumanUpperArmLength,
            LocalOrientation = Quaternion.LookRotation(upperArmDirection, Vector3.up),
            StartOffset = (float)HumanHeadHeight * upperArmDirection,
            Children = new List<Bone>()
        };

        var lowerArmBone = new Bone
        {
            Name = $"{namePrefix}LowerArm",
            Length = (float)HumanUpperArmLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        upperArmBone.Children.Add(lowerArmBone);

        var handBone = new Bone
        {
            Name = $"{namePrefix}Hand",
            Length = (float)HumanHandLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        lowerArmBone.Children.Add(handBone);

        return upperArmBone;
    }
    private Bone CreateHumanLegBoneChain(bool isLeft)
    {
        var namePrefix = isLeft ? "left" : "right";
        var upperLegStartOffsetXDistance = HumanHeadHeight / 2;
        var upperLegStartOffsetX = isLeft ?
            -upperLegStartOffsetXDistance
            : upperLegStartOffsetXDistance;

        var upperLegBone = new Bone
        {
            Name = $"{namePrefix}UpperLeg",
            Length = (float)HumanUpperLegLength,
            LocalOrientation = Quaternion.AngleAxis(180, Vector3.right),
            StartOffset = new Vector3((float)upperLegStartOffsetX, 0, -(float)((2.0 / 3) * HumanHeadHeight)),
            Children = new List<Bone>()
        };

        var lowerLegBone = new Bone
        {
            Name = $"{namePrefix}LowerLeg",
            Length = (float)HumanLowerLegLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        upperLegBone.Children.Add(lowerLegBone);

        var footBone = new Bone
        {
            Name = $"{namePrefix}Foot",
            Length = (float)HumanFootLength,
            LocalOrientation = Quaternion.LookRotation(Vector3.up, Vector3.up),
            Children = new List<Bone>()
        };
        lowerLegBone.Children.Add(footBone);

        return upperLegBone;
    }

    private Mesh CopyMesh(Mesh mesh)
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
    private void TranslateMesh(Mesh mesh, Vector3 translation)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            meshVertices[i] += translation;
        }

        mesh.vertices = meshVertices;
    }
    private void ScaleMesh(Mesh mesh, Vector3 scale)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            var vertex = meshVertices[i];
            meshVertices[i] = new Vector3(scale.x * vertex.x, scale.y * vertex.y, scale.z * vertex.z);
        }

        mesh.vertices = meshVertices;
    }
    private void RotateMesh(Mesh mesh, Quaternion rotation)
    {
        var meshVertices = mesh.vertices;

        for (var i = 0; i < meshVertices.Length; i++)
        {
            var vertex = meshVertices[i];
            meshVertices[i] = rotation * vertex;
        }

        mesh.vertices = meshVertices;
    }

    private GameObject CreateSkeletonObject(Bone skeletonRoot, Vector3 localPosition, float thickness)
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
    private GameObject CreateBoneObject(Bone bone, float thickness)
    {
        var boneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boneObject.name = bone.Name;

        var meshFilter = boneObject.GetComponent<MeshFilter>();

        // create bone mesh
        var boneMesh = CopyMesh(meshFilter.mesh);
        ScaleMesh(boneMesh, new Vector3(thickness / 2, thickness / 2, bone.Length));

        TranslateMesh(boneMesh, new Vector3(0, 0, bone.Length / 2));

        meshFilter.mesh = boneMesh;

        boneObject.GetComponent<MeshFilter>();

        Object.DestroyImmediate(boneObject.GetComponent<BoxCollider>());

        return boneObject;
    }
}