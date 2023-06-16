
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

// WARNING: adding a new value here will screw up all preceding components
public enum InputTypes {
    Generic,
    Knob,
    Switch,
    OverheadSwitch,
    PushButton,
    ThrottleLever,
    FlapsLever,
    GearLever
}

public enum AxisInputMethods {
    Twist,
    Slide
}

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
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
    public float defaultRotationOffset = 90f;
    public float[] targetAngles;
    public AxisInputReceiver[] receivers;
    public float fromAngle = -1f;
    public float toAngle = -1f;
    public float visualOffset = 0f;
    public float snappingOffset = 0f;
    public bool invertTwist = false; // for ceiling knob
    public bool invertRotator = true; // for ND knob TODO: Find a way to make this generic/remove it
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
    float lastKnownPercent;
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
        }

#if UNITY_EDITOR
        fakeHand = GameObject.Find("/FakeHand").transform;
        fakeFinger = fakeHand.Find("Index/IndexDistal");
#endif

        meshRenderer = rotator.GetComponent<Renderer>();
        boxCollider = rotator.GetComponent<BoxCollider>();
        sphereCollider = rotator.GetComponent<SphereCollider>();
        standardMaterial = meshRenderer.material;
        
        BeginUpdateLoop();
        
        // SendCustomEventDelayedSeconds(nameof(BeginTestLoop), 5);
    }

    public void BeginUpdateLoop() {
        CustomUpdate();
        SendCustomEventDelayedFrames(nameof(BeginUpdateLoop), 5);
    }

    void CustomUpdate() {
        if (GetIsOwner()) {
            MovePickupToHand();

            if (isPickingUp) {
                rotatorMovementOnAxis = GetRotatorRotationOnAxis();
            }

            pickupMovementOnAxis = (inputMethod == AxisInputMethods.Slide ? this.transform.position[(int)pickupAxis] : this.transform.rotation[(int)pickupAxis]);
            
            var hasChanged = (pickupMovementOnAxis != syncedPickupMovementOnAxis || rotatorMovementOnAxis != syncedRotatorMovementOnAxis);
            
            syncedRotatorMovementOnAxis = rotatorMovementOnAxis;
            syncedPickupMovementOnAxis = pickupMovementOnAxis;

            if (hasChanged) {
                HandlePercentages();
                SyncVarsToOtherPlayers();
            }
        } else {
            UpdatePickupTransform();
        }
        
        MoveRotatorVisibly();
        
        DetectHandHover();
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    // [DrawGizmo (GizmoType.Selected)] // GizmoType.NonSelected
    void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(this.transform.parent.position, 0.0025f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(this.transform.position, 0.0025f);

        DrawRadius();
        DrawAngleGizmos();
    }

    void DrawRadius() {
        Gizmos.color = Color.green;
        var radius = inputMethod == AxisInputMethods.Slide ? Vector3.Distance(this.transform.parent.position, this.transform.position) : 0.02f;

        Vector3 position = this.transform.parent.position;
        Vector3 previousPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;
        float circumference = 2f * Mathf.PI * radius;
        int numSegments = 180;
        float angleStep = 360f / numSegments;

        for (int i = 0; i <= numSegments; i++) {
            float angle = i * angleStep;

            float x = 0;
            float y = 0;
            float z = 0;

            var axisToUse = inputMethod == AxisInputMethods.Slide ? rotatorAxis : pickupAxis;

            switch (axisToUse) {
                case Axis.X:
                    x = position.x;
                    y = position.y + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
                    z = position.z + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    break;
                case Axis.Y:
                    x = position.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
                    y = position.y;
                    z = position.z + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    break;
                case Axis.Z:
                    x = position.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
                    y = position.y + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    z = position.z;
                    break;
            }

            Vector3 currentPoint = new Vector3(x, y, z);

            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            else
            {
                firstPoint = currentPoint;
            }

            previousPoint = currentPoint;
        }

        Gizmos.DrawLine(previousPoint, firstPoint);
    }

    void DrawAngleGizmos() {
        var radius = inputMethod == AxisInputMethods.Slide ? Vector3.Distance(this.transform.parent.position, this.transform.position) : 0.02f;
        Vector3 startPoint = this.transform.parent.position;

        var offsetToAddToAllAngles = (inputMethod == AxisInputMethods.Twist ? -90f : 0) + visualOffset;

        var colorsByAngle = new System.Collections.Generic.Dictionary<Color, float>();
        colorsByAngle.Add(Color.white, 0f + offsetToAddToAllAngles);
        colorsByAngle.Add(new Color(1, 0, 1, 1), defaultRotation);

        if (fromAngle != -1) {
            colorsByAngle.Add(Color.yellow, (fromAngle + offsetToAddToAllAngles));
        }
        if (toAngle != -1) {
            colorsByAngle.Add(Color.red, (toAngle + offsetToAddToAllAngles));
        }

        for (var i = 0; i < targetAngles.Length; i++) {
            var targetAngle = targetAngles[i] + offsetToAddToAllAngles + snappingOffset;
            colorsByAngle.Add(new Color(1, 0.2f + ((float)i / 10), 0.5f), targetAngle);
        }

        colorsByAngle.Add(Color.green, rotatorMovementOnAxis);

        foreach (System.Collections.Generic.KeyValuePair<Color, float> entry in colorsByAngle) {
            Vector3 endPoint = GetLineEndPoint(startPoint, entry.Value, radius);

            Gizmos.color = entry.Key;
            Gizmos.DrawLine(startPoint, endPoint);
        }
    }

    Vector3 GetLineEndPoint(Vector3 startPoint, float angle, float distance) {
        float angleInRadians = angle * Mathf.Deg2Rad;

        float x = 0;
        float y = 0;
        float z = 0;

        var axisToUse = inputMethod == AxisInputMethods.Slide ? rotatorAxis : pickupAxis;

        switch (axisToUse) {
            case Axis.X:
                x = startPoint.x;
                y = startPoint.y + distance * Mathf.Cos(angleInRadians);
                z = startPoint.z + distance * Mathf.Sin(angleInRadians);
                break;
            case Axis.Y:
                x = startPoint.x + distance * Mathf.Cos(angleInRadians);
                y = startPoint.y;
                z = startPoint.z + distance * Mathf.Sin(angleInRadians);
                break;
            case Axis.Z:
                x = startPoint.x + distance * Mathf.Cos(angleInRadians);
                y = startPoint.y + distance * Mathf.Sin(angleInRadians);
                z = startPoint.z;
                break;
        }

        return new Vector3(x, y, z);
    }
#endif

    //////////////

    public override void OnPlayerJoined(VRCPlayerApi newPlayer) {
        RequestSerialization();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi newOwner) {
        isOwner = (newOwner.playerId == Networking.LocalPlayer.playerId);
        RequestSerialization();
    }

    public override void OnDeserialization() {
        rotatorMovementOnAxis = syncedRotatorMovementOnAxis;
        pickupMovementOnAxis = syncedPickupMovementOnAxis;

        HandlePercentages();

        if (syncedSelectedIndex != selectedIndex) {
            NotifyReceiversOfIndex(selectedIndex);
        }

        selectedIndex = syncedSelectedIndex;
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

    void SyncVarsToOtherPlayers() {
        if (!GetIsOwner()) {
            return;
        }

        RequestSerialization();
    }

    void InitializePickupTransform() {
        if (inputMethod == AxisInputMethods.Slide) {
            // TODO: Consolidate this into a single thing
            
            if (inputType == InputTypes.ThrottleLever) {
                var rotationToUse = ConvertDegreesOutOf360ToRotationValue(defaultRotation + defaultRotationOffset);
  
                // TODO: Calculate this?
                var distance = 0.1f;

                Vector3 offset = Quaternion.Euler(rotationToUse, 0f, 0f) * Vector3.forward * distance;

                var pos = rotator.transform.position + offset;
                var positionToUse = pos[(int)pickupAxis];

                lastKnownPickupPosition = new Vector3(
                    pickupAxis == Axis.X ? positionToUse : initialPickupPosition.x,
                    pickupAxis == Axis.Y ? positionToUse : initialPickupPosition.y,
                    pickupAxis == Axis.Z ? positionToUse : initialPickupPosition.z
                );

                this.transform.position = lastKnownPickupPosition;
            } else if (inputType == InputTypes.GearLever) {
                var rotationToUse = ConvertDegreesOutOf360ToRotationValue(defaultRotation + defaultRotationOffset);
  
                // TODO: Calculate this?
                var distance = 0.25f;

                Vector3 offset = Quaternion.Euler(rotationToUse, 0f, 0f) * Vector3.forward * distance;

                var pos = rotator.transform.position + offset;
                var positionToUse = pos[(int)pickupAxis];

                lastKnownPickupPosition = new Vector3(
                    pickupAxis == Axis.X ? positionToUse : initialPickupPosition.x,
                    pickupAxis == Axis.Y ? positionToUse : initialPickupPosition.y,
                    pickupAxis == Axis.Z ? positionToUse : initialPickupPosition.z
                );

                this.transform.position = lastKnownPickupPosition;
            } else {
                // TODO: Handle other kinds
            }
        } else {
            var rotationToUse = ConvertDegreesOutOf360ToRotationValue(defaultRotation + defaultRotationOffset);

            lastKnownPickupRotation = Quaternion.Euler(
                pickupAxis == Axis.X ? rotationToUse : initialPickupRotation.eulerAngles.x,
                pickupAxis == Axis.Y ? rotationToUse : initialPickupRotation.eulerAngles.y,
                pickupAxis == Axis.Z ? rotationToUse : initialPickupRotation.eulerAngles.z
            );

            Debug.Log("InitializePickupTransform " + rotationToUse + "d => " + lastKnownPickupRotation.eulerAngles);

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

    void UpdatePickupTransform() {
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
                    pickupAxis == Axis.X ? (((handRotation.eulerAngles.x * (invertTwist ? -1 : 1)) - 90f) * -1) : initialPickupRotation.eulerAngles.x,
                    pickupAxis == Axis.Y ? (((handRotation.eulerAngles.y * (invertTwist ? -1 : 1)) - 90f) * -1) : initialPickupRotation.eulerAngles.y,
                    pickupAxis == Axis.Z ? (((handRotation.eulerAngles.z * (invertTwist ? -1 : 1)) - 90f) * -1) : initialPickupRotation.eulerAngles.z
                );
            }
        }
        
        // need to do this each frame otherwise it moves with the rotator causing an infinite rotation effect
        if (inputMethod == AxisInputMethods.Slide) {
            if (this.transform.position != lastKnownPickupPosition) {
                this.transform.position = lastKnownPickupPosition;
            }
        } else {
            if (this.transform.rotation != lastKnownPickupRotation) {
                Debug.Log("MovePickupToHand rotation " + this.transform.rotation.eulerAngles + " is NOT the same, updating to " + lastKnownPickupRotation.eulerAngles);
                this.transform.rotation = lastKnownPickupRotation;
            }
        }
    }

    Quaternion GetBoneRotation(HumanBodyBones humanBodyBone) {
        #if UNITY_EDITOR
        if (humanBodyBone == HumanBodyBones.RightIndexDistal) {
            return fakeHand.Find("Index/IndexDistal").rotation;
        } else {
            return fakeHand.Find("Index").rotation;
        }
        #else
        if (Networking.LocalPlayer == null) {
            return Quaternion.identity;
        }

        return Networking.LocalPlayer.GetBoneRotation(humanBodyBone);
        #endif
    }

    Vector3 GetHandPosition() {
#if UNITY_EDITOR
        return fakeHand.position;
#else
        if (Networking.LocalPlayer == null) {
            return Vector3.zero;
        }

        var trackingData = Networking.LocalPlayer.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.RightHand);
        return trackingData.position;
#endif
    }

    Vector3 GetIndexFingerTipPosition() {
        // TODO: Calculate the finger tip somehow

#if UNITY_EDITOR
        return fakeFinger.position;
#else
        if (Networking.LocalPlayer == null) {
            return Vector3.zero;
        }

        var bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexDistal);

        if (bonePosition == Vector3.zero) {
            bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexIntermediate);

            if (bonePosition == Vector3.zero) {
                bonePosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexProximal);

                if (bonePosition == Vector3.zero) {
                    return GetHandPosition();
                }
            }
        }

        return bonePosition;
#endif
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

        VibrateController();

        HighlightObject();
    }

    void VibrateController() {
        Networking.LocalPlayer.PlayHapticEventInHand(VRC.SDK3.Components.VRCPickup.PickupHand.Right, 1f, 1f, 1f);
    }

    void HighlightObject() {
        meshRenderer.material = highlightMaterial;

        meshRenderer.material.renderQueue = 5000;
    }

    void UnhighlightObject() {
        meshRenderer.material = standardMaterial;
        
        meshRenderer.material.renderQueue = 2000; // standard
    }

    void OnHandLeave() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" hand leave");

        UnhighlightObject();
    }

    void OnCustomPickup() {
        Debug.Log("AxisInput \"" + GetDisplayName() + "\" pickup");

        isPickingUp = true;

        BecomeOwner();

        UnhighlightObject();
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

        var newPercent = GetRotatorRotationAsPercentage();

        if (lastKnownPercent == null || newPercent != lastKnownPercent) {
            NotifyReceiversOfPercentage(newPercent);
            lastKnownPercent = newPercent;
        }
    }

    float NormalizeUnityRotationValueTo0to360(float unityRotationValue) {
        float convertedRotation = unityRotationValue % 360f;

        if (convertedRotation < 0f) {
            convertedRotation += 360f;
        }

        return convertedRotation;
    }

    float GetRotatorRotationAsPercentage() {
        var rotatorRotation360 = NormalizeUnityRotationValueTo0to360(rotatorMovementOnAxis);

        if (inputMethod == AxisInputMethods.Twist) {
            rotatorRotation360 += 180;

            if (invertTwist) {
                rotatorRotation360 = 360 - rotatorRotation360;
            }
        } else {
            if (inputType == InputTypes.ThrottleLever) {
                rotatorRotation360 -= visualOffset;
            } else {
                // TODO: Verify this is the flaps/gear/speedbrake
                rotatorRotation360 -= 135;
            }
        }
        
        rotatorRotation360 = NormalizeDegreesTo0to360(rotatorRotation360);

        return DegreesToPercentage(rotatorRotation360, fromAngle, toAngle);
    }

    void MoveRotatorVisibly() {
        if (rotator == null) {
            return;
        }

        if (inputMethod == AxisInputMethods.Twist) {
            rotator.rotation = Quaternion.Euler(
                rotatorAxis == Axis.X ? (rotatorMovementOnAxis * (invertRotator ? -1 : 1)) : initialRotatorRotation.eulerAngles.x,
                rotatorAxis == Axis.Y ? (rotatorMovementOnAxis * (invertRotator ? -1 : 1)) : initialRotatorRotation.eulerAngles.y,
                rotatorAxis == Axis.Z ? (rotatorMovementOnAxis * (invertRotator ? -1 : 1)) : initialRotatorRotation.eulerAngles.z
            );
            return;
        }

        if (inputType == InputTypes.ThrottleLever || inputType == InputTypes.Switch || inputType == InputTypes.OverheadSwitch) {
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
            
            if (GetDisplayName() == "SWITCH_OVHD_EXTLT_STROBE.004") {
                Debug.Log("Rotator Snapped  " + currentRotationValue360 + "d -> (between " + firstTargetAngle + "d and " + lastTargetAngle + "d) -> " + clampedDegrees360 + "d -> " + newRotationOnAxis + "d");
            }

            return newRotationOnAxis;
        } else {
            var clampedDegrees360 = (fromAngle != -1 && toAngle != -1 ? ClampDegrees(currentRotationValue360, fromAngle, toAngle) : currentRotationValue360);

            var degreesWithOffset = ConvertToPositiveDegrees(clampedDegrees360 + (inputMethod == AxisInputMethods.Twist ? 180f : inputType == InputTypes.ThrottleLever ? visualOffset : 135f));

            var newRotationOnAxis = ConvertDegreesOutOf360ToRotationValue(degreesWithOffset);

            // Debug.Log("Rotator Unsnapped  " + currentRotationValue360 + "d -> (between " + fromAngle + "d and " + toAngle + "d) -> Clamp " + clampedDegrees360 + "d -> Actual " + newRotationOnAxis + "d");

            return newRotationOnAxis;
        }
    }

    float NormalizeDegreesTo0to360(float degrees) {
        return (degrees + 360) % 360;
    }

    float ClampDegrees(float angle, float min, float max) {
        if (angle > max && min == 0) {
            min = 360;
        }
        
        var center = min > max ? ((min - max) / 2) : Mathf.DeltaAngle(min, max);

        if (angle < min && angle > max) {
            if (min > max) {
                if (angle > (max + center)) {
                    return min;
                } else {
                    return max;
                } 
            } else {
                if (angle > center) {
                    return min;
                } else {
                    return max;
                }
            }
        } else if (angle > max && angle < min) {
            if (angle > center) {
                return max;
            } else {
                return min;
            }
        } else if (min > max) {
            return angle;
        } else if (angle < min) {
            return min;
        } else if (angle > max) {
            return max;
        }

        return angle;
    }

    float DegreesToPercentage(float degrees, float fromAngle, float toAngle) {
        // 343, from=311, to=15

        // 311 > 15 TRUE then diff=311-15 = 296
        float difference = (fromAngle > toAngle ? fromAngle - toAngle : toAngle - fromAngle);
        // 360 - 296 = 64
        float totalDegrees = 360 - difference;

        // 343 - 311 = 32
        float newDegrees = fromAngle > toAngle ? (degrees - fromAngle) : (degrees + fromAngle);

        // 32
        newDegrees = ConvertToPositiveDegrees(newDegrees);

        // 32 / 64 * 100
        float percent = newDegrees / totalDegrees * 100;

        // Debug.Log("DegreesToPercentage  " + degrees + "d  from " + fromAngle + "d -> " + toAngle + "d  result  %" + percent);

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

        var offset = (
            inputType == InputTypes.ThrottleLever ? 
                90f : 
            inputType == InputTypes.FlapsLever ? 
                90f : 
            inputType == InputTypes.OverheadSwitch ? 
                270f : 
            0
        );
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
            SyncVarsToOtherPlayers();
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







    // public void BeginTestLoop() {
    //     if (this.transform.parent.gameObject.name != "LEVER_THROTTLE_1.004") {
    //         return;
    //     }


    //     if (!GetIsOwner()) {
    //         Debug.Log("I am not the owner so I cannot do the test run");
    //         return;
    //     }


    //     Debug.Log("I am the owner so I am proceeding with test run...");


    //     isPickingUp = true;


    //     var newZ = fakeHand.position.z == 11.1884f ? 11.6498f : 11.1884f;

    //     fakeHand.position = new Vector3(fakeHand.position.x, fakeHand.position.y, newZ);
        

    //     SendCustomEventDelayedSeconds(nameof(BeginTestLoop), 5);
    // }
}
