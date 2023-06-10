
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class Switch : UdonSharpBehaviour
{
    float visibleRotationOnAxis;
    [UdonSynced]
    float syncedVisibleRotationOnAxis;

    float pickupPositionOnAxis;
    [UdonSynced]
    float syncedPickupPositionOnAxis;    

    public float defaultRotation;
    public float[] targetAngles;

    // notify a display of a new index
    public Display displayToUpdate;

    public float fromAngle = -1;
    public float toAngle = -1;
    public KnobReceiver knobReceiver;

    Vector3 initialPickupPosition;

    Transform rotator;
    Quaternion rotatorInitialRotation;

    bool isPickingUp = false;
    float maxDegrees;
    int lastSelectedIndex;
    Transform handleTarget;
    float pickupRotationOffset;
    bool needsToSnap = false;

    // hand interaction
    SphereCollider sphereCollider;
    bool hasHandEnteredCollider = false;
    float timeBeforeNextCollisionCheck = -1;
    Renderer renderer;
    Material standardMaterial;
    public Material highlightMaterial;
    Vector3 lastKnownPickupPosition;
    // debug
    Transform fakeHand;
    bool currentValueOfOnDrop = false;

    public void Start() {
        rotator = this.transform.parent;
        rotatorInitialRotation = rotator.rotation;

        initialPickupPosition = this.transform.position;
        lastKnownPickupPosition = initialPickupPosition;

        pickupRotationOffset = this.transform.rotation.y; // 25.575

        handleTarget = this.transform.parent.Find("HandleTarget");

        SetMaxDegrees();

        #if UNITY_EDITOR
        fakeHand = GameObject.Find("/FakeHand").transform;
        #endif

        renderer = rotator.GetComponent<Renderer>();
        sphereCollider = rotator.GetComponent<SphereCollider>();
        standardMaterial = renderer.material;
    }

    public void Update() {
        if (GetIsOwner()) {
            MovePickupToHand();

            if (isPickingUp) {
                visibleRotationOnAxis = GetRotatorRotationOnAxis();
            }

            syncedVisibleRotationOnAxis = visibleRotationOnAxis;

            pickupPositionOnAxis = this.transform.position.x;
            syncedPickupPositionOnAxis = pickupPositionOnAxis;
        } else {
            SyncPickupPosition();
        }
        
        DetectHandHover();
        
        MoveRotatorVisibly();
    }

    //////////////
    
    public override void OnDeserialization() {
        if (GetIsOwner()) {
            return;
        }

        visibleRotationOnAxis = syncedVisibleRotationOnAxis;
        pickupPositionOnAxis = syncedPickupPositionOnAxis;
    }

    public override void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) {
        #if UNITY_EDITOR
        currentValueOfOnDrop = !currentValueOfOnDrop;
        value = currentValueOfOnDrop;
        #endif

        if (value == true) {
            Debug.Log("Player grab");

            if (hasHandEnteredCollider) {
                OnPickup();
            }
        } else {
            Debug.Log("Player drop");

            if (isPickingUp) {
                OnDrop();
            }
        }
    }

    //////////////

    void SyncPickupPosition() {
        this.transform.position = new Vector3(
            pickupPositionOnAxis,
            initialPickupPosition.y,
            initialPickupPosition.z
        );
    }

    void MovePickupToHand() {
        if (isPickingUp) {
            var handPosition = GetHandPosition();

            lastKnownPickupPosition = new Vector3(
                handPosition.x,
                initialPickupPosition.y,
                initialPickupPosition.z
            );
            
            Debug.Log("Moving pickup to " + lastKnownPickupPosition);
        }
        
        // need to do this each frame otherwise it moves with the rotator causing an infinite rotation effect
        this.transform.position = lastKnownPickupPosition;
    }

    Vector3 GetHandPosition() {
        #if UNITY_EDITOR
        return fakeHand.position;
        #else
        return Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);
        #endif
    }

    bool GetIsHandInsideCollider() {
        Bounds colliderBounds = sphereCollider.bounds;

        var handPosition = GetHandPosition();

        bool isInside = colliderBounds.Contains(handPosition);

        return isInside;
    }

    void DetectHandHover() {
        if (sphereCollider == null) {
            return;
        }

        // prevent edge case where boxcollider physically moves away from finger on push in so triggers another collision
        if (timeBeforeNextCollisionCheck != -1) {
            if (Time.time > timeBeforeNextCollisionCheck) {
                timeBeforeNextCollisionCheck = -1;
            }
            return;
        }

        var isHandInsideCollider = GetIsHandInsideCollider();

        if (!hasHandEnteredCollider) {
            if (isHandInsideCollider) {
                hasHandEnteredCollider = true;

                OnHandEnter();

                timeBeforeNextCollisionCheck = Time.time + 0.5f; // Time.time in seconds
            }
        } else {
            if (!isHandInsideCollider) {
                hasHandEnteredCollider = false;

                OnHandLeave();
            }
        }
    }

    void OnHandEnter() {
        Debug.Log("Switch \"" + this.gameObject.name + "\" hand enter");

        if (isPickingUp) {
            return;
        }

        renderer.material = highlightMaterial;
    }

    void SwitchToStandardMaterial() {
        renderer.material = standardMaterial;
    }

    void OnHandLeave() {
        Debug.Log("Switch \"" + this.gameObject.name + "\" hand leave");

        SwitchToStandardMaterial();
    }

    void OnPickup() {
        Debug.Log("Switch \"" + this.gameObject.name + "\" pickup");

        isPickingUp = true;

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        SwitchToStandardMaterial();
    }

    void OnDrop() {
        Debug.Log("Switch \"" + this.gameObject.name + "\" drop");

        isPickingUp = false;

        if (GetNeedsSnapping()) {
            Debug.Log("Snapping to nearest target angle...");
            visibleRotationOnAxis = GetSnappedRotatorRotationOnAxis();
        } else {
            Debug.Log("Does not need snapping");
        }
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

    void MovePickupToHandle() {
        this.transform.position = handleTarget.position;
    }

    float GetDifferenceOfDegrees(float degreesA, float degreesB) {
        return (degreesB - degreesA + 360) % 360;
    }

    void ClampPickupPosition() {
        this.transform.position = new Vector3(pickupPositionOnAxis, initialPickupPosition.y, initialPickupPosition.z);
    }

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    bool GetNeedsSnapping() {
        return targetAngles.Length >= 2;
    }

    void MoveRotatorVisibly() {
        if (rotator == null || visibleRotationOnAxis == null) {
            return;
        }

        rotator.rotation = Quaternion.Euler(rotatorInitialRotation.eulerAngles.x, visibleRotationOnAxis, rotatorInitialRotation.eulerAngles.z);
    }

    float GetCurrentRotationAsDegreesOf360() {
        Vector3 direction = this.transform.position - rotator.position;

        // NOTE: always between 0-180 degrees
        float angle = Vector3.Angle(Vector3.back, direction);

        // NOTE: always seems to be a tiiiiny bit different
        if (this.transform.position.x < rotator.position.x) {
            angle = -angle;
        }

        if (angle < 0f) {
            angle = angle + 360f;
        } else if (angle > 360f) {
            angle = angle - 360f;
        }

        var offset = 270f;
        var angleWithin360 = (angle - offset + 360) % 360;

        return angleWithin360;
    }

    float ConvertToPositiveDegrees(float degrees) {
        float positiveDegrees = degrees % 360f;
        if (positiveDegrees < 0f)
        {
            positiveDegrees += 360f;
        }
        return positiveDegrees;
    }

    float ConvertDegreesOutOf360ToLeverRotationValue(float degreesOf360) {
        if (degreesOf360 > 180) {
            return degreesOf360 - 360;
        } else {
            return degreesOf360;
        }
    }

    float GetRotatorRotationOnAxis() {
        if (rotator == null) {
            return 0f;
        }



                // DEBUG ONLY
        Vector3 direction = this.transform.position - rotator.position;

        direction.y = 0f;

        if (direction == Vector3.zero) {
            return rotator.rotation.eulerAngles.y;
        }

        Quaternion rotation = Quaternion.LookRotation(direction);

        var currentRotationValue = rotation.eulerAngles.y;
        // END



        var currentRotationValue360 = GetCurrentRotationAsDegreesOf360();
        var rotationOffset = -90f;

        if (targetAngles.Length >= 2) {
            float firstTargetAngle = targetAngles[0];
            float lastTargetAngle = targetAngles[targetAngles.Length - 1];

            var clampedDegrees360 = ClampDegrees(currentRotationValue360, firstTargetAngle, lastTargetAngle);

            var nearestAngleInverted = clampedDegrees360 * -1;
            var nearestAngleWithOffset = ConvertToPositiveDegrees(nearestAngleInverted + rotationOffset);

            var newRotationOnAxis = ConvertDegreesOutOf360ToLeverRotationValue(nearestAngleWithOffset);
            
            Debug.Log("Clamped " + currentRotationValue + "d -> " + currentRotationValue360 + "d -> (between " + firstTargetAngle + "d and " + lastTargetAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        } else {
            var clampedDegrees360 = ClampDegrees(currentRotationValue360, fromAngle, toAngle);

            var degreesWithOffset = ConvertToPositiveDegrees(clampedDegrees360 + rotationOffset);

            var newRotationOnAxis = ConvertDegreesOutOf360ToLeverRotationValue(degreesWithOffset);

            // Debug.Log("Clamped " + currentRotationValue + "d -> " + currentRotationValue360 + "d -> (between " + fromAngle + "d and " + toAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        }
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

    float GetSnappedRotatorRotationOnAxis() {
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
