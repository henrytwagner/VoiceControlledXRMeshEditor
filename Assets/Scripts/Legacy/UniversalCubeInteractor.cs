using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Universal interaction system for EditableCube that works in both VR and desktop modes.
/// In VR: Uses controller rays for interaction.
/// In Desktop: Uses camera center raycast with mouse controls.
/// </summary>
public class UniversalCubeInteractor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The EditableCube to interact with")]
    public EditableCube targetCube;
    
    [Tooltip("Camera to use for desktop raycast (defaults to Camera.main)")]
    public Camera desktopCamera;
    
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance for interaction")]
    public float interactionDistance = 10f;
    
    [Tooltip("Layer mask for raycasting")]
    public LayerMask interactionLayers = -1;
    
    [Header("Visual Feedback")]
    [Tooltip("Show interaction ray in desktop mode")]
    public bool showDesktopRay = true;
    
    [Tooltip("Line renderer for visual feedback (optional)")]
    public LineRenderer rayLine;
    
    [Tooltip("Color when pointing at nothing")]
    public Color idleColor = Color.white;
    
    [Tooltip("Color when pointing at interactable")]
    public Color highlightColor = Color.green;
    
    [Tooltip("Color when grabbing")]
    public Color grabColor = Color.yellow;
    
    [Header("Desktop Crosshair")]
    [Tooltip("Show crosshair in desktop mode")]
    public bool showCrosshair = true;
    
    [Tooltip("Crosshair UI element (optional)")]
    public UnityEngine.UI.Image crosshairImage;
    
    // Private state
    private Transform currentTarget;
    private bool isGrabbing;
    private Vector3 grabOffset;
    private int grabbedCornerIndex = -1;
    
    void Start()
    {
        if (desktopCamera == null)
        {
            desktopCamera = Camera.main;
        }
        
        // Create simple line renderer if none provided
        if (rayLine == null && showDesktopRay)
        {
            GameObject lineObj = new GameObject("InteractionRay");
            lineObj.transform.SetParent(transform);
            rayLine = lineObj.AddComponent<LineRenderer>();
            rayLine.startWidth = 0.01f;
            rayLine.endWidth = 0.01f;
            rayLine.material = new Material(Shader.Find("Sprites/Default"));
            rayLine.positionCount = 2;
        }
        
        // Configure crosshair visibility
        if (crosshairImage != null)
        {
            bool isXR = XRSettings.isDeviceActive;
            crosshairImage.gameObject.SetActive(!isXR && showCrosshair);
        }
    }
    
    void Update()
    {
        PerformRaycast();
        HandleInteraction();
        UpdateVisuals();
    }
    
    void PerformRaycast()
    {
        Ray ray = GetInteractionRay();
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayers))
        {
            currentTarget = hit.collider.transform;
            
            // Update line renderer
            if (rayLine != null)
            {
                rayLine.SetPosition(0, ray.origin);
                rayLine.SetPosition(1, hit.point);
            }
        }
        else
        {
            currentTarget = null;
            
            // Update line renderer to max distance
            if (rayLine != null)
            {
                rayLine.SetPosition(0, ray.origin);
                rayLine.SetPosition(1, ray.origin + ray.direction * interactionDistance);
            }
        }
    }
    
    void HandleInteraction()
    {
        bool selectPressed = UniversalInput.GetSelectPressed();
        
        if (selectPressed && !isGrabbing && currentTarget != null)
        {
            // Check if we're hitting a vertex sphere
            if (currentTarget.name.StartsWith("corner_"))
            {
                TryGrabVertex(currentTarget);
            }
        }
        else if (!selectPressed && isGrabbing)
        {
            ReleaseVertex();
        }
        
        if (isGrabbing)
        {
            UpdateGrabbedVertex();
        }
    }
    
    void TryGrabVertex(Transform vertexTransform)
    {
        if (targetCube == null) return;
        
        // Extract corner index from name (e.g., "corner_3")
        string indexStr = vertexTransform.name.Replace("corner_", "");
        if (int.TryParse(indexStr, out int cornerIndex))
        {
            isGrabbing = true;
            grabbedCornerIndex = cornerIndex;
            
            // Calculate grab offset
            Vector3[] corners = targetCube.GetCorners();
            if (cornerIndex >= 0 && cornerIndex < corners.Length)
            {
                Vector3 worldCornerPos = targetCube.transform.TransformPoint(corners[cornerIndex]);
                grabOffset = worldCornerPos - GetInteractionPoint();
            }
            
            Debug.Log($"[UniversalCubeInteractor] Grabbed vertex {cornerIndex}");
        }
    }
    
    void UpdateGrabbedVertex()
    {
        if (targetCube == null || grabbedCornerIndex < 0) return;
        
        // Get new position from interaction point
        Vector3 newWorldPos = GetInteractionPoint() + grabOffset;
        Vector3 newLocalPos = targetCube.transform.InverseTransformPoint(newWorldPos);
        
        // Update the corner
        targetCube.SetCorner(grabbedCornerIndex, newLocalPos);
    }
    
    void ReleaseVertex()
    {
        if (isGrabbing)
        {
            Debug.Log($"[UniversalCubeInteractor] Released vertex {grabbedCornerIndex}");
            isGrabbing = false;
            grabbedCornerIndex = -1;
        }
    }
    
    void UpdateVisuals()
    {
        if (rayLine != null)
        {
            bool isXR = XRSettings.isDeviceActive;
            
            // Show line only in desktop mode (or always if desired)
            rayLine.enabled = !isXR && showDesktopRay;
            
            // Update color based on state
            Color targetColor = idleColor;
            if (isGrabbing)
            {
                targetColor = grabColor;
            }
            else if (currentTarget != null)
            {
                targetColor = highlightColor;
            }
            
            rayLine.startColor = targetColor;
            rayLine.endColor = targetColor;
        }
        
        // Update crosshair color
        if (crosshairImage != null)
        {
            if (isGrabbing)
            {
                crosshairImage.color = grabColor;
            }
            else if (currentTarget != null)
            {
                crosshairImage.color = highlightColor;
            }
            else
            {
                crosshairImage.color = idleColor;
            }
        }
    }
    
    Ray GetInteractionRay()
    {
        if (XRSettings.isDeviceActive)
        {
            // VR: Use right controller ray
            // In a full implementation, you'd get the actual controller transform
            // For now, use camera forward as fallback
            if (Camera.main != null)
            {
                return new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            }
        }
        
        // Desktop: Ray from camera through screen center
        if (desktopCamera != null)
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            return desktopCamera.ScreenPointToRay(screenCenter);
        }
        
        return new Ray(Vector3.zero, Vector3.forward);
    }
    
    Vector3 GetInteractionPoint()
    {
        Ray ray = GetInteractionRay();
        
        // If we hit something, use hit point
        if (currentTarget != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayers))
            {
                return hit.point;
            }
        }
        
        // Otherwise, return point along ray at fixed distance
        return ray.origin + ray.direction * 2f;
    }
}

