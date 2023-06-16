using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class AP_Alt_AxisInputReceiver : AxisInputReceiver
{
    TextMeshProUGUI c1;
    TextMeshProUGUI c2;
    TextMeshProUGUI c3;
    TextMeshProUGUI c4;
    TextMeshProUGUI c5;
    int currentValue = 27000;

    public override void OnStart() {
        c1 = this.transform.Find("C1").GetComponent<TextMeshProUGUI>();
        c2 = this.transform.Find("C2").GetComponent<TextMeshProUGUI>();
        c3 = this.transform.Find("C3").GetComponent<TextMeshProUGUI>();
        c4 = this.transform.Find("C4").GetComponent<TextMeshProUGUI>();
        c5 = this.transform.Find("C5").GetComponent<TextMeshProUGUI>();
    }

    public override void OnValueChange(int valueChange) {
        var rawNewValue = currentValue + ((valueChange > 0 ? Mathf.FloorToInt(valueChange / 100f) : Mathf.RoundToInt(valueChange / 100f)) * 1000);
        var newValue = rawNewValue;

        if (rawNewValue < 0) {
            newValue = 0;
        } else if (rawNewValue > 40000) {
            newValue = 40000;
        }

        Debug.Log("OnValueChange  " + valueChange + "  current " + currentValue + "  new " + rawNewValue + " => " + newValue);

        currentValue = newValue;

        UpdateDisplayWithValue(currentValue);
    }

    void UpdateDisplayWithValue(int newValue) {
        if (c1 == null) {
            return;
        }

        var valueString = newValue.ToString().PadLeft(5, '0');

        c1.text = valueString[0].ToString();
        c2.text = valueString[1].ToString();
        c3.text = valueString[2].ToString();
        c4.text = valueString[3].ToString();
        c5.text = valueString[4].ToString();
    }
}
