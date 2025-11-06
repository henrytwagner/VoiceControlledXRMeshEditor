using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns and manages multiple editable mesh instances
/// Works with TopMenuBar to create primitives on demand
/// </summary>
public class MeshSpawner : MonoBehaviour
{
    [Header("Prefab Setup")]
    public GameObject editableMeshPrefab; // Prefab with EditableMesh component
    
    [Header("Primitive Meshes")]
    public Mesh cubeMesh;
    public Mesh sphereMesh;
    public Mesh cylinderMesh;
    public Mesh capsuleMesh;
    public Mesh planeMesh;
    
    [Header("Default Settings")]
    public Material defaultMaterial;
    public Material vertexMaterial;
    public Material wireframeMaterial;
    
    [Header("Spawn Settings")]
    public Vector3 spawnPosition = Vector3.zero;
    
    [Header("Auto-Spawn on Start")]
    [Tooltip("Automatically spawn a cube when the scene starts")]
    public bool spawnCubeOnStart = false;
    
    private List<EditableMesh> spawnedMeshes = new List<EditableMesh>();
    private int spawnCounter = 0;
    
    void Start()
    {
        // Load default materials if not assigned
        if (defaultMaterial == null)
            defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        if (vertexMaterial == null)
            vertexMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        if (wireframeMaterial == null)
            wireframeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        // Auto-spawn cube if enabled
        if (spawnCubeOnStart)
        {
            SpawnPrimitive(PrimitiveType.Cube);
        }
    }
    
    /// <summary>
    /// Spawn a primitive mesh
    /// </summary>
    public EditableMesh SpawnPrimitive(PrimitiveType primitiveType)
    {
        Mesh mesh = GetPrimitiveMesh(primitiveType);
        if (mesh == null)
        {
            Debug.LogError($"[MeshSpawner] Failed to get mesh for primitive: {primitiveType}");
            return null;
        }
        
        return SpawnMesh(mesh, primitiveType.ToString());
    }
    
    /// <summary>
    /// Spawn a custom mesh
    /// </summary>
    public EditableMesh SpawnMesh(Mesh mesh, string objectName = "EditableMesh")
    {
        GameObject newObj;
        
        if (editableMeshPrefab != null)
        {
            // Use prefab - instantiate at origin with no rotation
            newObj = Instantiate(editableMeshPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            // Create from scratch
            newObj = new GameObject();
            newObj.AddComponent<EditableMesh>();
        }
        
        // Setup object
        spawnCounter++;
        newObj.name = $"{objectName}_{spawnCounter}";
        
        // Force spawn at origin (0, 0, 0) - clear parent and set position
        newObj.transform.SetParent(null);
        newObj.transform.position = spawnPosition;
        newObj.transform.rotation = Quaternion.identity;
        newObj.transform.localScale = Vector3.one;
        
        // Configure EditableMesh
        EditableMesh editableMesh = newObj.GetComponent<EditableMesh>();
        if (editableMesh != null)
        {
            editableMesh.sourceMesh = mesh;
            editableMesh.meshMaterial = defaultMaterial;
            editableMesh.vertexMaterial = vertexMaterial;
            editableMesh.wireframeMaterial = wireframeMaterial;
            editableMesh.RebuildFromSource();
        }
        
        // Track it
        spawnedMeshes.Add(editableMesh);
        
        Debug.Log($"[MeshSpawner] Spawned {objectName} at {newObj.transform.position}");
        
        return editableMesh;
    }
    
    Mesh GetPrimitiveMesh(PrimitiveType primitiveType)
    {
        // Return assigned mesh if available
        switch (primitiveType)
        {
            case PrimitiveType.Cube:
                if (cubeMesh != null) return cubeMesh;
                break;
            case PrimitiveType.Sphere:
                if (sphereMesh != null) return sphereMesh;
                break;
            case PrimitiveType.Cylinder:
                if (cylinderMesh != null) return cylinderMesh;
                break;
            case PrimitiveType.Capsule:
                if (capsuleMesh != null) return capsuleMesh;
                break;
            case PrimitiveType.Plane:
                if (planeMesh != null) return planeMesh;
                break;
        }
        
        // Generate primitive mesh as fallback
        return CreatePrimitiveMesh(primitiveType);
    }
    
    Mesh CreatePrimitiveMesh(PrimitiveType primitiveType)
    {
        // Create temporary primitive GameObject
        GameObject temp = GameObject.CreatePrimitive(primitiveType);
        MeshFilter mf = temp.GetComponent<MeshFilter>();
        Mesh originalMesh = mf.mesh; // Use .mesh (instance) not .sharedMesh
        
        // The instance mesh (.mesh) is readable, sharedMesh is not
        Mesh readableMesh = new Mesh();
        readableMesh.name = $"{primitiveType}_Readable";
        
        // Copy all mesh data
        readableMesh.vertices = originalMesh.vertices;
        readableMesh.triangles = originalMesh.triangles;
        readableMesh.normals = originalMesh.normals;
        readableMesh.uv = originalMesh.uv;
        
        if (originalMesh.tangents != null && originalMesh.tangents.Length > 0)
            readableMesh.tangents = originalMesh.tangents;
        
        readableMesh.RecalculateBounds();
        
        // Clean up temp object
        #if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(temp);
        else
            Destroy(temp);
        #else
        Destroy(temp);
        #endif
        
        return readableMesh;
    }
    
    /// <summary>
    /// Delete a spawned mesh
    /// </summary>
    public void DeleteMesh(EditableMesh mesh)
    {
        if (mesh != null && spawnedMeshes.Contains(mesh))
        {
            spawnedMeshes.Remove(mesh);
            Destroy(mesh.gameObject);
        }
    }
    
    /// <summary>
    /// Delete all spawned meshes
    /// </summary>
    public void ClearAll()
    {
        foreach (EditableMesh mesh in spawnedMeshes)
        {
            if (mesh != null)
                Destroy(mesh.gameObject);
        }
        spawnedMeshes.Clear();
        spawnCounter = 0;
    }
    
    /// <summary>
    /// Get all spawned meshes
    /// </summary>
    public List<EditableMesh> GetAllMeshes()
    {
        // Remove null entries
        spawnedMeshes.RemoveAll(m => m == null);
        return spawnedMeshes;
    }
}

