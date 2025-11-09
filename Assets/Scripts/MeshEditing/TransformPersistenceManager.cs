using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class TransformData
{
    public string name;
    public float[] position;
    public float[] rotation;
    public float[] scale;
    public List<float[]> vertices = new List<float[]>();

    public TransformData(EditableMesh mesh)
    {
       
        name = mesh.gameObject.name;
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
    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "transforms.json");
    }

    void Start()
    {
        // Auto-load any existing saved transforms when app starts
        LoadTransforms();
    }

    void OnApplicationQuit()
    {
        // Auto-save when app exits or play mode stops
        SaveTransforms();
    }

    public void SaveTransforms()
    {
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        TransformSaveFile saveFile = new TransformSaveFile();

        Debug.Log($"[TransformPersistence] Found {allMeshes.Length} EditableMesh objects: " +
              string.Join(", ", System.Array.ConvertAll(allMeshes, m => m.gameObject.name)));

        foreach (EditableMesh mesh in allMeshes)
        {
            saveFile.objects.Add(new TransformData(mesh));
            Debug.Log($"[TransformPersistence] Captured {mesh.GetVertexCount()} vertices for {mesh.name}");
        }

        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(savePath, json);

        Debug.Log($"[TransformPersistence] Saved {saveFile.objects.Count} objects to {savePath}");
    }

    public void LoadTransforms()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("[TransformPersistence] No save file found.");
            return;
        }

        string json = File.ReadAllText(savePath);
        TransformSaveFile saveFile = JsonUtility.FromJson<TransformSaveFile>(json);

        foreach (TransformData data in saveFile.objects)
        {
            GameObject obj = GameObject.Find(data.name);
            if (obj == null)
            {
                Debug.LogWarning($"[TransformPersistence] Object {data.name} not found in scene, skipping.");
                continue;
            }

            obj.transform.position = new Vector3(data.position[0], data.position[1], data.position[2]);
            obj.transform.rotation = Quaternion.Euler(data.rotation[0], data.rotation[1], data.rotation[2]);
            obj.transform.localScale = new Vector3(data.scale[0], data.scale[1], data.scale[2]);

            EditableMesh mesh = obj.GetComponent<EditableMesh>();
            if (mesh != null && data.vertices != null && data.vertices.Count == mesh.GetVertexCount())
            {
                for (int i = 0; i < data.vertices.Count; i++)
                {
                    var v = data.vertices[i];
                    mesh.SetVertex(i, new Vector3(v[0], v[1], v[2]));
                }
                Debug.Log($"[TransformPersistence] Restored {data.vertices.Count} vertices for {data.name}");
            }
            else if (mesh != null)
            {
                Debug.LogWarning($"[TransformPersistence] Vertex count mismatch for {data.name}: " +
                                 $"{data.vertices?.Count ?? 0} saved vs {mesh.GetVertexCount()} current.");
            }
        }

        Debug.Log($"[TransformPersistence] Loaded {saveFile.objects.Count} objects from {savePath}");
    }
}
