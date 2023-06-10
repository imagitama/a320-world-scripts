
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Display : UdonSharpBehaviour
{
    public int defaultIndex;

    void Start()
    {
        UpdateDisplay(defaultIndex);
    }

    public void OnKnobIndex(int knobIndex) {
        Debug.Log("You have chosen " + knobIndex.ToString());

        UpdateDisplay(knobIndex);
    }

    void UpdateDisplay(int materialIndex) {
        var renderer = this.transform.GetComponent<Renderer>();

        renderer.material.mainTextureOffset = new Vector2(0.2f * materialIndex, 0);
    }
}
