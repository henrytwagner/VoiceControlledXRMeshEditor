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
    }
    
    /// <summary>
    /// Spawn a primitive mesh
    /// </summary>
    public EditableMesh SpawnPrimitive(PrimitiveType primitiveType)
    {
        Mesh mesh = GetPrimitiveMesh(primitiveType);
        if (mesh == null)
        {
            Debug.LogError($"Failed to get mesh for primitive: {primitiveType}");
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
            // Use prefab
            newObj = Instantiate(editableMeshPrefab);
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
        
        // Always spawn at origin (0, 0, 0)
        newObj.transform.position = spawnPosition;
        
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
        
        Debug.Log($"[MeshSpawner] Spawned {objectName} at {spawnPosition}");
        
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
        // Create temporary primitive GameObject to extract mesh
        GameObject temp = GameObject.CreatePrimitive(primitiveType);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        
        // Create a copy of the mesh
        Mesh meshCopy = new Mesh();
        meshCopy.vertices = mesh.vertices;
        meshCopy.triangles = mesh.triangles;
        meshCopy.normals = mesh.normals;
        meshCopy.uv = mesh.uv;
        meshCopy.name = $"{primitiveType}_Generated";
        
        // Clean up temp object
        Destroy(temp);
        
        return meshCopy;
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

