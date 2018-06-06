using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimationExperiment : MonoBehaviour
{
    public const float StrideLength = 0.5f;
    public bool IsRightFootPlanted = false;

    public Vector3 LeftFootTargetPosition;
    public Vector3 RightFootTargetPosition;

    Bone humanSkeleton;
    GameObject humanSkeletonParentObject;
    GameObject humanSkeletonObject;
    GameObject leftFootObject;
    GameObject rightFootObject;
    GameObject leftHandTarget;
    GameObject rightHandTarget;
    GameObject leftFootTarget;
    GameObject rightFootTarget;

    float walkSpeed = 0.5f;

    private void Start()
    {
        var boneThickness = 0.1f;
        
        humanSkeleton = CreateHumanSkeleton();
        humanSkeletonObject = Bone.CreateSkeletonObject(humanSkeleton, Vector3.zero, boneThickness);

        var rootObject = humanSkeletonObject;
        rootObject.transform.position += 1.12f * Vector3.up;

        humanSkeletonParentObject = new GameObject();
        humanSkeletonObject.transform.parent = humanSkeletonParentObject.transform;

        var leftUpperArmObject = humanSkeletonObject.FindDescendant("leftUpperArm");
        leftHandTarget = new GameObject("leftHandTarget");

        var rightUpperArmObject = humanSkeletonObject.FindDescendant("rightUpperArm");
        rightHandTarget = new GameObject("rightHandTarget");

        var leftUpperLegObject = humanSkeletonObject.FindDescendant("leftUpperLeg");
        leftFootObject = humanSkeletonObject.FindDescendant("leftFoot");
        leftFootTarget = new GameObject("leftFootTarget");

        var rightUpperLegObject = humanSkeletonObject.FindDescendant("rightUpperLeg");
        rightFootObject = humanSkeletonObject.FindDescendant("rightFoot");
        rightFootTarget = new GameObject("rightFootTarget");

        IsRightFootPlanted = false;
        LeftFootTargetPosition = leftFootObject.transform.position;
        RightFootTargetPosition = rightFootObject.transform.position + new Vector3(0, 0, StrideLength);

        leftFootTarget.transform.position = LeftFootTargetPosition;
        rightFootTarget.transform.position = RightFootTargetPosition;
    }
    private void Update()
    {
        humanSkeletonParentObject.transform.position += Time.deltaTime * (walkSpeed * Vector3.forward);

        if (!IsRightFootPlanted)
        {
            if (Vector3.Distance(rightFootObject.transform.position, RightFootTargetPosition) < 0.01f)
            {
                IsRightFootPlanted = !IsRightFootPlanted;
                LeftFootTargetPosition += 2 * StrideLength * Vector3.forward;
            }
        }
        else
        {
            if (Vector3.Distance(leftFootObject.transform.position, LeftFootTargetPosition) < 0.01f)
            {
                IsRightFootPlanted = !IsRightFootPlanted;
                RightFootTargetPosition += 2 * StrideLength * Vector3.forward;
            }
        }

        var leftFootDirection = (LeftFootTargetPosition - leftFootTarget.transform.position).normalized;
        var leftFootVelocity = 2.5f * walkSpeed * leftFootDirection;
        leftFootTarget.transform.position += Time.deltaTime * leftFootVelocity;

        var rightFootDirection = (RightFootTargetPosition - rightFootTarget.transform.position).normalized;
        var rightFootVelocity = 2.5f * walkSpeed * rightFootDirection;
        rightFootTarget.transform.position += Time.deltaTime * rightFootVelocity;

        RunIk();
    }

    private void RunIk()
    {
        /*var elbowTargetOffset = new Vector3(0, -1, -1);

        var leftUpperArmBone = Bone.FindBoneByName(humanSkeleton, "leftUpperArm").Value;
        TwoBoneIk(
            humanSkeletonObject, leftUpperArmBone,
            leftHandTarget.transform.position, elbowTargetOffset
        );

        var rightUpperArmBone = Bone.FindBoneByName(humanSkeleton, "rightUpperArm").Value;
        TwoBoneIk(
            humanSkeletonObject, rightUpperArmBone,
            rightHandTarget.transform.position, elbowTargetOffset
        );*/

        var kneeTargetOffset = new Vector3(0, 1, 1);

        var leftUpperLegBone = Bone.FindBoneByName(humanSkeleton, "leftUpperLeg").Value;
        TwoBoneIk(
            humanSkeletonObject, leftUpperLegBone,
            leftFootTarget.transform.position, kneeTargetOffset
        );

        var rightUpperLegBone = Bone.FindBoneByName(humanSkeleton, "rightUpperLeg").Value;
        TwoBoneIk(
            humanSkeletonObject, rightUpperLegBone,
            rightFootTarget.transform.position, kneeTargetOffset
        );
    }
    private void TwoBoneIk(
        GameObject skeletonObject, Bone bone1,
        Vector3 targetPosition, Vector3 elbowTargetOffset
    )
    {
        var bone1Object = skeletonObject.FindDescendant(bone1.Name);
        var bone2 = bone1.Children[0];
        var bone2Object = skeletonObject.FindDescendant(bone2.Name);

        var targetElbowPosition = bone1Object.transform.position + elbowTargetOffset;

        GameObjectExtensions.TwoBoneIk(
            bone1Object.transform, bone1.Length,
            bone2Object.transform, bone2.Length,
            targetPosition, targetElbowPosition, true
        );
    }

    private Bone CreateHumanSkeleton()
    {
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
            Length = (float)Measurements.HumanTorsoLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        humanSkeleton.Children.Add(torsoBone);

        var neckBone = new Bone
        {
            Name = "neck",
            Length = (float)Measurements.HumanNeckLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        torsoBone.Children.Add(neckBone);

        var headBone = new Bone
        {
            Name = "head",
            Length = (float)Measurements.HumanHeadHeight,
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

        return humanSkeleton;
    }
    private Bone CreateHumanArmBoneChain(bool isLeft)
    {
        var namePrefix = isLeft ? "left" : "right";
        var upperArmDirection = isLeft ? Vector3.left : Vector3.right;

        var upperArmBone = new Bone
        {
            Name = $"{namePrefix}UpperArm",
            Length = (float)Measurements.HumanUpperArmLength,
            LocalOrientation = Quaternion.LookRotation(upperArmDirection, Vector3.up),
            StartOffset = (float)Measurements.HumanHeadHeight * upperArmDirection,
            Children = new List<Bone>()
        };

        var lowerArmBone = new Bone
        {
            Name = $"{namePrefix}LowerArm",
            Length = (float)Measurements.HumanUpperArmLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        upperArmBone.Children.Add(lowerArmBone);

        var handBone = new Bone
        {
            Name = $"{namePrefix}Hand",
            Length = (float)Measurements.HumanHandLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        lowerArmBone.Children.Add(handBone);

        return upperArmBone;
    }
    private Bone CreateHumanLegBoneChain(bool isLeft)
    {
        var namePrefix = isLeft ? "left" : "right";
        var upperLegStartOffsetXDistance = Measurements.HumanHeadHeight / 2;
        var upperLegStartOffsetX = isLeft ?
            -upperLegStartOffsetXDistance
            : upperLegStartOffsetXDistance;

        var upperLegBone = new Bone
        {
            Name = $"{namePrefix}UpperLeg",
            Length = (float)Measurements.HumanUpperLegLength,
            LocalOrientation = Quaternion.AngleAxis(180, Vector3.right),
            StartOffset = new Vector3(
                (float)upperLegStartOffsetX,
                0,
                -(float)((2.0 / 3) * Measurements.HumanHeadHeight)
            ),
            Children = new List<Bone>()
        };

        var lowerLegBone = new Bone
        {
            Name = $"{namePrefix}LowerLeg",
            Length = (float)Measurements.HumanLowerLegLength,
            LocalOrientation = Quaternion.identity,
            Children = new List<Bone>()
        };
        upperLegBone.Children.Add(lowerLegBone);

        var footBone = new Bone
        {
            Name = $"{namePrefix}Foot",
            Length = (float)Measurements.HumanFootLength,
            LocalOrientation = Quaternion.LookRotation(Vector3.up, Vector3.up),
            Children = new List<Bone>()
        };
        lowerLegBone.Children.Add(footBone);

        return upperLegBone;
    }
}