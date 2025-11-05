# Universal XR/Desktop Setup Guide

This guide explains how to set up your Unity scene to work in both VR and desktop modes automatically.

## Overview

The system includes 5 new scripts that enable your project to work seamlessly in both XR (VR) and traditional desktop modes:

1. **XRAutoSetup** - Detects XR device and enables appropriate components
2. **UniversalInput** - Unified input API for both controllers and mouse/keyboard
3. **DesktopCameraController** - First-person camera controls for desktop
4. **AdaptiveCanvas** - UI that works in both screen-space and world-space
5. **UniversalCubeInteractor** - EditableCube interaction in both modes

## Quick Setup (5 Minutes)

### Step 1: Create Desktop Camera

1. In Hierarchy, create a new **Empty GameObject**
2. Rename it to "Desktop Camera"
3. Position it at (0, 1.6, 0) - typical eye height
4. Add Component → **Camera**
5. Add Component → **DesktopCameraController**
6. Set the camera component to:
   - **Clear Flags** (Built-in RP) or **Background Type** (URP):
     - Built-in Render Pipeline: Set to "Skybox"
     - Universal Render Pipeline (URP): Set to "Skybox" 
     - High Definition RP (HDRP): Set to "Sky"
   - **Tag**: Set to "Untagged" (Main Camera tag should be on XR camera, not this one)
   - **Culling Mask**: Set to "Everything" (or whatever layers you want)
7. **Disable this GameObject** for now (we'll enable it via script)
   
   **Note:** If you're using URP, the Background setting is in the Camera component's "Rendering" section.
   Look for "Background Type" dropdown and select "Skybox".

### Step 2: Create XR Manager GameObject

1. In Hierarchy, create a new **Empty GameObject**
2. Rename it to "XR Manager"
3. Add Component → **XRAutoSetup**
4. In the Inspector, configure:
   - **Desktop Camera**: Drag your "Desktop Camera" GameObject here
   - **Desktop Controls**: Drag the DesktopCameraController component here
   - **XR Rig**: Drag your existing XR Origin GameObject here (optional)
   - **Show Debug Info**: Check this to see mode detection in Console

### Step 3: Configure Your UI Canvas (Optional)

If you have a UI Canvas:

1. Select your Canvas in Hierarchy
2. Add Component → **AdaptiveCanvas**
3. Configure settings:
   - **VR Position**: (0, 1.5, 2) - in front of user
   - **VR Scale**: 0.001 - typical for world-space UI
   - **Position In Front Of Camera**: Check this
   - **Desktop Sort Order**: 0

### Step 4: Add EditableCube Interaction (Optional)

If you want to interact with EditableCube in both modes:

1. Create a new Empty GameObject
2. Rename it to "Cube Interactor"
3. Add Component → **UniversalCubeInteractor**
4. Configure:
   - **Target Cube**: Drag your EditableCube GameObject
   - **Desktop Camera**: Drag your Desktop Camera
   - **Show Desktop Ray**: Check this for visual feedback
   - **Show Crosshair**: Check this if you have a crosshair UI

## Testing

### Desktop Mode (No VR Headset)

1. Make sure no VR headset is connected
2. Press Play in Unity Editor
3. You should see in Console: "No XR device detected - enabling desktop mode"
4. Controls:
   - **WASD** - Move
   - **Mouse** - Look around
   - **Shift** - Sprint
   - **Q/E** - Move down/up
   - **Escape** - Toggle cursor lock
   - **Left Click** - Interact (if using UniversalCubeInteractor)

### VR Mode (With VR Headset)

1. Connect your VR headset
2. Press Play
3. You should see in Console: "XR device detected - using VR mode"
4. Desktop camera and controls will be automatically disabled
5. Use VR controllers as normal

## Hierarchy Example

Your scene should look something like this:

```
Scene
├── XR Origin (your existing XR rig)
│   └── Camera Offset
│       └── Main Camera (tagged "MainCamera")
│
├── Desktop Camera (disabled by default)
│   └── DesktopCameraController component
│
├── XR Manager
│   └── XRAutoSetup component
│
├── Environment
│   └── Your game objects
│
├── UI Canvas
│   └── AdaptiveCanvas component (optional)
│
└── Cube Interactor (optional)
    └── UniversalCubeInteractor component
```

## Using Universal Input in Your Scripts

Instead of using `Input.GetMouseButton()` or XR-specific input, use the UniversalInput class:

```csharp
using UnityEngine;

public class MyScript : MonoBehaviour
{
    void Update()
    {
        // Works in both VR and desktop!
        if (UniversalInput.GetSelectDown())
        {
            Debug.Log("Select pressed!");
        }
        
        Vector2 movement = UniversalInput.GetMovementInput();
        
        bool isXR = UniversalInput.IsXRActive();
    }
}
```

## Common Issues & Solutions

### Issue: Desktop camera not activating
**Solution**: Make sure the Desktop Camera GameObject is disabled in the hierarchy. XRAutoSetup will enable it when needed.

### Issue: Both cameras active at once
**Solution**: Only the XR Origin camera should have the "MainCamera" tag. The desktop camera should be untagged.

### Issue: UI not visible in VR
**Solution**: Make sure AdaptiveCanvas is attached and "Position In Front Of Camera" is checked.

### Issue: No input working
**Solution**: Check that you have Unity's Input System package installed (Window → Package Manager → Input System).

## Building

### Desktop Build
1. File → Build Settings
2. Select Windows/Mac/Linux
3. Build and Run
4. Works automatically without VR device

### VR Build
1. File → Build Settings
2. Select your VR platform (Windows for PCVR, Android for Quest)
3. Make sure XR Plug-in Management is configured for that platform
4. Build and Run

## Advanced: Custom Interactions

To add your own interactions that work in both modes:

```csharp
using UnityEngine;

public class MyInteractor : MonoBehaviour
{
    void Update()
    {
        Ray ray = GetRay();
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (UniversalInput.GetSelectDown())
            {
                // Interact with hit object
            }
        }
    }
    
    Ray GetRay()
    {
        if (UniversalInput.IsXRActive())
        {
            // VR: Use controller (implement controller tracking)
            return new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        }
        else
        {
            // Desktop: Use camera center
            Vector3 center = new Vector3(Screen.width/2, Screen.height/2, 0);
            return Camera.main.ScreenPointToRay(center);
        }
    }
}
```

## Summary

With this setup:
- ✅ Works in VR when headset connected
- ✅ Works on desktop without VR
- ✅ Same scene, same code
- ✅ Automatic detection
- ✅ No manual switching needed

Your project is now universal!

