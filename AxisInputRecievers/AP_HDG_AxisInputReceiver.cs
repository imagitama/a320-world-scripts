using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class AP_HDG_AxisInputReceiver : AxisInputReceiver
{
    TextMeshProUGUI c1;
    TextMeshProUGUI c2;
    TextMeshProUGUI c3;
    int currentValue = 200;

    public override void OnStart() {
        c1 = this.transform.Find("C1").GetComponent<TextMeshProUGUI>();
        c2 = this.transform.Find("C2").GetComponent<TextMeshProUGUI>();
        c3 = this.transform.Find("C3").GetComponent<TextMeshProUGUI>();
    }

    public override void OnValueChange(int valueChange) {
        var rawNewValue = currentValue + valueChange;
        var newValue = 0;

        if (rawNewValue < 0) {
            newValue = 360 - rawNewValue;
        } else if (rawNewValue > 360) {
            newValue = rawNewValue - 360;
        } else {
            newValue = rawNewValue;
        }

        Debug.Log("OnValueChange  " + valueChange + "  current " + currentValue + "  new " + rawNewValue + " => " + newValue);

        currentValue = newValue;

        UpdateDisplayWithValue(currentValue);
    }

    void UpdateDisplayWithValue(int newValue) {
        if (c1 == null) {
            return;
        }

        var valueString = newValue.ToString().PadLeft(3, '0');

        c1.text = valueString[0].ToString();
        c2.text = valueString[1].ToString();
        c3.text = valueString[2].ToString();
    }
}
