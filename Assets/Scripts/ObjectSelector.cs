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
    public bool autoFindEditableMeshes = true; // Automatically find all EditableMesh objects
    [Tooltip("Auto-detect crosshair mode based on cursor lock state")]
    public bool autoDetectCrosshairMode = true;
    
    [Header("Visual Feedback")]
    public bool showSelectionOutline = true;
    public Color selectionColor = Color.yellow;
    
    private Transform currentSelection;
    
    void Start()
    {
        UpdateCameraReference();
        
        if (transformPanel == null)
            transformPanel = FindAnyObjectByType<TransformPanel>();
    }
    
    void Update()
    {
        // Update camera reference dynamically
        if (raycastCamera == null || !raycastCamera.enabled)
        {
            UpdateCameraReference();
        }
        
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
        
        // Auto-detect crosshair mode based on cursor lock state
        bool useCrosshair = autoDetectCrosshairMode && (Cursor.lockState == CursorLockMode.Locked);
        
        if (useCrosshair)
        {
            // Use screen center (crosshair position) instead of mouse
            mousePos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
        
        // Get all selectable objects
        GameObject[] selectableObjects = GetSelectableObjects();
        
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
            // Don't deselect if current selection is in edit mode
            if (currentSelection != null)
            {
                EditableMesh mesh = currentSelection.GetComponent<EditableMesh>();
                if (mesh != null && mesh.mode == EditableMesh.DisplayMode.Edit)
                {
                    // Keep selection - can't deselect while in edit mode
                    return;
                }
            }
            
            DeselectObject();
        }
    }
    
    GameObject[] GetSelectableObjects()
    {
        if (autoFindEditableMeshes)
        {
            // Find all EditableMesh components in scene
            EditableMesh[] meshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
            GameObject[] objects = new GameObject[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                objects[i] = meshes[i].gameObject;
            }
            return objects;
        }
        
        return new GameObject[0];
    }
    
    /// <summary>
    /// For TopMenuBar to notify when new objects are created
    /// </summary>
    public void AddSelectableObject(string objectName)
    {
        // This method exists for API compatibility but isn't needed with auto-find
    }
    
    /// <summary>
    /// Get currently selected object (for EditableMesh to check if it's selected)
    /// </summary>
    public Transform GetCurrentSelection()
    {
        return currentSelection;
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
        }
        
        if (transformPanel != null)
        {
            transformPanel.ClearSelection();
        }
    }
    
}

