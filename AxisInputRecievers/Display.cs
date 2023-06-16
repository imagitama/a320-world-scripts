
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Display : AxisInputReceiver
{
    public int defaultIndex;

    public override void OnIndex(int index) {
        UpdateDisplay(index);
    }

    void UpdateDisplay(int materialIndex) {
        var renderer = this.transform.GetComponent<Renderer>();
        renderer.material.mainTextureOffset = new Vector2(0.2f * materialIndex, 0);
    }
}
