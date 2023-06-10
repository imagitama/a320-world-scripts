
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ToggleGameObjectsButton : UdonSharpBehaviour
{
    public bool currentValue;
    public Transform[] gameObjects;

    void Start() {
        UpdateVisibility();
    }

    public override void Interact() {
        currentValue = !currentValue;
        UpdateVisibility();
    }

    void UpdateVisibility() {
        foreach (Transform gameObject in gameObjects) {
            gameObject.gameObject.SetActive(currentValue);
        }
    }
}
