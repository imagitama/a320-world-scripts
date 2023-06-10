
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif

public class Lever : UdonSharpBehaviour
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
    // int lastPercent;

    Vector3 initialPickupPosition;

    Transform rotator;

    bool isPickingUp = false;
    float maxDegrees;
    int lastSelectedIndex = -1;
    Transform handleTarget;
    public float rotationOffset = 0f;
    public float rotationOffsetForReal;
    // gear lever
    public bool isVertical = false;

    bool needsToSnap = false;

// #if UNITY_EDITOR && !COMPILER_UDONSHARP
//     string GetGizmoLabel() {

//         if (isPickingUp) {
//             if (GetNeedsSnapping()) {
//                 return FindNearestAngleIndexForRotator() + " - " + FindNearestAngleForRotator();
//             } else {
//                 return "Picking";
//             }
//         }

//         if (!GetNeedsSnapping()) {
//             return "SnapOff";
//         }
        
//         if (lastSelectedIndex == -1) {
//             return "NoSelected";
//         }

//         return lastSelectedIndex + " - " + targetAngles[lastSelectedIndex];
//     }

//     [DrawGizmo (GizmoType.Selected | GizmoType.NonSelected)]
//     void OnDrawGizmos() {
//         // Handles.Label(transform.position, GetGizmoLabel());
//     }
// #endif

    void Start()
    {
        rotator = this.transform.parent;

        initialPickupPosition = this.transform.position;

        handleTarget = rotator.Find("LeverTarget");

        SetMaxDegrees();

        if (GetIsOwner() && GetNeedsSnapping()) {
            visibleRotationOnAxis = GetSnappedRotatorRotationOnAxis();
        }

        if (!GetIsOwner()) {
            this.transform.Find("Cube").gameObject.SetActive(false);
        }
    }

    public override void OnDeserialization() {
        if (GetIsOwner()) {
            return;
        }
        visibleRotationOnAxis = syncedVisibleRotationOnAxis;
        pickupPositionOnAxis = syncedPickupPositionOnAxis;
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
        Debug.Log("Lever " + this.gameObject.name + " dropped");

        isPickingUp = false;

        if (GetNeedsSnapping()) {
            Debug.Log("Snapping to nearest target angle...");
            visibleRotationOnAxis = GetSnappedRotatorRotationOnAxis();
        } else {
            Debug.Log("Does not need snapping");
        }

        // MovePickupToHandle();
    }

    void MovePickupToHandle() {
        this.transform.position = handleTarget.position;
    }

    float GetDifferenceOfDegrees(float degreesA, float degreesB) {
        return (degreesB - degreesA + 360) % 360;
    }

    void ClampPickupPosition() {
        if (isVertical) {
            this.transform.position = new Vector3(
                initialPickupPosition.x,
                pickupPositionOnAxis,
                initialPickupPosition.z
            );
        } else {
            this.transform.position = new Vector3(
                initialPickupPosition.x,
                initialPickupPosition.y,
                pickupPositionOnAxis
            );
        }
    }

    void SyncPickupPosition() {
        if (isVertical) {
            this.transform.position = new Vector3(
                initialPickupPosition.x,
                pickupPositionOnAxis,
                initialPickupPosition.z
            );
        } else {
            this.transform.position = new Vector3(
                initialPickupPosition.x,
                initialPickupPosition.y,
                pickupPositionOnAxis
            );
        }
    }

    void LateUpdate() {
        // NOTE: Always modify VRCPickup transforms in LateUpdate

        if (GetIsOwner()) {
            ClampPickupPosition();
        } else {
            SyncPickupPosition();
        }
        
        // need to do this otherwise flickering as pickup only clamps here
        MoveRotatorVisibly();
    }

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    bool GetNeedsSnapping() {
        // return false;
        return targetAngles.Length >= 2;
    }

    void Update() {
        if (GetIsOwner()) {
            if (isPickingUp) {
                visibleRotationOnAxis = GetRotatorRotationOnAxis();
            }

            // prevent self DDOS
            if (visibleRotationOnAxis != syncedVisibleRotationOnAxis) {
                syncedVisibleRotationOnAxis = visibleRotationOnAxis;
            }

            pickupPositionOnAxis = isVertical ? this.transform.position.y : this.transform.position.z;

            // prevent self DDOS
            if (pickupPositionOnAxis != syncedPickupPositionOnAxis) {
                syncedPickupPositionOnAxis = pickupPositionOnAxis;
            }
        }
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        Vector3 direction = this.transform.position - rotator.position;

        float angle = Mathf.Atan2(direction.y, direction.z) * Mathf.Rad2Deg;

        angle = angle - rotationOffsetForReal;

        angle = angle * -1;

        Quaternion rotation = Quaternion.Euler(angle, 0f, 0f);

        rotator.rotation = rotation;
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

        if (targetAngles.Length >= 2) {
            float firstTargetAngle = targetAngles[0];
            float lastTargetAngle = targetAngles[targetAngles.Length - 1];

            var clampedDegrees360 = ClampDegrees(currentRotationValue360, firstTargetAngle, lastTargetAngle);

            var nearestAngleInverted = clampedDegrees360 * -1;
            var nearestAngleWithOffset = ConvertToPositiveDegrees(nearestAngleInverted + rotationOffset); // 90

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
        // WARNING: The OnDrop clamping mechanism INVERTS the angle it is snapped to
        // ie. when lever is dropped lower it snaps to the HIGHER angle but visually is lower
        if (isVertical) {
            Vector3 direction1 = rotator.position - this.transform.position;

            Quaternion targetRotation = Quaternion.LookRotation(direction1, Vector3.back);
            var angle1 = targetRotation.eulerAngles.z;
            var offset1= 0;

            // NOTE: always seems to be a tiiiiny bit different
            if (this.transform.position.x < rotator.position.x) {
                angle1 = -angle1;
            }

            var angleWithin3601 = ConvertToPositiveDegrees(angle1 - offset1);

            return angleWithin3601;
        }

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

    float GetSnappedRotatorRotationOnAxis(float overrideCurrentRotationOf360 = -1f) {
        float desiredRotation360 = overrideCurrentRotationOf360 != -1 ? overrideCurrentRotationOf360 : GetCurrentRotationAsDegreesOf360();

        float nearestAngle = FindNearestAngle(desiredRotation360);
        int indexOfNearestAngle = FindNearestAngleIndex(desiredRotation360);

        if (indexOfNearestAngle != lastSelectedIndex) {
            NotifyDisplayOfNewIndex(indexOfNearestAngle);

            lastSelectedIndex = indexOfNearestAngle;
        }

        var nearestAngleInverted = nearestAngle * -1;
        var nearestAngleInvertedWithOffset = isVertical ? nearestAngleInverted + 40 : nearestAngleInverted - 90;
        var nearestAngleWithOffset = ConvertToPositiveDegrees(nearestAngleInvertedWithOffset);
        
        var rotatorRotation = ConvertDegreesOutOf360ToLeverRotationValue(nearestAngleWithOffset);

        Debug.Log("Snapped lever " + this.gameObject.name + " from " + desiredRotation360 + "d -> " + rotatorRotation + "d -> " + nearestAngleWithOffset + "d [index " + indexOfNearestAngle.ToString() + ", " + nearestAngle.ToString() + "d]");
    
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

    float FindNearestAngle(float desiredAngle) {
        float nearestAngle = targetAngles[0];
        float minDifference = Mathf.Abs(desiredAngle - nearestAngle);

        foreach (float angle in targetAngles)
        {
            float difference = Mathf.Abs(desiredAngle - angle);
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
