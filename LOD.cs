
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class LOD : UdonSharpBehaviour
{
    public Transform[] highQualityObjects;
    public Transform[] lowQualityObjects;
    public bool invertCollision = false;
    public bool initiallyShowLowQuality = true;

    void Start()
    {
        if (initiallyShowLowQuality) {
            ShowLowQualityObjects();
        } else {
            ShowHighQualityObjects();
        }
    }

    void ShowHighQualityObjects() {
        Debug.Log("Showing high quality objects...");

        foreach (Transform lowQualityObject in lowQualityObjects) {
            lowQualityObject.gameObject.SetActive(false);
        }
        foreach (Transform highQualityObject in highQualityObjects) {
            highQualityObject.gameObject.SetActive(true);
        }
    }

    void ShowLowQualityObjects() {
        Debug.Log("Showing low quality objects...");

        foreach (Transform lowQualityObject in lowQualityObjects) {
            lowQualityObject.gameObject.SetActive(true);
        }
        foreach (Transform highQualityObject in highQualityObjects) {
            highQualityObject.gameObject.SetActive(false);
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
        if (player != Networking.LocalPlayer) {
            return;
        }

        if (invertCollision) {
            ShowLowQualityObjects();
        } else {
            ShowHighQualityObjects();
        } 
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player) {
        if (player != Networking.LocalPlayer) {
            return;
        }
        
        if (invertCollision) {
            ShowHighQualityObjects();
        } else {
            ShowLowQualityObjects();
        }
    }
}
