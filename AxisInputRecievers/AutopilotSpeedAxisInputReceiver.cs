using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class AutopilotSpeedAxisInputReceiver : AxisInputReceiver
{
    TextMeshProUGUI c1;
    TextMeshProUGUI c2;
    TextMeshProUGUI c3;
    // Text c4;
    float lastPercent;

    public override void OnStart() {
        Debug.Log("AutopilotSpeedAxisInputReceiver.OnStart");

        c1 = this.transform.Find("C1").GetComponent<TextMeshProUGUI>();
        c2 = this.transform.Find("C2").GetComponent<TextMeshProUGUI>();
        c3 = this.transform.Find("C3").GetComponent<TextMeshProUGUI>();
        // c4 = this.transform.Find("C4").GetComponent<Text>();

        if (lastPercent != null) {
            OnPercent(lastPercent);
        }
    }

    public override void OnPercent(float percentOutOf100) {
        lastPercent = percentOutOf100;
        UpdateSpeedWithPercent(percentOutOf100);
    }

    void UpdateSpeedWithPercent(float percentOutOf100) {
        if (c1 == null) {
            return;
        }

        var newSpeed = 400 * (percentOutOf100 / 100);
        var newSpeedString = newSpeed.ToString("0");

        if (newSpeedString.Length == 1) {
            newSpeedString = "00" + newSpeedString;
        } else if (newSpeedString.Length == 2) {
            newSpeedString = "0" + newSpeedString;
        }

        c1.text = newSpeedString[0].ToString();
        c2.text = newSpeedString[1].ToString();
        c3.text = newSpeedString[2].ToString();

        Debug.Log("AutopilotSpeedAxisInputReceiver.UpdateSpeedWithPercent %" + percentOutOf100 + " => " + newSpeedString);
    }
}
