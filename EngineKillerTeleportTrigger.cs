
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class EngineKillerTeleportTrigger : UdonSharpBehaviour
{
    Vector3 teleportPosition;

    void Start() {
        var teleportTarget = this.transform.parent.Find("TeleportTarget");
        teleportPosition = teleportTarget.position;
    }

    void KillPlayer(VRCPlayerApi player) {
        if (player != Networking.LocalPlayer) {
            return;
        }

        player.TeleportTo(teleportPosition, new Quaternion(0, 0, -1, 1));
        player.SetVelocity(new Vector3(0, 0, -10f));
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
        Debug.Log("Player " + player.displayName + " wants to die!");

        KillPlayer(player);
    }
}
