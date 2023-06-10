
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class Knob : UdonSharpBehaviour
{
    float visibleRotationOnAxis;
    [UdonSynced]
    float syncedVisibleRotationOnAxis;

    float pickupRotationOnAxis;
    [UdonSynced]
    float syncedPickupRotationOnAxis;    

    int selectedIndex = -1;
    [UdonSynced]
    int syncedSelectedIndex;

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
    Transform handleTarget;
    public float rotationOffset = 0f;
    public float rotationOffsetForReal;
    bool needsToSnap = false;
    public bool isHorizontal = false;

    // hand interaction
    SphereCollider sphereCollider;
    bool hasHandEnteredCollider = false;
    float timeBeforeNextCollisionCheck = -1;
    Renderer renderer;
    Material standardMaterial;
    public Material highlightMaterial;
    Quaternion lastKnownPickupRotation;
    // debug
    Transform fakeHand;

    void Start()
    {
        rotator = this.transform.parent;
        rotatorInitialRotation = rotator.rotation;

        initialPickupRotation = this.transform.rotation;
        lastKnownPickupRotation = initialPickupRotation;
        initialPickupPosition = this.transform.position;

        SetMaxDegrees();

        if (GetIsOwner() && GetNeedsSnapping()) {
            visibleRotationOnAxis = GetSnappedRotatorRotationOnAxis();
        }

        #if UNITY_EDITOR
        fakeHand = GameObject.Find("/FakeHand").transform;
        #endif

        renderer = rotator.GetComponent<Renderer>();
        sphereCollider = rotator.GetComponent<SphereCollider>();
        standardMaterial = renderer.material;
    }

    void Update() {
        if (GetIsOwner()) {
            MovePickupToHand();
            
            if (isPickingUp) {
                visibleRotationOnAxis = GetRotatorRotationOnAxis();
            }

            if (visibleRotationOnAxis != syncedVisibleRotationOnAxis) {
                syncedVisibleRotationOnAxis = visibleRotationOnAxis;
            }

            pickupRotationOnAxis = isHorizontal ? this.transform.rotation.eulerAngles.y : this.transform.rotation.eulerAngles.z;

            if (pickupRotationOnAxis != syncedPickupRotationOnAxis) {
                syncedPickupRotationOnAxis = pickupRotationOnAxis;
            }
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
        pickupRotationOnAxis = syncedPickupRotationOnAxis;

        if (syncedSelectedIndex != selectedIndex) {
            NotifyDisplayOfNewIndex(syncedSelectedIndex);
        }

        selectedIndex = syncedSelectedIndex;
    }

   public override void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) {
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

    Vector3 GetHandPosition() {
        #if UNITY_EDITOR
        return fakeHand.position;
        #else
        return Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);
        #endif
    }

    Quaternion GetHandRotation() {
        #if UNITY_EDITOR
        return fakeHand.rotation;
        #else
        return Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.RightHand);
        #endif
    }

    void MovePickupToHand() {
        if (isPickingUp) {
            var handRotation = GetHandRotation();

            if (isHorizontal) {
                lastKnownPickupRotation = Quaternion.Euler(
                    initialPickupRotation.eulerAngles.x,
                    handRotation.eulerAngles.y + 180,
                    initialPickupRotation.eulerAngles.z
                );
            } else {
                lastKnownPickupRotation = Quaternion.Euler(
                    initialPickupRotation.eulerAngles.x,
                    initialPickupRotation.eulerAngles.y,
                    handRotation.eulerAngles.z + 180
                );
            }

            // Debug.Log("Move To Hand " + lastKnownPickupRotation);
        } else {
            // Debug.Log("DO NOT Move To Hand " + lastKnownPickupRotation);
        }
        
        // need to do this each frame otherwise it moves with the rotator causing an infinite rotation effect
        this.transform.rotation = lastKnownPickupRotation;
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
        Debug.Log("Knob \"" + this.gameObject.name + "\" hand enter");

        if (isPickingUp) {
            return;
        }

        renderer.material = highlightMaterial;
    }

    void SwitchToStandardMaterial() {
        renderer.material = standardMaterial;
    }

    void OnHandLeave() {
        Debug.Log("Knob \"" + this.gameObject.name + "\" hand leave");

        SwitchToStandardMaterial();
    }

    void OnPickup() {
        Debug.Log("Knob \"" + this.gameObject.name + "\" pickup");

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        isPickingUp = true;

        SwitchToStandardMaterial();
    }

    void OnDrop() {
        Debug.Log("Knob \"" + this.gameObject.name + "\" drop");

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

    float GetDifferenceOfDegrees(float degreesA, float degreesB) {
        return (degreesB - degreesA + 360) % 360;
    }

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    bool GetNeedsSnapping() {
        return targetAngles.Length >= 2;
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        var angle = visibleRotationOnAxis;

        rotator.rotation = Quaternion.Euler(rotatorInitialRotation.eulerAngles.x, rotatorInitialRotation.eulerAngles.y, angle);

        /*
        
        rotator.rotation = (
            isHorizontal 
                ? Quaternion.Euler(rotatorInitialRotation.eulerAngles.x, rotatorInitialRotation.eulerAngles.y, angle)
                : Quaternion.Euler(rotatorInitialRotation.eulerAngles.x, rotatorInitialRotation.eulerAngles.y, angle)
        ); */
    }

    float GetRotatorRotationOnAxis() {
        if (rotator == null) {
            return 0f;
        }

        return (isHorizontal ? this.transform.rotation.eulerAngles.y : (this.transform.rotation.eulerAngles.z * -1));
    }

    float GetPositiveDegrees(float degrees) {
        return (degrees % 360 + 360) % 360;
    }

    float NormalizeDegreesTo0to360(float degrees) {
        return (degrees + 360) % 360;
    }

    float ClampDegrees(float angle, float min, float max) {
        // 354d, 330d -> 30d

        if (angle > max && min == 0) {
            min = 360;
        }

        var center = Mathf.DeltaAngle(min, max);

        // 354d < 330d (FALSE) + 10d > 30d ()
        if (angle < min && angle > max) {
            if (angle > center) {
                return min;
            } else {
                return max;
            }
        // 354d > 30d (FALSE) + 354d < 330d (FALSE)
        } else if (angle > max && angle < min) {
            if (angle > center) {
                return max;
            } else {
                return min;
            }
        // 354d > 330d (TRUE) + 
        } else if (min > max) {
            return angle;
        // 354d < 330d (FALSE)
        } else if (angle < min) {
            return min;
        // 354d > 30d (TRUE)
        } else if (angle > max) {
            return max;
        }

        return angle;
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

    float GetCurrentRotationAsDegreesOf360() {
        var currentRotationZ = rotator.rotation.eulerAngles.z;

        currentRotationZ = currentRotationZ + 180f;

        var currentRotationZOf360 = NormalizeDegreesTo0to360(currentRotationZ);

        return currentRotationZOf360;
    }

    float GetSnappedRotatorRotationOnAxis(float overrideCurrentRotationOf360 = -1f) {
        float desiredRotation360 = overrideCurrentRotationOf360 != -1 ? overrideCurrentRotationOf360 : GetCurrentRotationAsDegreesOf360();

        float nearestAngle = FindNearestAngle(desiredRotation360);
        int indexOfNearestAngle = FindNearestAngleIndex(desiredRotation360);

        if (indexOfNearestAngle != selectedIndex) {
            selectedIndex = indexOfNearestAngle;
            syncedSelectedIndex = selectedIndex;
            NotifyDisplayOfNewIndex(selectedIndex);
        }

        var nearestAnglePositiveDegrees = ConvertToPositiveDegrees(nearestAngle);

        var rotatorRotation = nearestAnglePositiveDegrees + 180f;

        Debug.Log("Snapped knob " + this.gameObject.name + " from " + desiredRotation360 + "d -> " + rotatorRotation + "d -> " + nearestAnglePositiveDegrees + "d -> " + rotatorRotation + "d [index " + indexOfNearestAngle.ToString() + ", " + nearestAngle.ToString() + "d]");

        return rotatorRotation;
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

    float AngleDifference(float a, float b) {
        float difference = Mathf.Abs(a - b) % 360;
        if (difference > 180)
        {
            difference = 360 - difference;
        }
        return difference;
    }

    float FindNearestAngle(float desiredAngle) {
        float nearestAngle = targetAngles[0];
        float minDifference = Mathf.Abs(AngleDifference(desiredAngle, nearestAngle));

        foreach (float angle in targetAngles)
        {
            float difference = Mathf.Abs(AngleDifference(desiredAngle, angle));
            if (difference < minDifference)
            {
                Debug.Log(angle + " is close to " + desiredAngle + "!");
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
