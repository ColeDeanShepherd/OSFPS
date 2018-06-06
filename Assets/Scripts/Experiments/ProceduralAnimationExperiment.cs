using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimationExperiment : MonoBehaviour
{
    Bone humanSkeleton;
    GameObject humanSkeletonObject;
    GameObject leftHandTarget;
    GameObject rightHandTarget;
    GameObject leftFootTarget;
    GameObject rightFootTarget;

    private void Start()
    {
        var boneThickness = 0.1f;

        humanSkeleton = CreateHumanSkeleton();
        humanSkeletonObject = Bone.CreateSkeletonObject(humanSkeleton, Vector3.zero, boneThickness);

        humanSkeletonObject.transform.Translate(Vector3.up);

        var leftUpperArmObject = humanSkeletonObject.FindDescendant("leftUpperArm");
        leftHandTarget = new GameObject("leftHandTarget");
        leftHandTarget.transform.position = new Vector3(leftUpperArmObject.transform.position.x, -0.6f, 0.4f);

        var rightUpperArmObject = humanSkeletonObject.FindDescendant("rightUpperArm");
        rightHandTarget = new GameObject("rightHandTarget");
        rightHandTarget.transform.position = new Vector3(rightUpperArmObject.transform.position.x, -0.6f, 0.4f);

        var leftUpperLegObject = humanSkeletonObject.FindDescendant("leftUpperLeg");
        leftFootTarget = new GameObject("leftFootTarget");
        leftFootTarget.transform.position = new Vector3(leftUpperLegObject.transform.position.x, -0.6f, 0.4f);

        var rightUpperLegObject = humanSkeletonObject.FindDescendant("rightUpperLeg");
        rightFootTarget = new GameObject("rightFootTarget");
        rightFootTarget.transform.position = new Vector3(rightUpperLegObject.transform.position.x, -0.6f, 0.4f);

        var torsoObject = humanSkeletonObject.FindDescendant("torso");
        torsoObject.transform.Translate(0.15f * Vector3.down);

        RunIk();
    }
    private void Update()
    {
        humanSkeletonObject.transform.position += Time.deltaTime * Vector3.forward;

        leftHandTarget.transform.position = new Vector3(
            leftHandTarget.transform.position.x,
            leftHandTarget.transform.position.y,
            humanSkeletonObject.transform.position.z
        );
        rightHandTarget.transform.position = new Vector3(
            rightHandTarget.transform.position.x,
            rightHandTarget.transform.position.y,
            humanSkeletonObject.transform.position.z
        );

        leftFootTarget.transform.position = new Vector3(
            leftFootTarget.transform.position.x,
            leftFootTarget.transform.position.y,
            humanSkeletonObject.transform.position.z
        );
        rightFootTarget.transform.position = new Vector3(
            rightFootTarget.transform.position.x,
            rightFootTarget.transform.position.y,
            humanSkeletonObject.transform.position.z
        );

        RunIk();
    }

    private void RunIk()
    {
        /*elbowTarget.transform.position = Quaternion.AngleAxis(-90, Vector3.right) * (
            (targetRightFootPosition - rightUpperLegObject.transform.position).normalized +
            new Vector3(rightUpperLegObject.transform.position.x, 0, 0)
        );*/

        var elbowTargetOffset = new Vector3(0, -1, -1);

        var leftUpperArmBone = Bone.FindBoneByName(humanSkeleton, "leftUpperArm").Value;
        TwoBoneIk(
            humanSkeletonObject, leftUpperArmBone,
            leftHandTarget.transform.position, elbowTargetOffset
        );

        var rightUpperArmBone = Bone.FindBoneByName(humanSkeleton, "rightUpperArm").Value;
        TwoBoneIk(
            humanSkeletonObject, rightUpperArmBone,
            rightHandTarget.transform.position, elbowTargetOffset
        );

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