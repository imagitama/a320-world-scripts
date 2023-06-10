
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class PushButton : UdonSharpBehaviour
{
    // [UdonSynced]
    public bool isSeqOneOn = false;
    // [UdonSynced]
    public bool isSeqTwoOn = false;

    public bool isSeqTwoTogglable = true;
    public bool isCombined = false;
    public bool isPulledOut = false;
    public Material textSeqDefaultMaterial;
    public Material textSeqOneOnMaterial;
    public Material textSeqTwoOnMaterial;

    Transform seqOne;
    Transform seqTwo;
    float timeToUpdateRenderer;

    void Start() {
        for (int i = 0; i < this.transform.childCount; i++)
        {
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
    }

    void Update() {
        CheckIfSeqTwoNeedsUpdating();
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

    public override void Interact() {
        // NOTE: If 2 people interact at same time it might compete
        SendCustomNetworkEvent(NetworkEventTarget.All, "PushTheButton");
    }

    void ToggleState() {
        if (isSeqTwoTogglable) {
            isSeqTwoOn = !isSeqTwoOn;
        } else {
            isSeqOneOn = !isSeqOneOn;
        }
    }

    public void PushTheButton() {
        ToggleState();

        if (isPulledOut != true) {
            PlayPushDownAnimation();
        } else {
            PlayPullOutAnimation();
        }

        UpdateRendererAfterDelay();
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
