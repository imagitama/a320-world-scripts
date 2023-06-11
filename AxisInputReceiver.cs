
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class AxisInputReceiver : UdonSharpBehaviour
{
    void Start()
    {
        OnStart();
    }

    public virtual void OnStart() {

    }

    public virtual void OnPercent(float percent) {

    }

    public virtual void OnIndex(int index) {

    }
}
