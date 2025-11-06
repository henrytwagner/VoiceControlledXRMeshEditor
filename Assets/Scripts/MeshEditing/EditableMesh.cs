using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generic editable mesh system that works with any imported mesh (OBJ, FBX, etc.)
/// Extracts unique vertices and allows runtime/editor manipulation.
/// More flexible than EditableCube - works with any topology.
/// </summary>
[ExecuteAlways]
public class EditableMesh : MonoBehaviour
{
    public enum DisplayMode { Object, Edit }
    
    [Header("Mode")]
    public DisplayMode mode = DisplayMode.Object;
    
    [Header("Source Mesh")]
    [Tooltip("The imported mesh to make editable (drag from Project)")]
    public Mesh sourceMesh;
    
    [Tooltip("Merge vertices within this distance (for cleaner editing)")]
    public float vertexMergeThreshold = 0.001f;
    
    [Header("Materials")]
    public Material meshMaterial;
    public Material vertexMaterial;
    public Material wireframeMaterial;
    
    [Header("Vertex Spheres")]
    [Min(0.0001f)] public float sphereRadius = 0.02f;
    public bool removeSphereColliders = true;
    public bool showWireframeInVertexMode = true;
    
    [Header("Hotkeys (Play Mode)")]
    public bool enableHotkey = true;
    public KeyCode toggleModeKey = KeyCode.Tab;
    public bool requireSelectionToEdit = true; // Only toggle mode when selected
    
    [Header("Origin Indicator")]
    public bool showOriginInObjectMode = true;
    public float originDotSize = 6f; // Pixels
    public Color originColorSelected = new Color(1f, 0.5f, 0f, 1f); // Orange when selected
    public bool showOriginInGameView = true;
    public bool showOriginInSceneView = true;
    
    // Internal
    const string MeshChildName = "_EditableMesh";
    const string VertsChildName = "_MeshVertices";
    
    Transform meshRoot, vertsRoot;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh editableMesh;
    
    // Unique vertex positions and their mapping to mesh indices
    [SerializeField] private Vector3[] uniqueVertices;
    [SerializeField] private int[][] vertexToMeshIndices; // Each unique vertex maps to multiple mesh indices
    
    void Reset()
    {
        #if UNITY_EDITOR
        // Try to find Cube.obj automatically in editor
        if (sourceMesh == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Cube t:Mesh", new[] { "Assets/Meshes" });
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                sourceMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Debug.Log($"[EditableMesh] Auto-loaded mesh: {path}");
            }
        }
        #endif
        
        if (meshMaterial == null)
        {
            meshMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            meshMaterial.name = "EditableMesh_Material";
        }
        
        if (sourceMesh != null)
            RebuildFromSource();
    }
    
    void OnEnable()
    {
        EnsureChildren();
        if (editableMesh == null && sourceMesh != null)
            RebuildFromSource();
        ApplyModeActiveStates();
        
        // Force ensure collider exists
        EnsureMeshCollider();
    }
    
    void EnsureMeshCollider()
    {
        if (meshFilter != null)
        {
            var meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = false;
                if (editableMesh != null)
                {
                    meshCollider.sharedMesh = editableMesh;
                    Debug.Log($"[EditableMesh] Added MeshCollider - Mesh: {editableMesh.name}, Vertices: {editableMesh.vertexCount}");
                }
                else
                {
                    Debug.LogWarning("[EditableMesh] MeshCollider added but no mesh to assign yet!");
                }
            }
            else
            {
                // Collider exists, make sure it has the mesh
                if (meshCollider.sharedMesh == null && editableMesh != null)
                {
                    meshCollider.sharedMesh = editableMesh;
                    Debug.Log($"[EditableMesh] Updated existing MeshCollider with mesh: {editableMesh.name}");
                }
            }
        }
    }
    
    void OnValidate()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                EnsureChildren();
                if (sourceMesh != null)
                    RebuildFromSource();
            }
        };
        #endif
    }
    
    void Update()
    {
        if (!Application.isPlaying || !enableHotkey)
            return;
        
        // Check if Tab is pressed
        bool pressed = false;
        #if ENABLE_INPUT_SYSTEM
        pressed = UnityEngine.InputSystem.Keyboard.current?[UnityEngine.InputSystem.Key.Tab].wasPressedThisFrame ?? false;
        #else
        pressed = Input.GetKeyDown(toggleModeKey);
        #endif
        
        if (pressed)
        {
            // Only toggle if this mesh is selected (or selection requirement is disabled)
            if (!requireSelectionToEdit || IsThisObjectSelected())
            {
                ToggleMode();
            }
        }
    }
    
    bool IsThisObjectSelected()
    {
        // Check if this mesh is currently selected by ObjectSelector
        ObjectSelector selector = FindAnyObjectByType<ObjectSelector>();
        if (selector != null)
        {
            Transform selected = selector.GetCurrentSelection();
            return selected == transform || selected == transform.parent;
        }
        
        // If no selector, allow toggle (backward compatibility)
        return true;
    }
    
    public void ToggleMode()
    {
        mode = (mode == DisplayMode.Object) ? DisplayMode.Edit : DisplayMode.Object;
        ApplyModeActiveStates();
    }
    
    // ——————————————————— Build / Update ———————————————————
    
    void EnsureChildren()
    {
        if (meshRoot == null)
        {
            var t = transform.Find(MeshChildName);
            meshRoot = t != null ? t : new GameObject(MeshChildName).transform;
            meshRoot.SetParent(transform, false);
        }
        
        if (meshFilter == null || meshRenderer == null)
        {
            var existingHolder = meshRoot.Find("MeshHolder");
            if (existingHolder != null)
            {
                meshFilter = existingHolder.GetComponent<MeshFilter>();
                meshRenderer = existingHolder.GetComponent<MeshRenderer>();
            }
            
            if (meshFilter == null || meshRenderer == null)
            {
                var holder = new GameObject("MeshHolder");
                holder.transform.SetParent(meshRoot, false);
                meshFilter = holder.AddComponent<MeshFilter>();
                meshRenderer = holder.AddComponent<MeshRenderer>();
            }
        }
        
        // Ensure MeshCollider exists
        EnsureMeshCollider();
        
        if (vertsRoot == null)
        {
            var t = transform.Find(VertsChildName);
            vertsRoot = t != null ? t : new GameObject(VertsChildName).transform;
            vertsRoot.SetParent(transform, false);
        }
    }
    
    public void ApplyModeActiveStates()
    {
        if (mode == DisplayMode.Object)
        {
            if (meshRoot) meshRoot.gameObject.SetActive(true);
            if (vertsRoot) vertsRoot.gameObject.SetActive(false);
            if (meshRenderer && meshMaterial)
                meshRenderer.sharedMaterial = meshMaterial;
        }
        else // Edit mode
        {
            if (vertsRoot) vertsRoot.gameObject.SetActive(true);
            
            if (showWireframeInVertexMode)
            {
                if (meshRoot) meshRoot.gameObject.SetActive(true);
                if (meshRenderer)
                {
                    meshRenderer.sharedMaterial = wireframeMaterial != null 
                        ? wireframeMaterial 
                        : meshMaterial;
                }
            }
            else
            {
                if (meshRoot) meshRoot.gameObject.SetActive(false);
            }
        }
    }
    
    public void RebuildFromSource()
    {
        if (sourceMesh == null)
        {
            Debug.LogWarning("[EditableMesh] No source mesh assigned!");
            return;
        }
        
        ExtractUniqueVertices();
        BuildEditableMesh();
        BuildVertexSpheres();
        ApplyModeActiveStates();
    }
    
    void ExtractUniqueVertices()
    {
        Vector3[] sourceVerts = sourceMesh.vertices;
        
        // Merge vertices that are very close together
        List<Vector3> unique = new List<Vector3>();
        List<List<int>> indexMapping = new List<List<int>>();
        
        for (int i = 0; i < sourceVerts.Length; i++)
        {
            Vector3 v = sourceVerts[i];
            
            // Check if this vertex is close to an existing unique vertex
            int foundIndex = -1;
            for (int j = 0; j < unique.Count; j++)
            {
                if (Vector3.Distance(v, unique[j]) < vertexMergeThreshold)
                {
                    foundIndex = j;
                    break;
                }
            }
            
            if (foundIndex >= 0)
            {
                // Add to existing unique vertex's index list
                indexMapping[foundIndex].Add(i);
            }
            else
            {
                // New unique vertex
                unique.Add(v);
                indexMapping.Add(new List<int> { i });
            }
        }
        
        uniqueVertices = unique.ToArray();
        vertexToMeshIndices = indexMapping.Select(list => list.ToArray()).ToArray();
        
        Debug.Log($"[EditableMesh] Extracted {uniqueVertices.Length} unique vertices from {sourceVerts.Length} total vertices");
    }
    
    void BuildEditableMesh()
    {
        if (editableMesh == null)
        {
            editableMesh = new Mesh();
            editableMesh.name = "EditableMesh_Runtime";
            meshFilter.sharedMesh = editableMesh;
            if (meshRenderer) meshRenderer.sharedMaterial = meshMaterial;
        }
        
        // Copy mesh data from source
        editableMesh.Clear();
        editableMesh.vertices = sourceMesh.vertices;
        editableMesh.triangles = sourceMesh.triangles;
        editableMesh.normals = sourceMesh.normals;
        editableMesh.uv = sourceMesh.uv;
        editableMesh.RecalculateBounds();
        
        // Update collider mesh for selection
        var meshCollider = meshFilter.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null; // Clear first
            meshCollider.sharedMesh = editableMesh; // Then reassign
        }
    }
    
    void BuildVertexSpheres()
    {
        if (vertsRoot == null || uniqueVertices == null)
            return;
        
        // Clear old spheres
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < vertsRoot.childCount; i++)
        {
            toDestroy.Add(vertsRoot.GetChild(i).gameObject);
        }
        
        foreach (GameObject obj in toDestroy)
        {
            #if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
            #else
            Destroy(obj);
            #endif
        }
        
        // Create sphere for each unique vertex
        for (int i = 0; i < uniqueVertices.Length; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = $"vertex_{i}";
            g.transform.SetParent(vertsRoot, false);
            g.transform.localPosition = uniqueVertices[i];
            float d = sphereRadius * 2f;
            g.transform.localScale = new Vector3(d, d, d);
            
            var r = g.GetComponent<MeshRenderer>();
            if (vertexMaterial != null) r.sharedMaterial = vertexMaterial;
            
            if (removeSphereColliders)
            {
                var c = g.GetComponent<Collider>();
                if (c)
                {
                    #if UNITY_EDITOR
                    if (Application.isPlaying)
                        Destroy(c);
                    else
                        DestroyImmediate(c);
                    #else
                    Destroy(c);
                    #endif
                }
            }
        }
    }
    
    // ——————————————————— Public API ———————————————————
    
    public Vector3[] GetVertices()
    {
        return uniqueVertices != null ? (Vector3[])uniqueVertices.Clone() : new Vector3[0];
    }
    
    public Vector3 GetVertex(int index)
    {
        if (uniqueVertices == null || index < 0 || index >= uniqueVertices.Length)
            return Vector3.zero;
        
        return uniqueVertices[index];
    }
    
    public int GetVertexCount()
    {
        return uniqueVertices != null ? uniqueVertices.Length : 0;
    }
    
    [ContextMenu("Debug: Check Collider Status")]
    void DebugColliderStatus()
    {
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter is NULL!");
            return;
        }
        
        var meshCollider = meshFilter.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogError("NO MeshCollider found! Will add one now...");
            EnsureMeshCollider();
        }
        else
        {
            Debug.Log($"✓ MeshCollider EXISTS on {meshFilter.gameObject.name}");
            Debug.Log($"  - Mesh assigned: {(meshCollider.sharedMesh != null ? meshCollider.sharedMesh.name : "NULL")}");
            Debug.Log($"  - Vertex count: {(meshCollider.sharedMesh != null ? meshCollider.sharedMesh.vertexCount : 0)}");
            Debug.Log($"  - Convex: {meshCollider.convex}");
            Debug.Log($"  - Enabled: {meshCollider.enabled}");
            Debug.Log($"  - GameObject: {meshCollider.gameObject.name}");
            Debug.Log($"  - Full path: {GetFullPath(meshCollider.transform)}");
            Debug.Log($"  - Layer: {LayerMask.LayerToName(meshCollider.gameObject.layer)}");
            Debug.Log($"  - World Position: {meshCollider.transform.position}");
            Debug.Log($"  - Bounds: {meshCollider.bounds}");
        }
    }
    
    string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    // Draw origin indicator
    void OnDrawGizmos()
    {
        if (showOriginInObjectMode && showOriginInSceneView && mode == DisplayMode.Object)
        {
            DrawOriginGizmoScene();
        }
    }
    
    void OnGUI()
    {
        if (showOriginInObjectMode && showOriginInGameView && mode == DisplayMode.Object)
        {
            DrawOriginDot();
        }
    }
    
    void DrawOriginDot()
    {
        // Find active camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera c in cameras)
            {
                if (c.enabled && c.gameObject.activeInHierarchy)
                {
                    cam = c;
                    break;
                }
            }
        }
        
        if (cam == null)
            return;
        
        // Convert origin to screen position
        Vector3 worldOrigin = transform.position;
        Vector3 screenPos = cam.WorldToScreenPoint(worldOrigin);
        
        // Only draw if in front of camera (z > 0)
        if (screenPos.z < 0)
            return;
        
        // Check if this object is selected
        bool isSelected = IsThisObjectSelected();
        Color dotColor = isSelected ? originColorSelected : originColor;
        
        // GUI coordinates: (0,0) is top-left, Y increases downward
        // WorldToScreenPoint: (0,0) is bottom-left, Y increases upward
        // So we flip Y
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        
        // Draw circle/dot - always visible
        Texture2D dot = Texture2D.whiteTexture;
        float halfSize = originDotSize / 2f;
        
        // Draw multiple circles for a nice visible dot
        // Outer glow
        GUI.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0.3f);
        GUI.DrawTexture(new Rect(guiPos.x - halfSize - 3, guiPos.y - halfSize - 3, 
                                originDotSize + 6, originDotSize + 6), dot);
        
        // Middle ring (darker border)
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(guiPos.x - halfSize - 1, guiPos.y - halfSize - 1, 
                                originDotSize + 2, originDotSize + 2), dot);
        
        // Inner circle (colored)
        GUI.color = dotColor;
        GUI.DrawTexture(new Rect(guiPos.x - halfSize, guiPos.y - halfSize, 
                                originDotSize, originDotSize), dot);
        
        // Center highlight (brighter when selected)
        GUI.color = isSelected ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(guiPos.x - 1, guiPos.y - 1, 2, 2), dot);
        
        GUI.color = Color.white;
    }
    
    void DrawOriginGizmoScene()
    {
        Vector3 origin = transform.position;
        float size = 0.05f;
        
        // Check if this object is selected
        bool isSelected = IsThisObjectSelected();
        Color gizmoColor = isSelected ? originColorSelected : originColor;
        
        // Simple cross hair in Scene view
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(origin - transform.right * size, origin + transform.right * size);
        Gizmos.DrawLine(origin - transform.up * size, origin + transform.up * size);
        Gizmos.DrawLine(origin - transform.forward * size, origin + transform.forward * size);
        Gizmos.DrawSphere(origin, size * 0.3f);
    }
    
    public void SetVertex(int index, Vector3 localPos)
    {
        if (uniqueVertices == null || index < 0 || index >= uniqueVertices.Length)
            return;
        
        uniqueVertices[index] = localPos;
        
        // Update all mesh vertices that reference this unique vertex
        if (editableMesh != null && vertexToMeshIndices != null && index < vertexToMeshIndices.Length)
        {
            Vector3[] verts = editableMesh.vertices;
            foreach (int meshIndex in vertexToMeshIndices[index])
            {
                if (meshIndex < verts.Length)
                    verts[meshIndex] = localPos;
            }
            editableMesh.vertices = verts;
            editableMesh.RecalculateNormals();
            editableMesh.RecalculateBounds();
            
            // Update collider mesh for selection
            var meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // Clear first
                meshCollider.sharedMesh = editableMesh; // Then reassign
            }
        }
        
        // Update sphere position
        if (vertsRoot != null)
        {
            Transform sphere = vertsRoot.Find($"vertex_{index}");
            if (sphere != null)
                sphere.localPosition = localPos;
        }
    }
    
    public void ResetToSource()
    {
        if (sourceMesh != null)
        {
            RebuildFromSource();
            Debug.Log("[EditableMesh] Reset to source mesh");
        }
    }
}

