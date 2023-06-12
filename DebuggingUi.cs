
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
        text.text = "Waiting";
    }

    public override void OnPercent(float percent) {
        UpdateDisplay("%" + percent);
    }

    public override void OnIndex(int index) {
        UpdateDisplay("Idx: " + index);
    }

    void UpdateDisplay(string newText) {
        if (text == null) {
            return;
        }
        text.text = newText;
    }
}
