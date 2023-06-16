
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CaptainReadingLight : AxisInputReceiver
{
    Light light;
    float lastPercent;

    public override void OnStart() {
        light = this.gameObject.GetComponent<Light>();

        if (lastPercent != null) {
            OnPercent(lastPercent);
        }
    }

    public override void OnPercent(float percentOutOf100) {
        lastPercent = percentOutOf100;
        UpdateLightWithPercent(percentOutOf100);
    }

    void UpdateLightWithPercent(float percentOutOf100) {
        if (light == null) {
            return;
        }
        
        light.gameObject.SetActive(percentOutOf100 > 1);

        var newIntensity = (percentOutOf100 / 100) * 2;

        light.intensity = newIntensity;

        Debug.Log("CaptainReadingLight %" + percentOutOf100 + " => " + newIntensity);
    }
}
