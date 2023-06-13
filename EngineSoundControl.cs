
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class EngineSoundControl : AxisInputReceiver
{
    AudioSource audioSource;
    float lastPercent;

    public override void OnStart() {
        audioSource = this.gameObject.GetComponent<AudioSource>();

        if (lastPercent != null) {
            OnPercent(lastPercent);
        }
    }

    public override void OnPercent(float percentOutOf100) {
        lastPercent = percentOutOf100;
        UpdateAudioSourceWithPercent(percentOutOf100);
    }

    void UpdateAudioSourceWithPercent(float percentOutOf100) {
        if (audioSource == null) {
            return;
        }

        var newVolume = (percentOutOf100 / 100);

        audioSource.volume = newVolume;

        Debug.Log("EngineSoundControl %" + percentOutOf100 + " => " + newVolume);
    }
}
