using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Serializable]
public class TransformData
{
    public string name;
    public string primitiveType;
    public string sourceMeshName;
    public float[] position;
    public float[] rotation;
    public float[] scale;
    public List<float[]> vertices = new List<float[]>();

    public TransformData(EditableMesh mesh, string primitiveType, string sourceMeshName)
    {
        name = mesh.gameObject.name;
        this.primitiveType = primitiveType;
        this.sourceMeshName = sourceMeshName;

        Transform t = mesh.transform;
        position = new float[] { t.position.x, t.position.y, t.position.z };
        rotation = new float[] { t.rotation.eulerAngles.x, t.rotation.eulerAngles.y, t.rotation.eulerAngles.z };
        scale = new float[] { t.localScale.x, t.localScale.y, t.localScale.z };

        Vector3[] verts = mesh.GetVertices();
        foreach (Vector3 v in verts)
            vertices.Add(new float[] { v.x, v.y, v.z });
    }
}

[System.Serializable]
public class TransformSaveFile
{
    public List<TransformData> objects = new List<TransformData>();
}

public class TransformPersistenceManager : MonoBehaviour
{
    private string savesDirectory;
    private string autosavePath;
    private MeshSpawner meshSpawner;
    private string currentSceneName;

    void Awake()
    {
        savesDirectory = Path.Combine(Application.persistentDataPath, "SavedScenes");
        if (!Directory.Exists(savesDirectory))
            Directory.CreateDirectory(savesDirectory);

        autosavePath = Path.Combine(savesDirectory, "autosave.json");

        string legacyPath = Path.Combine(Application.persistentDataPath, "transforms.json");
        if (File.Exists(legacyPath) && !File.Exists(autosavePath))
        {
            File.Copy(legacyPath, autosavePath, true);
            File.Delete(legacyPath);
            Debug.Log($"[TransformPersistence] Migrated legacy save file to {autosavePath}");
        }

        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();

        currentSceneName = null;
    }

    void Start()
    {
        // No automatic load on start.
    }

    void OnApplicationQuit()
    {
        // Auto-save when app exits or play mode stops
        SaveTransforms();
    }

    public void SaveTransforms()
    {
        if (!string.IsNullOrEmpty(currentSceneName))
            SaveTransformsToPath(Path.Combine(savesDirectory, currentSceneName + ".json"));
        else
            SaveTransformsToPath(autosavePath);
    }

    public void SaveTransforms(string sceneName)
    {
        string sanitized = SanitizeFileName(sceneName);
        if (string.IsNullOrEmpty(sanitized))
            sanitized = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentSceneName = sanitized;
        string path = Path.Combine(savesDirectory, sanitized + ".json");
        SaveTransformsToPath(path);
        Debug.Log($"[TransformPersistence] Scene saved as '{sanitized}'");
    }

    void SaveTransformsToPath(string path)
    {
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        TransformSaveFile saveFile = new TransformSaveFile();

        Debug.Log($"[TransformPersistence] Found {allMeshes.Length} EditableMesh objects: " +
              string.Join(", ", System.Array.ConvertAll(allMeshes, m => m.gameObject.name)));

        foreach (EditableMesh mesh in allMeshes)
        {
            string primitiveType = DeterminePrimitiveType(mesh);
            string sourceMeshName = mesh.sourceMesh != null ? mesh.sourceMesh.name : null;
            saveFile.objects.Add(new TransformData(mesh, primitiveType, sourceMeshName));
            Debug.Log($"[TransformPersistence] Captured {mesh.GetVertexCount()} vertices for {mesh.name}");
        }

        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(path, json);

        Debug.Log($"[TransformPersistence] Saved {saveFile.objects.Count} objects to {path}");
    }

    public void LoadTransforms()
    {
        currentSceneName = null;
        LoadTransformsFromPath(autosavePath);
    }

    public void LoadTransformsFromFile(string sceneName)
    {
        string sanitized = SanitizeFileName(sceneName);
        string path = Path.Combine(savesDirectory, sanitized + ".json");
        currentSceneName = sanitized;
        LoadTransformsFromPath(path);
    }

    void LoadTransformsFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Debug.Log($"[TransformPersistence] Save file not found at {path}");
            return;
        }

        string json = File.ReadAllText(path);
        TransformSaveFile saveFile = JsonUtility.FromJson<TransformSaveFile>(json);

        // Clear current meshes first
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();

        List<EditableMesh> existingMeshes = new List<EditableMesh>(FindObjectsByType<EditableMesh>(FindObjectsSortMode.None));
        foreach (EditableMesh mesh in existingMeshes)
        {
            if (mesh != null)
                Destroy(mesh.gameObject);
        }

        foreach (TransformData data in saveFile.objects)
        {
            EditableMesh mesh = TryRecreateObject(data);
            if (mesh == null)
            {
                Debug.LogWarning($"[TransformPersistence] Failed to recreate object {data.name}, skipping.");
                continue;
            }

            GameObject obj = mesh.gameObject;
            obj.transform.position = new Vector3(data.position[0], data.position[1], data.position[2]);
            obj.transform.rotation = Quaternion.Euler(data.rotation[0], data.rotation[1], data.rotation[2]);
            obj.transform.localScale = new Vector3(data.scale[0], data.scale[1], data.scale[2]);

            if (data.vertices != null && data.vertices.Count == mesh.GetVertexCount())
            {
                for (int i = 0; i < data.vertices.Count; i++)
                {
                    var v = data.vertices[i];
                    mesh.SetVertex(i, new Vector3(v[0], v[1], v[2]));
                }
                Debug.Log($"[TransformPersistence] Restored {data.vertices.Count} vertices for {data.name}");
            }
            else
            {
                Debug.LogWarning($"[TransformPersistence] Vertex count mismatch for {data.name}: " +
                                 $"{data.vertices?.Count ?? 0} saved vs {mesh.GetVertexCount()} current.");
            }
        }

        Debug.Log($"[TransformPersistence] Loaded {saveFile.objects.Count} objects from {path}");

        if (path == autosavePath)
            currentSceneName = null;
        else
            currentSceneName = Path.GetFileNameWithoutExtension(path);
    }

    public List<string> GetSavedSceneNames(bool includeAutosave = false)
    {
        if (!Directory.Exists(savesDirectory))
            return new List<string>();

        IEnumerable<string> files = Directory.GetFiles(savesDirectory, "*.json");
        if (!includeAutosave)
            files = files.Where(f => !string.Equals(Path.GetFileName(f), Path.GetFileName(autosavePath)));

        return files
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(f => f)
            .ToList();
    }

    string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");
        return name.Trim();
    }

    public string SavesDirectory => savesDirectory;
    public string CurrentSceneName => currentSceneName;

    EditableMesh TryRecreateObject(TransformData data)
    {
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();

        EditableMesh newMesh = null;

        if (!string.IsNullOrEmpty(data.primitiveType) && System.Enum.TryParse(data.primitiveType, out PrimitiveType primitive))
        {
            newMesh = meshSpawner.SpawnPrimitive(primitive);
        }

        if (newMesh == null)
        {
            Mesh baseMesh = ResolveMeshByName(data.sourceMeshName);
            if (baseMesh != null)
                newMesh = meshSpawner.SpawnMesh(baseMesh, data.name);
        }

        if (newMesh != null)
        {
            newMesh.gameObject.name = data.name;
            return newMesh;
        }

        return null;
    }

    Mesh ResolveMeshByName(string meshName)
    {
        if (string.IsNullOrEmpty(meshName) || meshSpawner == null)
            return null;

        if (meshSpawner.cubeMesh != null && meshSpawner.cubeMesh.name == meshName) return meshSpawner.cubeMesh;
        if (meshSpawner.sphereMesh != null && meshSpawner.sphereMesh.name == meshName) return meshSpawner.sphereMesh;
        if (meshSpawner.cylinderMesh != null && meshSpawner.cylinderMesh.name == meshName) return meshSpawner.cylinderMesh;
        if (meshSpawner.capsuleMesh != null && meshSpawner.capsuleMesh.name == meshName) return meshSpawner.capsuleMesh;
        if (meshSpawner.planeMesh != null && meshSpawner.planeMesh.name == meshName) return meshSpawner.planeMesh;

        return null;
    }

    string DeterminePrimitiveType(EditableMesh mesh)
    {
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();

        if (meshSpawner != null)
        {
            Mesh source = mesh.sourceMesh;
            if (source != null)
            {
                if (source == meshSpawner.cubeMesh) return PrimitiveType.Cube.ToString();
                if (source == meshSpawner.sphereMesh) return PrimitiveType.Sphere.ToString();
                if (source == meshSpawner.cylinderMesh) return PrimitiveType.Cylinder.ToString();
                if (source == meshSpawner.capsuleMesh) return PrimitiveType.Capsule.ToString();
                if (source == meshSpawner.planeMesh) return PrimitiveType.Plane.ToString();

                string name = source.name;
                if (name.Contains("Cube", System.StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Cube.ToString();
                if (name.Contains("Sphere", System.StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Sphere.ToString();
                if (name.Contains("Cylinder", System.StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Cylinder.ToString();
                if (name.Contains("Capsule", System.StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Capsule.ToString();
                if (name.Contains("Plane", System.StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Plane.ToString();
            }
        }

        return null;
    }
}
