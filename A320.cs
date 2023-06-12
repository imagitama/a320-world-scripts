
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif

public class A320 : UdonSharpBehaviour
{
    // debug only
    bool currentValueOfOnDrop = false;
    Transform fakeHand;
    Transform fakeFinger;
    Transform handPositionBall;
    Transform fingerPositionBall;
    bool isDebuggingEnabled = false;

    void Start() {
        isDebuggingEnabled = GameObject.Find("/DebuggingStuff") != null;

#if UNITY_EDITOR
        fakeHand = GameObject.Find("/FakeHand").transform;
#endif
    }

    void Update() {
        if (!isDebuggingEnabled) {
            return;
        }

        MoveDebugBallsToHand();
    }

    void MoveDebugBallsToHand() {
        if (handPositionBall == null) {
            handPositionBall = GameObject.Find("/DebuggingStuff/HandPosition").transform;
        }
        if (fingerPositionBall == null) {
            fingerPositionBall = GameObject.Find("/DebuggingStuff/FingerPosition").transform;
        }
        if (fakeHand == null) {
            fakeHand = GameObject.Find("/FakeHand").transform;
        }
        if (fakeFinger == null) {
            fakeFinger = fakeHand.Find("Index/IndexDistal");
        }

        handPositionBall.position = GetHandPosition();
        fingerPositionBall.position = GetIndexFingerTipPosition();
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [DrawGizmo (GizmoType.Selected | GizmoType.NonSelected)]
    void OnDrawGizmos() {
        if (fakeHand == null) {
            fakeHand = GameObject.Find("/FakeHand").transform;
        }
        if (fakeFinger == null) {
            fakeFinger = fakeHand.Find("Index/IndexDistal");
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetHandPosition(), 0.0025f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetIndexFingerTipPosition(), 0.0025f);
    }
#endif

    public override void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) {
#if UNITY_EDITOR
        currentValueOfOnDrop = !currentValueOfOnDrop;
        value = currentValueOfOnDrop;
#endif

        if (value == true) {
            Debug.Log("Player grab");
        } else {
            Debug.Log("Player drop");
        }
    }
    
    Vector3 GetHandPosition() {
        #if UNITY_EDITOR
        return fakeHand.position;
        #else
        var trackingData = Networking.LocalPlayer.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.RightHand);
        return trackingData.position;
        #endif
    }

    Vector3 GetBonePosition(HumanBodyBones humanBodyBone) {
        #if UNITY_EDITOR
        if (humanBodyBone == HumanBodyBones.RightIndexDistal) {
            return fakeHand.Find("Index/IndexDistal").position;
        } else {
            return fakeHand.Find("Index").position;
        }
        #else
        return Networking.LocalPlayer.GetBonePosition(humanBodyBone);
        #endif
    }

    Quaternion GetBoneRotation(HumanBodyBones humanBodyBone) {
        #if UNITY_EDITOR
        if (humanBodyBone == HumanBodyBones.RightIndexDistal) {
            return fakeHand.Find("Index/IndexDistal").rotation;
        } else {
            return fakeHand.Find("Index").rotation;
        }
        #else
        return Networking.LocalPlayer.GetBoneRotation(humanBodyBone);
        #endif
    }

    Vector3 GetIndexFingerTipPosition() {
        return GetBonePosition(HumanBodyBones.RightIndexDistal);
    }
}
