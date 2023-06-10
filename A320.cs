
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif

public class A320 : UdonSharpBehaviour
{
    void Start()
    {
        
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
[DrawGizmo (GizmoType.Selected | GizmoType.NonSelected)]
    void OnDrawGizmos() {
        var myHand = GameObject.Find("/FakeHand").transform;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetHandPosition(myHand), 0.0025f);
    }
#endif
}
