using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime editor for EditableMesh - works with any imported mesh.
/// Press Tab to toggle Mesh/Vertex modes.
/// Click and drag vertices to edit in Play mode.
/// </summary>
public class RuntimeMeshEditor : MonoBehaviour
{
    [Header("References")]
    public EditableMesh targetMesh;
    public Camera editCamera;
    public DesktopCameraController cameraController;
    
    [Header("Settings")]
    [Tooltip("Selection radius in screen pixels")]
    public float selectRadius = 500f;
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;
    public bool disableCameraInVertexMode = true;
    public bool showLabels = true;
    
    [Header("Visual Aids")]
    public bool showEdges = true;
    public Color edgeColor = Color.cyan;
    public bool showInGameView = true; // Show edges in Game view, not just Scene view
    
    // State
    private EditableMesh.DisplayMode lastMode;
    private int selectedVertexIndex = -1;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Plane dragPlane;
    
    void Awake()
    {
        Debug.Log("[RuntimeMeshEditor] Initialized");
    }
    
    void Start()
    {
        Invoke(nameof(InitializeAfterSetup), 0.1f);
    }
    
    void InitializeAfterSetup()
    {
        UpdateEditCamera();
        FindComponents();
        
        if (targetMesh != null)
            lastMode = targetMesh.mode;
    }
    
    void UpdateEditCamera()
    {
        Camera activeCamera = null;
        
        // Prioritize DesktopCamera
        GameObject desktopCameraGO = GameObject.Find("DesktopCamera");
        if (desktopCameraGO != null && desktopCameraGO.activeInHierarchy)
        {
            Camera cam = desktopCameraGO.GetComponent<Camera>();
            if (cam != null && cam.enabled)
                activeCamera = cam;
        }
        
        // Fallback to any active camera
        if (activeCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    activeCamera = cam;
                    break;
                }
            }
        }
        
        editCamera = activeCamera != null ? activeCamera : Camera.main;
        Debug.Log($"[RuntimeMeshEditor] Using camera: {(editCamera != null ? editCamera.name : "NULL")}");
    }
    
    void FindComponents()
    {
        if (targetMesh == null)
            targetMesh = FindObjectOfType<EditableMesh>();
        
        if (cameraController == null)
            cameraController = FindObjectOfType<DesktopCameraController>(true);
    }
    
    void Update()
    {
        if (targetMesh == null)
            return;
        
        // Update camera if needed
        if (editCamera == null || !editCamera.enabled)
            UpdateEditCamera();
        
        // Check mode changes
        if (targetMesh.mode != lastMode)
        {
            OnModeChanged(targetMesh.mode);
            lastMode = targetMesh.mode;
        }
        
        // Only allow editing in Vertices mode
        if (targetMesh.mode != EditableMesh.DisplayMode.Vertices)
            return;
        
        HandleVertexSelection();
        HandleVertexDrag();
    }
    
    void OnModeChanged(EditableMesh.DisplayMode newMode)
    {
        bool isVertexMode = (newMode == EditableMesh.DisplayMode.Vertices);
        
        if (disableCameraInVertexMode && cameraController != null)
        {
            cameraController.enabled = !isVertexMode;
        }
        
        if (isVertexMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Deselect();
        }
    }
    
    void HandleVertexSelection()
    {
        if (editCamera == null)
            return;
        
        bool clickPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        clickPressed = Mouse.current?.leftButton.wasPressedThisFrame ?? false;
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        #endif
        
        if (!clickPressed)
            return;
        
        // Find closest vertex
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        float closestDist = float.MaxValue;
        int closestIndex = -1;
        
        #if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        #else
        Vector2 mousePos = Input.mousePosition;
        #endif
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z < 0) continue; // Behind camera
            
            float dist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), mousePos);
            
            if (dist < selectRadius && dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }
        
        if (closestIndex >= 0)
        {
            SelectVertex(closestIndex);
        }
        else
        {
            Deselect();
        }
    }
    
    void HandleVertexDrag()
    {
        if (selectedVertexIndex < 0 || editCamera == null)
            return;
        
        bool isDragging = false;
        
        #if ENABLE_INPUT_SYSTEM
        isDragging = Mouse.current?.leftButton.isPressed ?? false;
        #else
        isDragging = Input.GetMouseButton(0);
        #endif
        
        if (!isDragging)
        {
            Deselect();
            return;
        }
        
        Ray ray = GetMouseRay();
        float distance;
        
        if (dragPlane.Raycast(ray, out distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 localPoint = targetMesh.transform.InverseTransformPoint(worldPoint);
            targetMesh.SetVertex(selectedVertexIndex, localPoint);
        }
    }
    
    void SelectVertex(int index)
    {
        Deselect();
        selectedVertexIndex = index;
        
        // Highlight sphere
        Transform vertsRoot = targetMesh.transform.Find("_MeshVertices");
        if (vertsRoot != null)
        {
            Transform sphere = vertsRoot.Find($"vertex_{index}");
            if (sphere != null)
            {
                selectedRenderer = sphere.GetComponent<Renderer>();
                if (selectedRenderer != null)
                {
                    originalColor = selectedRenderer.material.color;
                    selectedRenderer.material.color = selectedColor;
                }
            }
        }
        
        // Setup drag plane
        Vector3[] vertices = targetMesh.GetVertices();
        Vector3 worldPos = targetMesh.transform.TransformPoint(vertices[index]);
        dragPlane = new Plane(editCamera.transform.forward, worldPos);
        
        Debug.Log($"[RuntimeMeshEditor] Selected vertex {index}");
    }
    
    void Deselect()
    {
        if (selectedRenderer != null)
        {
            selectedRenderer.material.color = originalColor;
            selectedRenderer = null;
        }
        selectedVertexIndex = -1;
    }
    
    Ray GetMouseRay()
    {
        if (editCamera == null)
            return new Ray(Vector3.zero, Vector3.forward);
        
        #if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        #else
        Vector2 mousePos = Input.mousePosition;
        #endif
        
        return editCamera.ScreenPointToRay(mousePos);
    }
    
    void OnGUI()
    {
        if (targetMesh == null || editCamera == null || !showLabels)
            return;
        
        bool inVertexMode = (targetMesh.mode == EditableMesh.DisplayMode.Vertices);
        
        if (!inVertexMode)
            return;
        
        // Draw vertex labels
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                Rect labelRect = new Rect(guiPos.x - 15, guiPos.y - 20, 30, 20);
                
                labelStyle.normal.textColor = (i == selectedVertexIndex) ? selectedColor : normalColor;
                GUI.Label(labelRect, $"V{i}", labelStyle);
            }
        }
        
        // Instructions
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.white;
        boxStyle.fontSize = 14;
        
        string instructions = "Vertex Mode: ON\nClick vertices to select & drag\nPress Tab to exit";
        GUI.Box(new Rect(10, 10, 250, 70), instructions, boxStyle);
        
        if (selectedVertexIndex >= 0)
        {
            GUI.Box(new Rect(10, 90, 150, 30), $"Selected: V{selectedVertexIndex}", boxStyle);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showEdges || targetMesh == null || showInGameView)
            return;
        
        bool inVertexMode = (targetMesh.mode == EditableMesh.DisplayMode.Vertices);
        if (!inVertexMode)
            return;
        
        DrawMeshEdges();
    }
    
    void OnRenderObject()
    {
        if (!showEdges || !showInGameView || targetMesh == null)
            return;
        
        bool inVertexMode = (targetMesh.mode == EditableMesh.DisplayMode.Vertices);
        if (!inVertexMode || !Application.isPlaying)
            return;
        
        // Use GL to draw lines in Game view
        DrawMeshEdgesGL();
    }
    
    void DrawMeshEdges()
    {
        // For Scene view (Gizmos)
        var edges = GetMeshEdges();
        if (edges == null)
            return;
        
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;
        
        Vector3[] meshVerts = mf.sharedMesh.vertices;
        
        Gizmos.color = edgeColor;
        foreach (var edge in edges)
        {
            Vector3 start = meshTransform.TransformPoint(meshVerts[edge.Item1]);
            Vector3 end = meshTransform.TransformPoint(meshVerts[edge.Item2]);
            Gizmos.DrawLine(start, end);
        }
    }
    
    void DrawMeshEdgesGL()
    {
        // For Game view (GL rendering)
        var edges = GetMeshEdges();
        if (edges == null)
            return;
        
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;
        
        Vector3[] meshVerts = mf.sharedMesh.vertices;
        
        // Create material if needed
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
        
        lineMaterial.SetPass(0);
        
        GL.PushMatrix();
        GL.MultMatrix(meshTransform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(edgeColor);
        
        foreach (var edge in edges)
        {
            GL.Vertex(meshVerts[edge.Item1]);
            GL.Vertex(meshVerts[edge.Item2]);
        }
        
        GL.End();
        GL.PopMatrix();
    }
    
    HashSet<(int, int)> GetMeshEdges()
    {
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        
        if (mf == null || mf.sharedMesh == null)
            return null;
        
        Mesh mesh = mf.sharedMesh;
        int[] triangles = mesh.triangles;
        HashSet<(int, int)> edges = new HashSet<(int, int)>();
        
        // Extract ALL edges from ALL triangles
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            
            // Add all 3 edges of this triangle
            AddEdge(edges, v0, v1);
            AddEdge(edges, v1, v2);
            AddEdge(edges, v2, v0);
        }
        
        return edges;
    }
    
    void AddEdge(HashSet<(int, int)> edges, int v0, int v1)
    {
        // Store in sorted order to avoid duplicates
        if (v0 > v1)
        {
            int temp = v0;
            v0 = v1;
            v1 = temp;
        }
        edges.Add((v0, v1));
    }
    
    private Material lineMaterial;
    
    void OnDestroy()
    {
        if (lineMaterial != null)
            DestroyImmediate(lineMaterial);
    }
}

