using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class StrobeLightAxisInputReceiver : AxisInputReceiver
{
    GameObject lightLeft;
    GameObject lightRight;
    GameObject lightTail;
    int lastIndex;

    public override void OnStart() {
        // NOTE: cannot toggle component enabled as animation will force it back on
        lightLeft = this.transform.Find("WingStrobeLeft").gameObject;
        lightRight = this.transform.Find("WingStrobeRight").gameObject;
        lightTail = this.transform.Find("TailStrobeLight").gameObject;

        if (lastIndex != null) {
            OnIndex(lastIndex);
        }
    }

    public override void OnIndex(int index) {
        lastIndex = index;

        // index 1 is off
        var isEnabled = index == 0;
        ChangeLights(isEnabled);
    }

    void ChangeLights(bool isEnabled) {
        if (lightLeft == null || lightRight == null || lightTail == null) {
            return;
        }
        Debug.Log("StrobeLightAxisInputReceiver.ChangeLights " + (isEnabled ? "Enabled" : "Disabled"));
        lightLeft.SetActive(isEnabled);
        lightRight.SetActive(isEnabled);
        lightTail.SetActive(isEnabled);
    }
}
