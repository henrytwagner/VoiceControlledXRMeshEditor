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
    public enum DisplayMode { Mesh, Vertices }
    
    [Header("Mode")]
    public DisplayMode mode = DisplayMode.Mesh;
    
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
        
        bool pressed = false;
        #if ENABLE_INPUT_SYSTEM
        pressed = UnityEngine.InputSystem.Keyboard.current?[UnityEngine.InputSystem.Key.Tab].wasPressedThisFrame ?? false;
        #else
        pressed = Input.GetKeyDown(toggleModeKey);
        #endif
        
        if (pressed)
            ToggleMode();
    }
    
    public void ToggleMode()
    {
        mode = (mode == DisplayMode.Mesh) ? DisplayMode.Vertices : DisplayMode.Mesh;
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
        
        if (vertsRoot == null)
        {
            var t = transform.Find(VertsChildName);
            vertsRoot = t != null ? t : new GameObject(VertsChildName).transform;
            vertsRoot.SetParent(transform, false);
        }
    }
    
    void ApplyModeActiveStates()
    {
        if (mode == DisplayMode.Mesh)
        {
            if (meshRoot) meshRoot.gameObject.SetActive(true);
            if (vertsRoot) vertsRoot.gameObject.SetActive(false);
            if (meshRenderer && meshMaterial)
                meshRenderer.sharedMaterial = meshMaterial;
        }
        else // Vertices
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
    
    public int GetVertexCount()
    {
        return uniqueVertices != null ? uniqueVertices.Length : 0;
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

