
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class EngineKillerEffectTrigger : UdonSharpBehaviour
{
    Transform particle;
    AudioSource audioSource;

    void Start() {
        particle = this.transform.parent.Find("Particle");
        audioSource = this.transform.parent.Find("Audio").GetComponent<AudioSource>();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
        Debug.Log("Player " + player.displayName + " wants to die!");

        PlayParticleEffectForTime();
        PlayAudio();
    }

    void PlayAudio() {
        audioSource.Play();
    }

    void PlayParticleEffectForTime() {
        particle.gameObject.SetActive(true);
  
        SendCustomEventDelayedSeconds("StopParticleEffect", 5); // 5 seconds
    }

    public void StopParticleEffect() {
        particle.gameObject.SetActive(false);
    }
}
