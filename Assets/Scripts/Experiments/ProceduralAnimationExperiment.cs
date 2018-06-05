using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimationExperiment : MonoBehaviour
{
    public const double InchesInFoot = 12;
    public const double CentimetersInInch = 2.54;
    public const double CentimetersInMeter = 100;
    public const double FeetInMeter = CentimetersInMeter / CentimetersInInch / InchesInFoot;
    public const double InchesInMeter = CentimetersInMeter / CentimetersInInch;

    Bone humanSkeleton;
    GameObject humanSkeletonObject;
    GameObject target;

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

    private Bone? FindBoneByName(Bone skeleton, string boneName)
    {
        if (skeleton.Name == boneName) return skeleton;

        foreach (var child in skeleton.Children)
        {
            var foundBone = FindBoneByName(child, boneName);
            if (foundBone != null) return foundBone;
        }

        return null;
    }

    private void Start()
    {
        var boneThickness = 0.1f;

        humanSkeleton = new Bone
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

        humanSkeletonObject = CreateSkeletonObject(humanSkeleton, Vector3.zero, boneThickness);

        target = new GameObject("target");
        target.transform.position = new Vector3(0, -0.6f, 0.4f);

        RunIk();
    }
    private void Update()
    {
        RunIk();
    }

    private void RunIk()
    {
        var rightUpperLegBone = FindBoneByName(humanSkeleton, "rightUpperLeg");
        var rightUpperLegObject = humanSkeletonObject.FindDescendant("rightUpperLeg");
        var rightLowerLegBone = FindBoneByName(humanSkeleton, "rightLowerLeg");
        var rightLowerLegObject = humanSkeletonObject.FindDescendant("rightLowerLeg");
        var rightFootBone = FindBoneByName(humanSkeleton, "rightFoot");
        var rightFootObject = humanSkeletonObject.FindDescendant("rightFoot");

        var targetRightFootPosition = new Vector3(rightUpperLegObject.transform.position.x, target.transform.position.y, target.transform.position.z);
        var bone1Forward = rightUpperLegObject.transform.parent.forward;
        Quaternion bone1Orientation, bone2Orientation;
        TwoBoneIk(
            rightUpperLegObject.transform.position, bone1Forward, rightUpperLegBone.Value.Length,
            rightLowerLegBone.Value.Length, targetRightFootPosition, Vector3.up, true,
            out bone1Orientation, out bone2Orientation
        );

        rightUpperLegObject.transform.localRotation = bone1Orientation;
        rightLowerLegObject.transform.localRotation = bone2Orientation;
    }

    private float LawOfCosinesSolveForAGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos((-(a * a) + (b * b) + (c * c)) / (2 * b * c));
    }
    private float LawOfCosinesSolveForBGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos(((a * a) + -(b * b) + (c * c)) / (2 * a * c));
    }
    private float LawOfCosinesSolveForCGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos(((a * a) + (b * b) + -(c * c)) / (2 * a * b));
    }

    private void GetTwoBoneIkAngles(
        float bone1Length, float bone2Length, float targetDistance, bool getPositiveAngleSolution,
        out float theta1InRadians, out float theta2InRadians)
    {
        if ((bone1Length + bone2Length) < targetDistance)
        {
            theta1InRadians = 0;
            theta2InRadians = 0;
        }
        else if (Mathf.Abs(bone1Length - bone2Length) > targetDistance)
        {
            theta1InRadians = 0;
            theta2InRadians = Mathf.PI;
        }
        else
        {
            var a = bone2Length;
            var b = bone1Length;
            var c = targetDistance;
            var A = LawOfCosinesSolveForAGivenThreeSideLengths(a, b, c);
            var C = LawOfCosinesSolveForCGivenThreeSideLengths(a, b, c);

            if (getPositiveAngleSolution)
            {
                theta1InRadians = A;
                theta2InRadians = -(Mathf.PI - C);
            }
            else
            {
                theta1InRadians = -A;
                theta2InRadians = Mathf.PI - C;
            }
        }
    }
    private void ApplyTwoBoneIkThetas(
        float theta1InRadians, float theta2InRadians, bool usePositiveAngleSolution,
        Vector3 bone1Position, Vector3 bone1Forward,
        Vector3 targetPosition, Vector3 upDirection,
        out Quaternion bone1Orientation, out Quaternion bone2Orientation
    )
    {
        var targetOffset = targetPosition - bone1Position;
        var ikPlane = new Plane(
            bone1Position,
            bone1Position + upDirection,
            targetPosition
        );
        var xAxisOnIkPlane = targetOffset.normalized;
        var yAxisOnIkPlane = Vector3Extensions.Reject(upDirection, xAxisOnIkPlane);
        var zAxisOnIkPlane = Vector3.Cross(xAxisOnIkPlane, yAxisOnIkPlane);
        var bone1ForwardOnIkPlane = Vector3.ProjectOnPlane(bone1Forward, ikPlane.normal);
        var bone1ForwardInIkBasis = new Vector2(
            Vector3Extensions.ScalarProject(bone1ForwardOnIkPlane, xAxisOnIkPlane),
            Vector3Extensions.ScalarProject(bone1ForwardOnIkPlane, yAxisOnIkPlane)
        );
        Debug.DrawLine(bone1Position, bone1Position + xAxisOnIkPlane);
        Debug.DrawLine(bone1Position, bone1Position + yAxisOnIkPlane);
        Debug.DrawLine(bone1Position, bone1Position + zAxisOnIkPlane);
        Debug.DrawLine(bone1Position, bone1Position + bone1ForwardOnIkPlane);

        var baseThetaInRadians = -Mathf.Atan2(bone1ForwardInIkBasis.y, bone1ForwardInIkBasis.x);

        var bone1AngleInRadians = baseThetaInRadians + theta1InRadians;
        var bone2AngleInRadians = theta2InRadians;

        bone1Orientation = Quaternion.AngleAxis(
            Mathf.Rad2Deg * bone1AngleInRadians, zAxisOnIkPlane
        );
        bone2Orientation = Quaternion.AngleAxis(
            Mathf.Rad2Deg * bone2AngleInRadians, zAxisOnIkPlane
        );
    }
    private void TwoBoneIk(
        Vector3 bone1Position, Vector3 bone1Forward, float bone1Length, float bone2Length,
        Vector3 targetPosition, Vector3 upDirection, bool getPositiveAngleSolution,
        out Quaternion bone1Orientation, out Quaternion bone2Orientation
    )
    {
        var targetDistance = Vector3.Distance(bone1Position, targetPosition);
        float theta1InRadians, theta2InRadians;
        GetTwoBoneIkAngles(
            bone1Length, bone2Length, targetDistance, getPositiveAngleSolution,
            out theta1InRadians, out theta2InRadians
        );

        ApplyTwoBoneIkThetas(
            theta1InRadians, theta2InRadians, getPositiveAngleSolution,
            bone1Position, bone1Forward, targetPosition, upDirection,
            out bone1Orientation, out bone2Orientation  
        );
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