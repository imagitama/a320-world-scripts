
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class Switch : UdonSharpBehaviour
{
    float visibleRotationY;
    [UdonSynced]
    float syncedVisibleRotationY;

    float pickupPositionX;
    [UdonSynced]
    float syncedPickupPositionX;    

    public float defaultRotation;

    public float[] targetAngles;

    // notify a display of a new index
    public Display displayToUpdate;

    public float fromAngle = -1;
    public float toAngle = -1;
    public KnobReceiver knobReceiver;

    Quaternion initialPickupRotation;
    Vector3 initialPickupPosition;

    Transform rotator;
    Quaternion rotatorInitialRotation;

    bool isPickingUp = false;
    float maxDegrees;
    int lastSelectedIndex;
    Transform handleTarget;
    float pickupRotationOffset;

    bool needsToSnap = false;

    void Start()
    {
        rotator = this.transform.parent;
        rotatorInitialRotation = rotator.rotation;

        initialPickupRotation = this.transform.rotation;
        initialPickupPosition = this.transform.position;

        pickupRotationOffset = this.transform.rotation.y; // 25.575

        handleTarget = this.transform.parent.Find("HandleTarget");

        SetMaxDegrees();
    }
    
    public override void OnDeserialization() {
        if (GetIsOwner()) {
            return;
        }
        visibleRotationY = syncedVisibleRotationY;
        pickupPositionX = syncedPickupPositionX;
    }

    void SetMaxDegrees() {
        if (targetAngles.Length < 2) {
            return;
        }

        float firstAngle = targetAngles[0];
        float lastAngle = targetAngles[targetAngles.Length - 1];

        maxDegrees = GetDifferenceOfDegrees(firstAngle, lastAngle);

        // Debug.Log("Max degrees: " + firstAngle.ToString() + "->" + lastAngle.ToString() + " = " + maxDegrees.ToString());
    }

    public override void OnPickup() {
        isPickingUp = true;
    }

    public override void OnDrop() {
        Debug.Log("Switch " + this.gameObject.name + " dropped");

        isPickingUp = false;

        if (GetNeedsSnapping()) {
            visibleRotationY = GetSnappedRotatorRotationY();
        }
    }

    void MovePickupToHandle() {
        this.transform.position = handleTarget.position;
    }

    float GetDifferenceOfDegrees(float degreesA, float degreesB) {
        return (degreesB - degreesA + 360) % 360;
    }

    void ClampPickupPosition() {
        this.transform.position = new Vector3(pickupPositionX, initialPickupPosition.y, initialPickupPosition.z);
    }

    void LateUpdate() {
        // NOTE: Always modify VRCPickup transforms in LateUpdate

        if (GetIsOwner()) {
            ClampPickupPosition();
        }
        
        // need to do this otherwise flickering as pickup only clamps here
        MoveRotatorVisibly();
    }

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    bool GetNeedsSnapping() {
        return targetAngles.Length >= 2;
    }

    void Update() {
        if (GetIsOwner()) {
            if (isPickingUp) {
                visibleRotationY = GetRotatorRotationY();
            }

            syncedVisibleRotationY = visibleRotationY;

            pickupPositionX = this.transform.position.x;
            syncedPickupPositionX = pickupPositionX;
        }
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        rotator.rotation = Quaternion.Euler(rotatorInitialRotation.eulerAngles.x, visibleRotationY, rotatorInitialRotation.eulerAngles.z);
    }

    float GetRotatorRotationY() {
        if (rotator == null) {
            return 0f;
        }

        Vector3 direction = this.transform.position - rotator.position;
        direction.y = 0f;

        if (direction == Vector3.zero) {
            return rotator.rotation.eulerAngles.y;
        }

        Quaternion rotation = Quaternion.LookRotation(direction);
        
        var newRotationY = rotation.eulerAngles.y;

        if (targetAngles.Length >= 2) {
            float firstTargetAngle = targetAngles[0];
            float lastTargetAngle = targetAngles[targetAngles.Length - 1];
            float rotationToClamp = 180 - rotation.eulerAngles.y;

            newRotationY = ClampDegrees(rotationToClamp, firstTargetAngle, lastTargetAngle);

            // Debug.Log("Clamp " + rotationToClamp + " between " + firstTargetAngle + " and " + lastTargetAngle + " to " + newRotationY);

            newRotationY = (newRotationY + 180) * -1;
        }

        return newRotationY;
    }

    float GetPositiveDegrees(float degrees) {
        return (degrees % 360 + 360) % 360;
    }

    float NormalizeDegreesTo0to360(float degrees) {
        return (degrees + 360) % 360;
    }

    float ClampDegrees(float degrees, float minAngle, float maxAngle) {
        degrees = NormalizeDegreesTo0to360(degrees);

        if (degrees > minAngle && degrees < maxAngle) {
            float nearestBoundary = Mathf.Abs(degrees - minAngle) < Mathf.Abs(degrees - maxAngle) ? minAngle : maxAngle;
            degrees = nearestBoundary;
        }

        return degrees;
    }

    float DegreesToPercentage(float degrees, float fromAngle, float toAngle) {
        float difference = (toAngle - fromAngle); // 220 - 140 = 80
        float totalDegrees = 360 - difference; // 360 - 80 = 280

        float newDegrees = degrees + fromAngle; // 0 + 140 = 140

        if (newDegrees > 360) {
            newDegrees = newDegrees - 360;
		}

        float percent = newDegrees / totalDegrees * 100; // 140 / 280 * 100 = 50%

        return percent;
    }

    float GetSnappedRotatorRotationY() {
        Debug.Log("Syncing switch " + this.gameObject.name + " to nearest angle...");

        float rotatorRotationY = rotator.rotation.eulerAngles.y;

        Vector3 direction = this.transform.position - rotator.position;
        direction.y = 0f;

        var targetRotation = Quaternion.LookRotation(direction);

        var desiredRotation = 180 - targetRotation.eulerAngles.y;
        var desiredRotation360 = (desiredRotation + 360f) % 360f;

        float nearestAngle = FindNearestAngle(desiredRotation360);
        int indexOfNearestAngle = FindNearestAngleIndex(desiredRotation360);

        if (indexOfNearestAngle != lastSelectedIndex) {
            NotifyDisplayOfNewIndex(indexOfNearestAngle);

            lastSelectedIndex = indexOfNearestAngle;
        }
        
        rotatorRotationY = (nearestAngle + pickupRotationOffset + 180) * -1;

        Debug.Log("Synced switch " + this.gameObject.name + " at " + desiredRotation360 + "d to " + rotatorRotationY + "d index " + indexOfNearestAngle.ToString() + " (" + nearestAngle.ToString() + "d)");
    
        return rotatorRotationY;
    }

    void NotifyKnobReceiverWithPercent(float percent) {
        if (knobReceiver == null) {
            return;
        }

        knobReceiver.OnKnobPercent(percent);
    }

    void NotifyDisplayOfNewIndex(int newIndex) {
        if (displayToUpdate == null) {
            return;
        }

        displayToUpdate.OnKnobIndex(newIndex);
    }

    float FindNearestAngle(float desiredAngle) {
        float nearestAngle = targetAngles[0];
        float minDifference = Mathf.Abs(desiredAngle - nearestAngle);

        foreach (float angle in targetAngles)
        {
            float difference = Mathf.Abs(desiredAngle - angle);
            if (difference < minDifference)
            {
                minDifference = difference;
                nearestAngle = angle;
            }
        }

        return nearestAngle;
    }

    int FindNearestAngleIndex(float desiredAngle) {
        int nearestAngleIndex = 0;
        float nearestAngle = targetAngles[nearestAngleIndex];
        float minDifference = Mathf.Abs(desiredAngle - nearestAngle);

        int index = 0;

        foreach (float angle in targetAngles)
        {
            float difference = Mathf.Abs(desiredAngle - angle);
            if (difference < minDifference)
            {
                minDifference = difference;
                nearestAngle = angle;
                nearestAngleIndex = index;
            }

            index++;
        }

        return nearestAngleIndex;
    }
}
