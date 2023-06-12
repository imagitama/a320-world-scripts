
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CaptainReadingLight : AxisInputReceiver
{
    Light light;

    public override void OnStart() {
        light = this.gameObject.GetComponent<Light>();
    }

    public override void OnPercent(float percentOutOf100) {
        UpdateLightWithPercent(percentOutOf100);
    }

    void UpdateLightWithPercent(float percentOutOf100) {
        if (light == null) {
            return;
        }
        
        light.gameObject.SetActive(percentOutOf100 > 1);

        var newIntensity = (percentOutOf100 / 100) * 2;

        light.intensity = newIntensity;

        Debug.Log("UpdateLightWithPercent %" + percentOutOf100 + " => " + newIntensity);
    }
}
