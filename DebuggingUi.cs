
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class DebuggingUi : AxisInputReceiver
{
    Text text;

    public override void OnStart() {
        text = this.gameObject.GetComponent<Text>();
        text.text = "Start";
    }

    public override void OnPercent(float percent) {
        UpdateDisplay(percent);
    }

    public override void OnIndex(int index) {
        Debug.Log("Index: " + index);
    }

    void UpdateDisplay(float percent) {
        text.text = "%" + percent;
    }
}
