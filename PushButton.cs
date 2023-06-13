
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PushButton : UdonSharpBehaviour
{
    public bool isSeqOneOn = false;
    [UdonSynced]
    bool syncedIsSeqOneOn = false;

    public bool isSeqTwoOn = false;
    [UdonSynced]
    bool syncedIsSeqTwoOn = false;

    public bool isSeqTwoTogglable = true;
    public bool isCombined = false;
    public bool isPulledOut = false;
    public Material textSeqDefaultMaterial;
    public Material textSeqOneOnMaterial;
    public Material textSeqTwoOnMaterial;

    Transform seqOne;
    Transform seqTwo;
    float timeToUpdateRenderer;
    BoxCollider boxCollider;
    bool hasFingerEnteredCollider = false;
    float timeBeforeNextCollisionCheck = -1;
    bool isOwner;

    // debug
    Transform fakeFinger;

    void Start() {
        for (int i = 0; i < this.transform.childCount; i++) {
            Transform child = this.transform.GetChild(i);

            if (child.name.Contains("SEQ1")) {
                seqOne = child;
            }
            if (child.name.Contains("SEQ2")) {
                seqTwo = child;
            }
        }

        UpdateSeqOneRenderer();
        UpdateSeqTwoRenderer();
        
        boxCollider = this.gameObject.GetComponent<BoxCollider>();
        
        #if UNITY_EDITOR
        fakeFinger = GameObject.Find("/FakeHand/Index/IndexDistal").transform;
        #endif

        BeginDetectingFingerPress();
    }

    public void BeginDetectingFingerPress() {
        DetectFingerPress();
        SendCustomEventDelayedFrames(nameof(BeginDetectingFingerPress), 5);
    }

    //////////////

    public override void OnPlayerJoined(VRCPlayerApi newPlayer) {
        SyncVarsToOtherPlayers();

        if (newPlayer.playerId == Networking.LocalPlayer.playerId) {
            UpdateRenderers();
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi newOwner) {
        Debug.Log("PushButton \"" + GetDisplayName() + "\"  has new owner \"" + newOwner.displayName + "\"");

        isOwner = Networking.IsOwner(this.gameObject);

        if (isOwner) {
            Debug.Log("That is me!");
        }

        SyncVarsToOtherPlayers();
    }

    public override void OnDeserialization() {
        isSeqOneOn = syncedIsSeqOneOn;
        isSeqTwoOn = syncedIsSeqTwoOn;
    }

    //////////////

    void SyncVarsToOtherPlayers() {
        if (!GetIsOwner()) {
            return;
        }

        RequestSerialization();
    }

    string GetDisplayName() {
        return this.gameObject.name;
    }

    void VibrateController() {
        Networking.LocalPlayer.PlayHapticEventInHand(VRC.SDK3.Components.VRCPickup.PickupHand.Right, 1f, 1f, 1f);
    }

    bool GetIsOwner() {
        if (isOwner == null) {
            isOwner = Networking.IsOwner(this.gameObject);
        }
        return isOwner;
    }

    void BecomeOwner() {
        Debug.Log("PushButton \"" + GetDisplayName() + "\" local player wants to own it");
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        isOwner = true;
    }

    Vector3 GetIndexFingerTipPosition() {
        // TODO: Calculate the finger tip somehow

#if UNITY_EDITOR
        return fakeFinger.position;
        #else
        if (Networking.LocalPlayer == null) {
            return Vector3.zero;
        }

        var bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexDistal);

        if (bonePosition == Vector3.zero) {
            bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexIntermediate);

            if (bonePosition == Vector3.zero) {
                bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexProximal);

                if (bonePosition == Vector3.zero) {
                    return GetHandPosition();
                }
            }
        }

        return bonePosition;
#endif
    }

    Vector3 GetHandPosition() {
        if (Networking.LocalPlayer == null) {
            return Vector3.zero;
        }

        var trackingData = Networking.LocalPlayer.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.RightHand);
        return trackingData.position;
    }

    bool GetIsIndexFingerInsideCollider() {
        Bounds colliderBounds = boxCollider.bounds;

        var indexFingerPosition = GetIndexFingerTipPosition();

        bool isInside = colliderBounds.Contains(indexFingerPosition);

        return isInside;
    }

    void DetectFingerPress() {
        // prevent edge case where boxcollider physically moves away from finger on push in so triggers another collision
        if (timeBeforeNextCollisionCheck != -1) {
            if (Time.time > timeBeforeNextCollisionCheck) {
                timeBeforeNextCollisionCheck = -1;
            }
            return;
        }

        var isIndexFingerInsideCollider = GetIsIndexFingerInsideCollider();

        if (!hasFingerEnteredCollider) {
            if (isIndexFingerInsideCollider) {
                hasFingerEnteredCollider = true;

                OnFingerInteract();

                timeBeforeNextCollisionCheck = Time.time + 0.5f; // Time.time in seconds
            }
        } else {
            if (!isIndexFingerInsideCollider) {
                hasFingerEnteredCollider = false;
            }
        }
    }

    public void UpdateRenderers() {
        Debug.Log("PushButton \"" + this.gameObject.name + "\" updating renderers with  1=" + (isSeqOneOn ? "ON" : "OFF") + "  2=" + (isSeqTwoOn ? "ON" : "OFF") + "");
        UpdateSeqOneRenderer();
        UpdateSeqTwoRenderer();
    }

    void OnFingerInteract() {
        Debug.Log("PushButton \"" + this.gameObject.name + "\" has been interacted with");

        if (!GetIsOwner()) {
            BecomeOwner();
        } else {
            Debug.Log("I am already the owner of \"" + this.gameObject.name + "\"");
        }

        ToggleState();

        syncedIsSeqOneOn = isSeqOneOn;
        syncedIsSeqTwoOn = isSeqTwoOn;
        
        VibrateController();

        RequestSerialization();
        
        TellAllPlayersToPlayAnimation();
    }

    void TellAllPlayersToPlayAnimation() {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "PlayAnimation");
    }

    public void PlayAnimation() {
        Debug.Log("PushButton \"" + this.gameObject.name + "\" playing animation");

        if (isPulledOut != true) {
            PlayPushDownAnimation();
        } else {
            PlayPullOutAnimation();
        }

        SendCustomEventDelayedSeconds(nameof(UpdateRenderers), 0.3f);
    }

    void ToggleState() {
        if (isSeqTwoTogglable) {
            isSeqTwoOn = !isSeqTwoOn;
        } else {
            isSeqOneOn = !isSeqOneOn;
        }
    }

    void UpdateSeqOneRenderer() {
        if (seqOne == null || textSeqOneOnMaterial == null) {
            return;
        }

        var renderer = seqOne.GetComponent<Renderer>();
        renderer.sharedMaterial = isSeqOneOn || (isCombined && isSeqTwoOn) ? textSeqOneOnMaterial : textSeqDefaultMaterial;
    }

    void UpdateSeqTwoRenderer() {
        if (seqTwo == null || textSeqTwoOnMaterial == null) {
            return;
        }

        var renderer = seqTwo.GetComponent<Renderer>();
        renderer.sharedMaterial = isSeqTwoOn || (isCombined && isSeqOneOn) ? textSeqTwoOnMaterial : textSeqDefaultMaterial;
    }

    void PlayPushDownAnimation() {
        var animator = this.gameObject.GetComponent<Animator>();

        if (animator == null) {
            Debug.Log("No animator found");
            return;
        }

        animator.SetTrigger("PushTheButton");
    }

    void PlayPullOutAnimation() {
        var animator = this.gameObject.GetComponent<Animator>();

        if (animator == null) {
            Debug.Log("No animator found");
            return;
        }

        animator.SetTrigger("PullTheButton");
    }
}
