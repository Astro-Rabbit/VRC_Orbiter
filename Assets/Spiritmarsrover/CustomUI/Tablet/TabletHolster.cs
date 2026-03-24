using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TabletHolster : UdonSharpBehaviour
{
    [Header("References")]
    public Transform adjustableChild; // The child object with the VRC_Pickup and Dock Collider
    public VRC_Pickup holsterPickup;

    [Header("Settings")]
    public HumanBodyBones targetBone = HumanBodyBones.LeftLowerArm;
    public bool isAdjustable = false;

    private VRCPlayerApi _localPlayer;
    private bool _isHeld = false;

    // Saved local offsets (relative to the holster root)
    //private Vector3 _savedLocalPos;
    //private Quaternion _savedLocalRot;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_localPlayer == null) return;

        // Initialize saved positions from the child's current placement in the editor
        //_savedLocalPos = adjustableChild.localPosition;
        //_savedLocalRot = adjustableChild.localRotation;

        holsterPickup.pickupable = isAdjustable;
    }

    //public void SetTargetBone(int boneIndex)
    //{
    //    switch (boneIndex)
    //    {
    //        case 0: targetBone = HumanBodyBones.Chest; break;
    //        case 1: targetBone = HumanBodyBones.Spine; break;
    //        case 2: targetBone = HumanBodyBones.LeftUpperArm; break;
    //        case 3: targetBone = HumanBodyBones.RightUpperArm; break;
    //        case 4: targetBone = HumanBodyBones.LeftLowerArm; break;
    //        case 5: targetBone = HumanBodyBones.RightLowerArm; break;
    //        case 6: targetBone = HumanBodyBones.LeftUpperLeg; break;
    //        case 7: targetBone = HumanBodyBones.RightUpperLeg; break;
    //        default: targetBone = HumanBodyBones.LeftLowerArm; break;
    //    }
    //}
    public void SetChest()
    {
        targetBone = HumanBodyBones.Chest;
    }
    public void SetSpine()
    {
        targetBone = HumanBodyBones.Spine;
    }
    public void SetRightUpperArm()
    {
        targetBone = HumanBodyBones.RightUpperArm;
        ResetHolsterPosition();
    }
    public void SetLeftUpperArm()
    {
        targetBone = HumanBodyBones.LeftUpperArm;
        ResetHolsterPosition();
    }
    public void SetRightLowerArm()
    {
        targetBone = HumanBodyBones.RightLowerArm;
        ResetHolsterPosition();
    }
    public void SetLeftLowerArm()
    {
        targetBone = HumanBodyBones.LeftLowerArm;
        ResetHolsterPosition();
    }
    public void SetRightUpperLeg()
    {
        targetBone = HumanBodyBones.RightUpperLeg;
        ResetHolsterPosition();
    }
    public void SetLeftUpperLeg()
    {
        targetBone = HumanBodyBones.LeftUpperLeg;
        ResetHolsterPosition();
    }

    public void ToggleLock()
    {
        isAdjustable = !isAdjustable;
        holsterPickup.pickupable = isAdjustable;
        if (!isAdjustable && _isHeld) holsterPickup.Drop();
    }

    public override void OnPickup()
    {
        _isHeld = true;
    }

    public override void OnDrop()
    {
        _isHeld = false;
        // Save the current local position so it stays here relative to the bone
        //_savedLocalPos = adjustableChild.localPosition;
        //_savedLocalRot = adjustableChild.localRotation;
    }

    public void ResetHolsterPosition()
    {
        adjustableChild.localPosition = Vector3.zero;
        adjustableChild.localRotation = Quaternion.Euler(Vector3.zero);
    }

    public override void PostLateUpdate()
    {
        if (_localPlayer == null) return;

        // 1. Move the ROOT to the bone
        Vector3 bonePos = _localPlayer.GetBonePosition(targetBone);
        Quaternion boneRot = _localPlayer.GetBoneRotation(targetBone);

        // If the bone returns Vector3.zero, the avatar might not have that bone
        if (bonePos != Vector3.zero)
        {
            transform.position = bonePos;
            transform.rotation = boneRot;
        }

        // 2. Handle the adjustable child
        //if (!_isHeld)
        //{
        //    // Lock the child to its last saved fine-tuned position
        //    adjustableChild.localPosition = _savedLocalPos;
        //    adjustableChild.localRotation = _savedLocalRot;
        //}
        // If _isHeld is true, the VRC_Pickup is naturally moving the child, so we do nothing.
    }
}