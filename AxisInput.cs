
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

public enum Axis {
    X,
    Y,
    Z
}

public enum InputTypes {
    Generic,
    Knob,
    Switch,
    PushButton,
    ThrottleLever,
    FlapsLever,
    GearLever
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
    public InputTypes inputType = InputTypes.Generic; 

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
    public float visualOffset = 0f;
    public float snappingOffset = 0f;
    public Material highlightMaterial;

    Vector3 initialPickupPosition;
    Quaternion initialPickupRotation;
    Quaternion initialRotatorRotation;
    Transform rotator;
    bool isPickingUp = false;
    float maxDegrees;
    BoxCollider boxCollider;
    SphereCollider sphereCollider;
    bool hasHandEnteredCollider = false;
    float timeBeforeNextCollisionCheck = -1;
    Renderer meshRenderer;
    Material standardMaterial;
    Vector3 lastKnownPickupPosition;
    Quaternion lastKnownPickupRotation;
    bool isOwner;

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

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [DrawGizmo (GizmoType.Selected | GizmoType.NonSelected)]
    void OnDrawGizmos() {
        if (rotator == null) {
            rotator = this.transform.parent;
        }
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(rotator.position, 0.0025f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(this.transform.position, 0.0025f);
    }
#endif

    //////////////

    public override void OnOwnershipTransferred(VRCPlayerApi newOwner) {
        isOwner = (newOwner == Networking.LocalPlayer);
    }

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
        // TODO: Work out the trig for the initial position
            // var positionToUse = 0;

            // lastKnownPickupPosition = new Vector3(
            //     pickupAxis == Axis.X ? positionToUse : initialPickupPosition.x,
            //     pickupAxis == Axis.Y ? positionToUse : initialPickupPosition.y,
            //     pickupAxis == Axis.Z ? positionToUse : initialPickupPosition.z
            // );

            // this.transform.position = lastKnownPickupPosition;
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

    // Networking.IsOwner is laggy
    bool GetIsOwner() {
        if (isOwner == null) {
            isOwner = Networking.IsOwner(this.gameObject);
        }
        return isOwner;
    }

    void BecomeOwner() {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
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

        BecomeOwner();

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
        float convertedRotation = unityRotationValue % 360f;

        if (convertedRotation < 0f)
        {
            convertedRotation += 360f;
        }

        return convertedRotation;
    }

    float GetRotatorRotationAsPercentage() {
        var rotatorRotation360 = NormalizeUnityRotationValueTo0to360(rotatorMovementOnAxis);

        if (inputMethod == AxisInputMethods.Twist) {
            rotatorRotation360 += 180;
        } else {
            rotatorRotation360 -= 135;
            rotatorRotation360 = NormalizeDegreesTo0to360(rotatorRotation360);
        }

        return DegreesToPercentage(rotatorRotation360, fromAngle, toAngle);
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        if (inputMethod == AxisInputMethods.Twist) {
            rotator.rotation = Quaternion.Euler(
                rotatorAxis == Axis.X ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.x,
                rotatorAxis == Axis.Y ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.y,
                rotatorAxis == Axis.Z ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.z
            );
            return;
        }

        if (inputType == InputTypes.ThrottleLever || inputType == InputTypes.Switch) {
            rotator.rotation = Quaternion.Euler(
                rotatorAxis == Axis.X ? rotatorMovementOnAxis * -1 : initialRotatorRotation.eulerAngles.x,
                rotatorAxis == Axis.Y ? rotatorMovementOnAxis * -1 : initialRotatorRotation.eulerAngles.y,
                rotatorAxis == Axis.Z ? rotatorMovementOnAxis * -1 : initialRotatorRotation.eulerAngles.z
            );
        } else {
            rotator.rotation = Quaternion.Euler(
                rotatorAxis == Axis.X ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.x,
                rotatorAxis == Axis.Y ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.y,
                rotatorAxis == Axis.Z ? rotatorMovementOnAxis : initialRotatorRotation.eulerAngles.z
            );
        }
    }

    float GetRotatorRotationOnAxis() {
        if (rotator == null) {
            return 0f;
        }

        var currentRotationValue360 = GetPickupMovementAsDegreesOf360();

        if (GetNeedsSnapping()) {
            float firstTargetAngle = targetAngles[0];
            float lastTargetAngle = targetAngles[targetAngles.Length - 1];

            var clampedDegrees360 = ClampDegrees(currentRotationValue360, firstTargetAngle, lastTargetAngle);

            var nearestAngleWithOffset = (
                inputMethod == AxisInputMethods.Twist ? 
                    clampedDegrees360 :
                inputType == InputTypes.FlapsLever ? 
                    clampedDegrees360 - 90f : 
                clampedDegrees360
            );
            nearestAngleWithOffset += visualOffset;
            var nearestAnglePositiveDegrees = ConvertToPositiveDegrees(nearestAngleWithOffset);

            var newRotationOnAxis = ConvertDegreesOutOf360ToRotationValue(nearestAnglePositiveDegrees);

            // if (inputMethod == AxisInputMethods.Twist) {
            //     newRotationOnAxis = newRotationOnAxis * -1;
            // }
            
            Debug.Log("Rotator Snapped  " + currentRotationValue360 + "d -> (between " + firstTargetAngle + "d and " + lastTargetAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        } else {
            var clampedDegrees360 = (fromAngle != -1 && toAngle != -1 ? ClampDegrees(currentRotationValue360, fromAngle, toAngle) : currentRotationValue360);

            var degreesWithOffset = ConvertToPositiveDegrees(clampedDegrees360 + (inputMethod == AxisInputMethods.Twist ? 180f : 135f));

            var newRotationOnAxis = ConvertDegreesOutOf360ToRotationValue(degreesWithOffset);

            Debug.Log("Rotator Unsnapped  " + currentRotationValue360 + "d -> (between " + fromAngle + "d and " + toAngle + "d) -> Clamp " + clampedDegrees360 + "d -> Actual " + newRotationOnAxis + "d");

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
        // degrees = 120
        float difference = (fromAngle > toAngle ? fromAngle - toAngle : toAngle - fromAngle); // 315 - 45 = 270
        float totalDegrees = 360 - difference; // 360 - 270 = 90

        float newDegrees = degrees + (fromAngle > toAngle ? toAngle : fromAngle); // 120 + 45 = 165

        // if (newDegrees > 360) {
        //     newDegrees = newDegrees - 360;
		// }

        newDegrees = ConvertToPositiveDegrees(newDegrees); // 165

        float percent = newDegrees / totalDegrees * 100; // 165 / 90 * 100

        // Debug.Log("DEG  " + degrees + "  from:" + fromAngle + " to:" + toAngle + "  ->  %" + percent);

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

    float GetPickupMovementAsDegreesOf360() {
        // knobs
        if (inputMethod == AxisInputMethods.Twist) {
            var currentRotationValue = this.transform.rotation.eulerAngles[(int)pickupAxis];

            currentRotationValue = currentRotationValue - 90f;

            var currentRotationValueOf360 = NormalizeDegreesTo0to360(currentRotationValue);

            return currentRotationValueOf360;
        }

        Vector3 direction = this.transform.position - rotator.position;

        // NOTE: always between 0-180 degrees
        float angle = Vector3.Angle(inputType == InputTypes.GearLever ? Vector3.up : Vector3.back, direction);

        // Debug.Log("GetPickupMovementAsDegreesOf360 direction=" + direction + " angle=" + angle + "d");

        // NOTE: always seems to be a tiiiiny bit different
        if (this.transform.position.x < rotator.position.x) {
            angle = -angle;
        }

        if (angle < 0f) {
            angle = angle + 360f;
        } else if (angle > 360f) {
            angle = angle - 360f;
        }

        var offset = inputType == InputTypes.ThrottleLever ? 90f : inputType == InputTypes.FlapsLever ? 90f : 0;
        var angleWithin360 = (angle - offset + 360) % 360;

        return angleWithin360;
    }

    float GetSnappedRotatorRotationOnAxis(float overrideCurrentRotationOf360 = -1f) {
        float desiredRotation360 = overrideCurrentRotationOf360 != -1 ? overrideCurrentRotationOf360 : GetPickupMovementAsDegreesOf360();

        float nearestAngle = FindNearestAngle(desiredRotation360);
        int indexOfNearestAngle = FindNearestAngleIndex(desiredRotation360);

        if (indexOfNearestAngle != selectedIndex) {
            selectedIndex = indexOfNearestAngle;
            syncedSelectedIndex = selectedIndex;
            NotifyReceiversOfIndex(selectedIndex);
        }

        var nearestAngleWithOffset = (
            inputMethod == AxisInputMethods.Twist ? 
                nearestAngle + 180f : 
            inputType == InputTypes.ThrottleLever ? 
                nearestAngle * -1 : 
            inputType == InputTypes.FlapsLever ?
                nearestAngle - 90f :
            nearestAngle);
        nearestAngleWithOffset += snappingOffset;
        var nearestAnglePositiveDegrees = ConvertToPositiveDegrees(nearestAngleWithOffset);
        
        var rotatorRotation = ConvertDegreesOutOf360ToRotationValue(nearestAnglePositiveDegrees);

        Debug.Log("Snapped lever " + GetDisplayName() + " from " + desiredRotation360 + "d -> " + nearestAngleWithOffset + "d -> " + rotatorRotation + "d  [index " + indexOfNearestAngle.ToString() + ", " + nearestAngle.ToString() + "d]");
    
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
