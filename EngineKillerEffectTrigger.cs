
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class EngineKillerEffectTrigger : UdonSharpBehaviour
{
    float timeToHide = 0f;
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

    void Update() {
        if (timeToHide != 0 && Time.time >= timeToHide) {
            StopParticleEffect();
            timeToHide = 0;
        }
    }

    void PlayParticleEffectForTime() {
        particle.gameObject.SetActive(true);
        timeToHide = Time.time + 5f;
    }

    void StopParticleEffect() {
        particle.gameObject.SetActive(false);
    }
}
