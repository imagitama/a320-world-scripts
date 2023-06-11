
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public enum Axis {
    X,
    Y,
    Z
}

public enum AxisInputMethods {
    Twist,
    Slide
}

public class AxisInput : UdonSharpBehaviour
{
    public Axis rotatorAxis;
    public AxisInputMethods inputMethod;
    public Axis pickupAxis;
    // use "pinch" to grab a knob then twist using hand rotation
    public bool useFingerCollision = true;

    float rotatorMovementOnAxis;
    [UdonSynced]
    float syncedRotatorMovementOnAxis;

    float pickupMovementOnAxis;
    [UdonSynced]
    float syncedPickupMovementOnAxis;

    int selectedIndex = -1;
    [UdonSynced]
    int syncedSelectedIndex;

    public float defaultRotation;
    public float[] targetAngles;

    public AxisInputReceiver[] receivers;

    public float fromAngle = -1f;
    public float toAngle = -1f;
    public float offset = 0f;

    Vector3 initialPickupPosition;
    Quaternion initialPickupRotation;
    Quaternion initialRotatorRotation;
    Transform rotator;
    bool isPickingUp = false;
    float maxDegrees;
    bool needsToSnap = false;

    BoxCollider boxCollider;
    SphereCollider sphereCollider;
    bool hasHandEnteredCollider = false;
    float timeBeforeNextCollisionCheck = -1;
    Renderer meshRenderer;
    Material standardMaterial;
    public Material highlightMaterial;

    Vector3 lastKnownPickupPosition;
    Quaternion lastKnownPickupRotation;

    // debug
    Transform fakeHand;
    Transform fakeFinger;
    bool currentValueOfOnDrop = false;

    public void Start() {
        rotator = this.transform.parent;
        initialRotatorRotation = rotator.rotation;

        initialPickupPosition = this.transform.position;
        lastKnownPickupPosition = initialPickupPosition;

        initialPickupRotation = this.transform.rotation;
        lastKnownPickupRotation = initialPickupRotation;

        SetMaxDegrees();

        if (GetIsOwner()) {
            InitializePickupTransform();

            if (GetNeedsSnapping()) {
                rotatorMovementOnAxis = GetSnappedRotatorRotationOnAxis();
            } else {
                rotatorMovementOnAxis = GetRotatorRotationOnAxis();

                HandlePercentages();
            }
        } else {
            var cube = this.transform.Find("Cube");
            
            if (cube != null) {
                cube.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        fakeHand = GameObject.Find("/FakeHand").transform;
        fakeFinger = fakeHand.Find("Index/IndexDistal");
#endif

        meshRenderer = rotator.GetComponent<Renderer>();
        boxCollider = rotator.GetComponent<BoxCollider>();
        sphereCollider = rotator.GetComponent<SphereCollider>();
        standardMaterial = meshRenderer.material;
    }

    public void Update() {
        if (GetIsOwner()) {
            MovePickupToHand();

            if (isPickingUp) {
                rotatorMovementOnAxis = GetRotatorRotationOnAxis();
            }

            syncedRotatorMovementOnAxis = rotatorMovementOnAxis;

            pickupMovementOnAxis = (inputMethod == AxisInputMethods.Slide ? this.transform.position[(int)pickupAxis] : this.transform.rotation[(int)pickupAxis]);
            syncedPickupMovementOnAxis = pickupMovementOnAxis;
        } else {
            SyncPickupTransform();
        }
        
        DetectHandHover();
        
        MoveRotatorVisibly();

        if (!GetNeedsSnapping()) {
            HandlePercentages();
        }
    }

    //////////////

    public override void OnDeserialization() {
        if (GetIsOwner()) {
            return;
        }

        rotatorMovementOnAxis = syncedRotatorMovementOnAxis;
        pickupMovementOnAxis = syncedPickupMovementOnAxis;
    }

    public override void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) {
#if UNITY_EDITOR
        currentValueOfOnDrop = !currentValueOfOnDrop;
        value = currentValueOfOnDrop;
#endif

        if (value == true) {
            if (hasHandEnteredCollider) {
                OnCustomPickup();
            }
        } else {
            if (isPickingUp) {
                OnCustomDrop();
            }
        }
    }

    //////////////

    void InitializePickupTransform() {
        if (inputMethod == AxisInputMethods.Slide) {
            var positionToUse = 0;

            lastKnownPickupPosition = new Vector3(
                pickupAxis == Axis.X ? positionToUse : initialPickupPosition.x,
                pickupAxis == Axis.Y ? positionToUse : initialPickupPosition.y,
                pickupAxis == Axis.Z ? positionToUse : initialPickupPosition.z
            );

            this.transform.position = lastKnownPickupPosition;
        } else {
            var rotationToUse = ConvertDegreesOutOf360ToRotationValue(defaultRotation + 90f); //  - 90f

            lastKnownPickupRotation = Quaternion.Euler(
                pickupAxis == Axis.X ? rotationToUse : initialPickupRotation.eulerAngles.x,
                pickupAxis == Axis.Y ? rotationToUse : initialPickupRotation.eulerAngles.y,
                pickupAxis == Axis.Z ? rotationToUse : initialPickupRotation.eulerAngles.z
            );

            this.transform.rotation = lastKnownPickupRotation;
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

    void SyncPickupTransform() {
        if (inputMethod == AxisInputMethods.Slide) {
            this.transform.position = new Vector3(
                pickupAxis == Axis.X ? pickupMovementOnAxis : initialPickupPosition.x,
                pickupAxis == Axis.Y ? pickupMovementOnAxis : initialPickupPosition.y,
                pickupAxis == Axis.Z ? pickupMovementOnAxis : initialPickupPosition.z
            );
        } else {
            this.transform.rotation = Quaternion.Euler(
                pickupAxis == Axis.X ? pickupMovementOnAxis : initialPickupRotation.eulerAngles.x,
                pickupAxis == Axis.Y ? pickupMovementOnAxis : initialPickupRotation.eulerAngles.y,
                pickupAxis == Axis.Z ? pickupMovementOnAxis : initialPickupRotation.eulerAngles.z
            );
        }
    }

    string GetDisplayName() {
        return rotator.gameObject.name;
    }

    bool GetIsOwner() {
        return Networking.IsOwner(this.gameObject);
    }

    bool GetNeedsSnapping() {
        return targetAngles.Length >= 2;
    }

    void MovePickupToHand() {
        if (isPickingUp) {
            if (inputMethod == AxisInputMethods.Slide) {
                var handPosition = GetHandPosition();

                lastKnownPickupPosition = new Vector3(
                    pickupAxis == Axis.X ? handPosition.x : initialPickupPosition.x,
                    pickupAxis == Axis.Y ? handPosition.y : initialPickupPosition.y,
                    pickupAxis == Axis.Z ? handPosition.z : initialPickupPosition.z
                );
            } else {
                var handRotation = GetHandRotation();

                lastKnownPickupRotation = Quaternion.Euler(
                    pickupAxis == Axis.X ? ((handRotation.eulerAngles.x - 90f) * -1) : initialPickupRotation.eulerAngles.x,
                    pickupAxis == Axis.Y ? ((handRotation.eulerAngles.y - 90f) * -1) : initialPickupRotation.eulerAngles.y,
                    pickupAxis == Axis.Z ? ((handRotation.eulerAngles.z - 90f) * -1) : initialPickupRotation.eulerAngles.z
                );
            }
        }
        
        // need to do this each frame otherwise it moves with the rotator causing an infinite rotation effect
        if (inputMethod == AxisInputMethods.Slide) {
            this.transform.position = lastKnownPickupPosition;
        } else {
            this.transform.rotation = lastKnownPickupRotation;
        }
    }

    Vector3 GetBonePosition(HumanBodyBones humanBodyBone) {
        #if UNITY_EDITOR
        if (humanBodyBone == HumanBodyBones.RightIndexDistal) {
            return fakeHand.Find("Index/IndexDistal").position;
        } else {
            return fakeHand.Find("Index").position;
        }
        #else
        return Networking.LocalPlayer.GetBonePosition(humanBodyBone);
        #endif
    }

    Quaternion GetBoneRotation(HumanBodyBones humanBodyBone) {
        #if UNITY_EDITOR
        if (humanBodyBone == HumanBodyBones.RightIndexDistal) {
            return fakeHand.Find("Index/IndexDistal").rotation;
        } else {
            return fakeHand.Find("Index").rotation;
        }
        #else
        return Networking.LocalPlayer.GetBoneRotation(humanBodyBone);
        #endif
    }

    Vector3 GetHandPosition() {
        #if UNITY_EDITOR
        return fakeHand.position;
        #else
        var trackingData = Networking.LocalPlayer.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.RightHand);
        return trackingData.position;
        #endif
    }

    Vector3 GetIndexFingerTipPosition() {
        return GetBonePosition(HumanBodyBones.RightIndexDistal);
    }

    Quaternion GetHandRotation() {
        #if UNITY_EDITOR
        return fakeHand.rotation;
        #else
        var trackingData = Networking.LocalPlayer.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.RightHand);
        return trackingData.rotation;
        #endif
    }

    bool GetIsBoneInsideCollider() {
        if (boxCollider == null && sphereCollider == null) {
            return false;
        }

        Bounds colliderBounds = boxCollider != null ? boxCollider.bounds : sphereCollider.bounds;

        var bonePosition = useFingerCollision ? GetIndexFingerTipPosition() : GetHandPosition();

        bool isInsideCollider = colliderBounds.Contains(bonePosition);

        // Debug.Log("Is hand position " + bonePosition + " in collider " + (isInsideCollider ? "YES": "NO"));

        return isInsideCollider;
    }

    void DetectHandHover() {
        if (boxCollider == null && sphereCollider == null) {
            return;
        }

        // prevent edge case where boxcollider physically moves away from finger on push in so triggers another collision
        if (timeBeforeNextCollisionCheck != -1) {
            if (Time.time > timeBeforeNextCollisionCheck) {
                timeBeforeNextCollisionCheck = -1;
            }
            return;
        }

        var isBoneInsideCollider = GetIsBoneInsideCollider();

        if (!hasHandEnteredCollider) {
            if (isBoneInsideCollider) {
                hasHandEnteredCollider = true;

                OnHandEnter();

                timeBeforeNextCollisionCheck = Time.time + 0.5f; // Time.time in seconds
            }
        } else {
            if (!isBoneInsideCollider) {
                hasHandEnteredCollider = false;

                OnHandLeave();
            }
        }
    }

    void OnHandEnter() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" hand enter");

        if (isPickingUp) {
            return;
        }

        meshRenderer.material = highlightMaterial;
    }

    void SwitchToStandardMaterial() {
        meshRenderer.material = standardMaterial;
    }

    void OnHandLeave() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" hand leave");

        SwitchToStandardMaterial();
    }

    void OnCustomPickup() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" pickup");

        isPickingUp = true;

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        SwitchToStandardMaterial();
    }

    void OnCustomDrop() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" drop");

        isPickingUp = false;

        if (GetNeedsSnapping()) {
            Debug.Log("Snapping to nearest target angle...");
            rotatorMovementOnAxis = GetSnappedRotatorRotationOnAxis();
        } else {
            Debug.Log("Does not need snapping");
            HandlePercentages();
        }
    }

    void HandlePercentages() {
        if (fromAngle == -1 || toAngle == -1) {
            return;
        }

        var percent = GetRotatorRotationAsPercentage();

        NotifyReceiversOfPercentage(percent);
    }

    float NormalizeUnityRotationValueTo0to360(float unityRotationValue) {
        // Convert the Euler angle rotation value to be outside of 360 degrees
float convertedRotation = unityRotationValue % 360f;

// If the converted rotation is negative, add 360 to make it positive
if (convertedRotation < 0f)
{
    convertedRotation += 360f;
}

    return convertedRotation;
    }

    float GetRotatorRotationAsPercentage() {
        var rotatorRotation360 = NormalizeUnityRotationValueTo0to360(rotatorMovementOnAxis);
        rotatorRotation360 += 180;
        return DegreesToPercentage(rotatorRotation360, fromAngle, toAngle);
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        // knobs
        if (inputMethod == AxisInputMethods.Twist) {
            rotator.rotation = Quaternion.Euler(
                rotatorAxis == Axis.X ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.x,
                rotatorAxis == Axis.Y ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.y,
                rotatorAxis == Axis.Z ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.z
            );
            return;
        }

        Vector3 direction = this.transform.position - rotator.position;

        float angle = Mathf.Atan2(direction.y, direction.z) * Mathf.Rad2Deg;

        angle = angle + offset;

        angle = angle * -1;

        // Quaternion rotation = Quaternion.Euler(angle, 0f, 0f);
        Quaternion rotation = Quaternion.Euler(
            rotatorAxis == Axis.X ? angle : initialRotatorRotation.eulerAngles.x,
            rotatorAxis == Axis.Y ? angle : initialRotatorRotation.eulerAngles.y,
            rotatorAxis == Axis.Z ? angle : initialRotatorRotation.eulerAngles.z
        );

        rotator.rotation = rotation;
    }

    float GetRotatorRotationOnAxis() {
        if (rotator == null) {
            return 0f;
        }

        var currentRotationValue360 = GetPickupRotationAsDegreesOf360();

        if (GetNeedsSnapping()) {
            float firstTargetAngle = targetAngles[0];
            float lastTargetAngle = targetAngles[targetAngles.Length - 1];

            var clampedDegrees360 = ClampDegrees(currentRotationValue360, firstTargetAngle, lastTargetAngle);

            var nearestAngleInverted = inputMethod == AxisInputMethods.Twist ? clampedDegrees360 : clampedDegrees360 * -1;
            // var nearestAngleInverted = clampedDegrees360 * -1;
            var nearestAngleWithOffset = ConvertToPositiveDegrees(nearestAngleInverted + offset);

            var newRotationOnAxis = ConvertDegreesOutOf360ToRotationValue(nearestAngleWithOffset);

            // if (inputMethod == AxisInputMethods.Twist) {
            //     newRotationOnAxis = newRotationOnAxis * -1;
            // }
            
            // Debug.Log("Rotator Snapped " + currentRotationValue360 + "d -> (between " + firstTargetAngle + "d and " + lastTargetAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        } else {
            var clampedDegrees360 = (fromAngle != -1 && toAngle != -1 ? ClampDegrees(currentRotationValue360, fromAngle, toAngle) : currentRotationValue360);

            var degreesWithOffset = ConvertToPositiveDegrees(clampedDegrees360 + 180f);

            var newRotationOnAxis = ConvertDegreesOutOf360ToRotationValue(degreesWithOffset);

            // Debug.Log("Rotator " + currentRotationValue360 + "d -> (between " + fromAngle + "d and " + toAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        }
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
        float difference = (fromAngle > toAngle ? fromAngle - toAngle : toAngle - fromAngle); // 225 - 135 = 90
        float totalDegrees = 360 - difference; // 360 - 90 = 270

        float newDegrees = degrees + (fromAngle > toAngle ? toAngle : fromAngle); // 0 + 135 = 135 

        // if (newDegrees > 360) {
        //     newDegrees = newDegrees - 360;
		// }

        newDegrees = ConvertToPositiveDegrees(newDegrees); // 135

        float percent = newDegrees / totalDegrees * 100; // 135 / 270 * 100 = 50%

        Debug.Log("DEG  " + degrees + "  from:" + fromAngle + " to:" + toAngle + "  ->  %" + percent);

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

    float ConvertDegreesOutOf360ToRotationValue(float degreesOf360) {
        if (degreesOf360 > 180) {
            return degreesOf360 - 360;
        } else {
            return degreesOf360;
        }
    }

    float GetPickupRotationAsDegreesOf360() {
        // knobs
        if (inputMethod == AxisInputMethods.Twist) {
            var currentRotationValue = this.transform.rotation.eulerAngles[(int)pickupAxis];

            currentRotationValue = currentRotationValue - 90f;

            var currentRotationValueOf360 = NormalizeDegreesTo0to360(currentRotationValue);

            return currentRotationValueOf360;
        }

        // WARNING: The OnDrop clamping mechanism INVERTS the angle it is snapped to
        // ie. when lever is dropped lower it snaps to the HIGHER angle but visually is lower
        // if (isVertical) {
        //     Vector3 direction1 = rotator.position - this.transform.position;

        //     Quaternion targetRotation = Quaternion.LookRotation(direction1, Vector3.back);
        //     var angle1 = targetRotation.eulerAngles.z;
        //     var offset1= 0;

        //     // NOTE: always seems to be a tiiiiny bit different
        //     if (this.transform.position.x < rotator.position.x) {
        //         angle1 = -angle1;
        //     }

        //     var angleWithin3601 = ConvertToPositiveDegrees(angle1 - offset1);

        //     return angleWithin3601;
        // }

        Vector3 direction = this.transform.position - rotator.position;

        // NOTE: always between 0-180 degrees
        float angle = Vector3.Angle(Vector3.back, direction);

        // Debug.Log("GetPickupRotationAsDegreesOf360 direction=" + direction + " angle=" + angle + "d");

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
        float desiredRotation360 = overrideCurrentRotationOf360 != -1 ? overrideCurrentRotationOf360 : GetPickupRotationAsDegreesOf360();

        float nearestAngle = FindNearestAngle(desiredRotation360);
        int indexOfNearestAngle = FindNearestAngleIndex(desiredRotation360);

        if (indexOfNearestAngle != selectedIndex) {
            selectedIndex = indexOfNearestAngle;
            syncedSelectedIndex = selectedIndex;
            NotifyReceiversOfIndex(selectedIndex);
        }

        var nearestAngleInverted = inputMethod == AxisInputMethods.Twist ? nearestAngle + 180f : nearestAngle * -1;
        var nearestAngleInvertedWithOffset = nearestAngleInverted;
        var nearestAngleWithOffset = ConvertToPositiveDegrees(nearestAngleInvertedWithOffset);
        
        var rotatorRotation = ConvertDegreesOutOf360ToRotationValue(nearestAngleWithOffset);

        Debug.Log("Snapped lever " + GetDisplayName() + " from " + desiredRotation360 + "d -> " + rotatorRotation + "d -> " + nearestAngleWithOffset + "d [index " + indexOfNearestAngle.ToString() + ", " + nearestAngle.ToString() + "d]");
    
        return rotatorRotation;
    }

    void NotifyReceiversOfIndex(int newIndex) {
        if (receivers == null) {
            return;
        }

        foreach (var receiver in receivers) {
            receiver.OnIndex(newIndex);
        }
    }

    void NotifyReceiversOfPercentage(float newPercent) {
        if (receivers == null) {
            return;
        }

        foreach (var receiver in receivers) {
            receiver.OnPercent(newPercent);
        }
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
