using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Universal input wrapper that works with both XR controllers and desktop mouse/keyboard.
/// Use this instead of direct Input calls for cross-platform compatibility.
/// </summary>
public static class UniversalInput
{
    /// <summary>
    /// Returns true if the primary select button is pressed (VR trigger or mouse left click)
    /// </summary>
    public static bool GetSelectPressed()
    {
        // Check XR controllers
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                return true;
            }
            
            // Also check left hand
            UnityEngine.XR.InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid && leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftTriggerPressed) && leftTriggerPressed)
            {
                return true;
            }
        }
        
        // Fallback to mouse
        #if ENABLE_INPUT_SYSTEM
        return Mouse.current?.leftButton.isPressed ?? false;
        #else
        return Input.GetMouseButton(0);
        #endif
    }

    /// <summary>
    /// Returns true on the frame the select button was pressed
    /// </summary>
    public static bool GetSelectDown()
    {
        // Check XR controllers
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                return true;
            }
        }
        
        // Fallback to mouse
        #if ENABLE_INPUT_SYSTEM
        return Mouse.current?.leftButton.wasPressedThisFrame ?? false;
        #else
        return Input.GetMouseButtonDown(0);
        #endif
    }

    /// <summary>
    /// Returns true if the grip button is pressed (VR grip or right mouse button)
    /// </summary>
    public static bool GetGripPressed()
    {
        // Check XR controllers
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripPressed) && gripPressed)
            {
                return true;
            }
        }
        
        // Fallback to right mouse button
        #if ENABLE_INPUT_SYSTEM
        return Mouse.current?.rightButton.isPressed ?? false;
        #else
        return Input.GetMouseButton(1);
        #endif
    }

    /// <summary>
    /// Returns movement input (VR thumbstick or WASD)
    /// </summary>
    public static Vector2 GetMovementInput()
    {
        // Check XR controller thumbstick
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid && leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 thumbstick) && thumbstick.sqrMagnitude > 0.01f)
            {
                return thumbstick;
            }
        }
        
        // Fallback to WASD
        Vector2 movement = Vector2.zero;
        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) movement.y += 1;
            if (kb.sKey.isPressed) movement.y -= 1;
            if (kb.aKey.isPressed) movement.x -= 1;
            if (kb.dKey.isPressed) movement.x += 1;
        }
        #else
        movement.x = Input.GetAxis("Horizontal");
        movement.y = Input.GetAxis("Vertical");
        #endif
        
        return movement;
    }

    /// <summary>
    /// Returns true if the menu/escape button is pressed
    /// </summary>
    public static bool GetMenuPressed()
    {
        // Check XR controllers
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuPressed) && menuPressed)
            {
                return true;
            }
        }
        
        // Fallback to Escape key
        #if ENABLE_INPUT_SYSTEM
        return Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
        #else
        return Input.GetKeyDown(KeyCode.Escape);
        #endif
    }

    /// <summary>
    /// Gets the position of the primary controller or mouse in world space
    /// </summary>
    public static Vector3 GetPointerPosition(Camera fallbackCamera = null)
    {
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position))
            {
                return position;
            }
        }
        
        // For desktop, we'd need to raycast from camera
        // Return camera position as fallback
        if (fallbackCamera != null)
            return fallbackCamera.transform.position;
        else if (Camera.main != null)
            return Camera.main.transform.position;
        
        return Vector3.zero;
    }

    /// <summary>
    /// Gets the rotation of the primary controller or camera
    /// </summary>
    public static Quaternion GetPointerRotation(Camera fallbackCamera = null)
    {
        if (XRSettings.isDeviceActive)
        {
            UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
            {
                return rotation;
            }
        }
        
        // For desktop, use camera rotation
        if (fallbackCamera != null)
            return fallbackCamera.transform.rotation;
        else if (Camera.main != null)
            return Camera.main.transform.rotation;
        
        return Quaternion.identity;
    }

    /// <summary>
    /// Returns true if currently in XR mode
    /// </summary>
    public static bool IsXRActive()
    {
        return XRSettings.isDeviceActive;
    }
}

