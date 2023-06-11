
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

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

    bool needsToPlayAnimation = false;

    // debug
    Transform fakeFinger;

    void Start() {
        syncedIsSeqOneOn = isSeqOneOn;
        syncedIsSeqTwoOn = isSeqTwoOn;

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
    }

    void Update() {
        CheckIfSeqTwoNeedsUpdating();

        DetectFingerPress();
        
        UpdateVisually();
    }

    //////////////

    public override void OnDeserialization() {
        if (GetIsOwner()) {
            return;
        }

        if (isSeqOneOn != syncedIsSeqOneOn || isSeqTwoOn != syncedIsSeqTwoOn) {
            needsToPlayAnimation = true;
        }

        isSeqOneOn = syncedIsSeqOneOn;
        isSeqTwoOn = syncedIsSeqTwoOn;
    }

    //////////////

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    Vector3 GetIndexFingerPosition() {
        #if UNITY_EDITOR
        return fakeFinger.position;
        #else
        return Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexDistal);
        #endif
    }

    bool GetIsIndexFingerInsideCollider() {
        Bounds colliderBounds = boxCollider.bounds;

        var indexFingerPosition = GetIndexFingerPosition();

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

    void CheckIfSeqTwoNeedsUpdating() {
        if (timeToUpdateRenderer == 0f) {
            return;
        }

        if (Time.time >= timeToUpdateRenderer) {
            UpdateSeqOneRenderer();
            UpdateSeqTwoRenderer();
            timeToUpdateRenderer = 0f;
        }
    }

    void OnFingerInteract() {
        Debug.Log("PushButton \"" + this.gameObject.name + "\" has been interacted with");

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        ToggleState();

        syncedIsSeqOneOn = isSeqOneOn;
        syncedIsSeqTwoOn = isSeqTwoOn;

        needsToPlayAnimation = true;
    }

    void UpdateVisually() {
        if (!needsToPlayAnimation) {
            return;
        }

        needsToPlayAnimation = false;

        if (isPulledOut != true) {
            PlayPushDownAnimation();
        } else {
            PlayPullOutAnimation();
        }

        UpdateRendererAfterDelay();
    }

    void ToggleState() {
        if (isSeqTwoTogglable) {
            isSeqTwoOn = !isSeqTwoOn;
        } else {
            isSeqOneOn = !isSeqOneOn;
        }
    }

    void UpdateRendererAfterDelay() {
        timeToUpdateRenderer = Time.time + 0.3f;
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

        Debug.Log("Pushing button...");

        animator.SetTrigger("PushTheButton");
    }

    void PlayPullOutAnimation() {
        var animator = this.gameObject.GetComponent<Animator>();

        if (animator == null) {
            Debug.Log("No animator found");
            return;
        }

        Debug.Log("Pulling button...");

        animator.SetTrigger("PullTheButton");
    }
}
