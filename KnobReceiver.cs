
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class KnobReceiver : UdonSharpBehaviour
{
    Light lightComponent;

    void Start()
    {
        lightComponent = this.transform.GetComponent<Light>();
    }

    public void OnKnobPercent(float percent) {
        Debug.Log("KNOB!!!! " + percent.ToString());

        lightComponent.intensity = percent / 100;
        
        lightComponent.enabled = percent > 5;
    }
}
