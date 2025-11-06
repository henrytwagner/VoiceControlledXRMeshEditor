using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Click on objects to select them and display in TransformPanel
/// Works with TransformPanel to show object info
/// </summary>
public class ObjectSelector : MonoBehaviour
{
    [Header("References")]
    public Camera raycastCamera;
    public TransformPanel transformPanel;
    
    [Header("Settings")]
    public float selectRadius = 50f; // Screen-space selection radius in pixels
    public string[] selectableObjectNames = new string[] { "EditableMesh", "EditableCube" };
    
    [Header("Visual Feedback")]
    public bool showSelectionOutline = true;
    public Color selectionColor = Color.yellow;
    
    private Transform currentSelection;
    private Renderer selectedRenderer;
    private Color originalOutlineColor;
    
    void Start()
    {
        UpdateCameraReference();
        
        if (transformPanel == null)
            transformPanel = FindObjectOfType<TransformPanel>();
    }
    
    void Update()
    {
        // Update camera reference dynamically
        if (raycastCamera == null || !raycastCamera.enabled)
        {
            UpdateCameraReference();
        }
        
        // Only select when cursor is not locked (not in camera control mode)
        if (Cursor.lockState == CursorLockMode.Locked)
            return;
        
        HandleSelection();
    }
    
    void UpdateCameraReference()
    {
        // Priority 1: DesktopCamera
        GameObject desktopCam = GameObject.Find("DesktopCamera");
        if (desktopCam != null)
        {
            Camera cam = desktopCam.GetComponent<Camera>();
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                raycastCamera = cam;
                return;
            }
        }
        
        // Priority 2: Camera.main
        if (Camera.main != null && Camera.main.enabled)
        {
            raycastCamera = Camera.main;
        }
    }
    
    void HandleSelection()
    {
        bool clickPressed = false;
        Vector2 mousePos = Vector2.zero;
        
        #if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            clickPressed = mouse.leftButton.wasPressedThisFrame;
            mousePos = mouse.position.ReadValue();
        }
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        mousePos = Input.mousePosition;
        #endif
        
        if (!clickPressed || raycastCamera == null)
            return;
        
        // Use screen-space distance method (same as vertex selection - much more reliable!)
        GameObject[] selectableObjects = new GameObject[selectableObjectNames.Length];
        for (int i = 0; i < selectableObjectNames.Length; i++)
        {
            selectableObjects[i] = GameObject.Find(selectableObjectNames[i]);
        }
        
        float closestDist = float.MaxValue;
        Transform closestObject = null;
        
        foreach (GameObject obj in selectableObjects)
        {
            if (obj == null)
                continue;
            
            // Get object's screen position
            Vector3 worldPos = obj.transform.position;
            Vector3 screenPos = raycastCamera.WorldToScreenPoint(worldPos);
            
            // Skip if behind camera
            if (screenPos.z < 0)
                continue;
            
            // Calculate screen-space distance
            float dist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), mousePos);
            
            if (dist < selectRadius && dist < closestDist)
            {
                closestDist = dist;
                closestObject = obj.transform;
            }
        }
        
        if (closestObject != null)
        {
            SelectObject(closestObject);
        }
        else
        {
            DeselectObject();
        }
    }
    
    void SelectObject(Transform obj)
    {
        // Don't reselect same object
        if (currentSelection == obj)
            return;
        
        // Deselect previous
        DeselectObject();
        
        // Select new
        currentSelection = obj;
        
        // Update transform panel
        if (transformPanel != null)
        {
            transformPanel.SetSelectedObject(obj);
        }
    }
    
    void DeselectObject()
    {
        if (currentSelection != null)
        {
            currentSelection = null;
            selectedRenderer = null;
        }
        
        if (transformPanel != null)
        {
            transformPanel.ClearSelection();
        }
    }
    
}

