using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Automatically detects if XR is active and enables desktop fallbacks if not.
/// Attach to a GameObject in your scene with references to desktop-only components.
/// </summary>
public class XRAutoSetup : MonoBehaviour
{
    [Header("Desktop Fallback Components")]
    [Tooltip("Camera to use when no XR device is detected")]
    public GameObject desktopCamera;
    
    [Tooltip("Desktop controls script (DesktopCameraController)")]
    public DesktopCameraController desktopControls;
    
    [Header("Optional: XR Components")]
    [Tooltip("XR Origin/Rig - will be disabled if no XR device")]
    public GameObject xrRig;
    
    [Header("Debug")]
    public bool showDebugInfo = true;

    void Start()
    {
        DetectAndConfigure();
    }

    void DetectAndConfigure()
    {
        // Check if XR device or loader is active (covers simulators)
        bool xrDeviceActive = XRSettings.isDeviceActive;
        bool xrLoaderActive = false;

        XRGeneralSettings generalSettings = XRGeneralSettings.Instance;
        if (generalSettings != null && generalSettings.Manager != null)
        {
            xrLoaderActive = generalSettings.Manager.activeLoader != null;
        }

        bool xrActive = xrDeviceActive || xrLoaderActive;
        
        if (showDebugInfo)
        {
            Debug.Log($"[XRAutoSetup] XR Device Active: {xrActive}");
            Debug.Log($"[XRAutoSetup] XR Device Name: {XRSettings.loadedDeviceName}");
            Debug.Log($"[XRAutoSetup] XR Loader Active: {xrLoaderActive}");
            Debug.Log($"[XRAutoSetup] XR Supported: {XRSettings.supportedDevices.Length > 0}");
        }

        if (!xrActive)
        {
            EnableDesktopMode();
        }
        else
        {
            EnableXRMode();
        }
    }

    void EnableDesktopMode()
    {
        if (showDebugInfo)
            Debug.Log("[XRAutoSetup] No XR device detected - enabling desktop mode");
        
        // Enable desktop components
        if (desktopCamera)
        {
            desktopCamera.SetActive(true);
            if (showDebugInfo)
                Debug.Log("[XRAutoSetup] Desktop camera enabled");
        }
        else
        {
            Debug.LogWarning("[XRAutoSetup] Desktop camera not assigned! Assign in Inspector.");
        }
        
        if (desktopControls)
        {
            desktopControls.enabled = true;
            if (showDebugInfo)
                Debug.Log("[XRAutoSetup] Desktop controls enabled");
        }
        else
        {
            Debug.LogWarning("[XRAutoSetup] Desktop controls not assigned! Assign in Inspector.");
        }
        
        // Optionally disable XR rig to save performance
        if (xrRig)
        {
            xrRig.SetActive(false);
            if (showDebugInfo)
                Debug.Log("[XRAutoSetup] XR Rig disabled");
        }
    }

    void EnableXRMode()
    {
        if (showDebugInfo)
            Debug.Log("[XRAutoSetup] XR device detected - using VR mode");
        
        // Disable desktop components
        if (desktopCamera)
        {
            desktopCamera.SetActive(false);
        }
        
        if (desktopControls)
        {
            desktopControls.enabled = false;
        }
        
        // Ensure XR rig is enabled
        if (xrRig)
        {
            xrRig.SetActive(true);
        }
    }

    // Public method to manually check XR status
    public bool IsXRActive()
    {
        return XRSettings.isDeviceActive;
    }
}

