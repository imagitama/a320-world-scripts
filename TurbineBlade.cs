
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TurbineBlade : UdonSharpBehaviour
{
    public bool isReverse = false;

    void Start() {
        if (isReverse) {
            var animator = this.transform.gameObject.GetComponent<Animator>();
            animator.SetBool("IsReverse", true);
        }
    }
}
