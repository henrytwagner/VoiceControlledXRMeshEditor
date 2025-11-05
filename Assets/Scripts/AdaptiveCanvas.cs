using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Automatically configures a Canvas to work in both XR and desktop modes.
/// In VR: Uses World Space rendering positioned in 3D space.
/// In Desktop: Uses Screen Space Overlay for traditional 2D UI.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class AdaptiveCanvas : MonoBehaviour
{
    [Header("VR Settings")]
    [Tooltip("Position in world space when in VR mode")]
    public Vector3 vrPosition = new Vector3(0, 1.5f, 2f);
    
    [Tooltip("Rotation in world space when in VR mode")]
    public Vector3 vrRotation = Vector3.zero;
    
    [Tooltip("Scale multiplier for VR (typically very small like 0.001)")]
    public float vrScale = 0.001f;
    
    [Tooltip("Distance from camera to position canvas")]
    public float vrDistanceFromCamera = 2f;
    
    [Tooltip("Position canvas in front of camera on start")]
    public bool positionInFrontOfCamera = true;
    
    [Header("Desktop Settings")]
    [Tooltip("Sort order for screen space canvas")]
    public int desktopSortOrder = 0;
    
    private Canvas canvas;
    private RectTransform rectTransform;
    
    void Start()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        
        ConfigureForCurrentMode();
    }
    
    void ConfigureForCurrentMode()
    {
        bool isXR = XRSettings.isDeviceActive;
        
        if (isXR)
        {
            ConfigureForVR();
        }
        else
        {
            ConfigureForDesktop();
        }
    }
    
    void ConfigureForVR()
    {
        Debug.Log("[AdaptiveCanvas] Configuring for VR mode (World Space)");
        
        // Set to world space
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Position the canvas
        if (positionInFrontOfCamera && Camera.main != null)
        {
            // Position in front of the camera
            Transform camTransform = Camera.main.transform;
            transform.position = camTransform.position + camTransform.forward * vrDistanceFromCamera;
            transform.rotation = Quaternion.LookRotation(transform.position - camTransform.position);
        }
        else
        {
            // Use fixed world position
            transform.position = vrPosition;
            transform.rotation = Quaternion.Euler(vrRotation);
        }
        
        // Scale down for VR (world space canvases are typically very large)
        transform.localScale = Vector3.one * vrScale;
        
        // Set rect transform for proper sizing in world space
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(1920, 1080); // Standard UI size
        }
    }
    
    void ConfigureForDesktop()
    {
        Debug.Log("[AdaptiveCanvas] Configuring for Desktop mode (Screen Space)");
        
        // Set to screen space overlay
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = desktopSortOrder;
        
        // Reset transform for screen space
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
    
    // Call this if XR mode changes at runtime
    public void RefreshMode()
    {
        ConfigureForCurrentMode();
    }
    
    // Public getters
    public bool IsInVRMode()
    {
        return canvas.renderMode == RenderMode.WorldSpace;
    }
}

