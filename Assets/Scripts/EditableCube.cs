using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class EditableCube : MonoBehaviour
{
    public enum DisplayMode { Mesh, Vertices }

    [Header("Mode")]
    public DisplayMode mode = DisplayMode.Mesh;

    [Header("Size / Materials")]
    [Min(0.0001f)] public float size = 1f;
    public Material meshMaterial;
    public Material vertexMaterial;

    [Header("Vertex Spheres")]
    [Min(0.0001f)] public float sphereRadius = 0.015f;
    public bool removeSphereColliders = true;
    public bool showWireframeInVertexMode = true;  // ← ADD THIS
    public Material wireframeMaterial;              // ← ADD THIS (optional, for custom wireframe look)

    [Header("Hotkeys (Play Mode)")]
    public bool enableHotkey = true;
    public KeyCode legacyToggleKey = KeyCode.Tab;

    // Internal
    const string MeshChildName = "_CubeMesh";
    const string VertsChildName = "_CubeVertices";

    Transform meshRoot, vertsRoot;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    // We keep 8 logical corners that drive the 24 actual mesh verts (hard edges)
    // Corner index layout (like binary xyz: x∈{-1,+1}, etc.)
    // 0: (-,-,-) 1: (+,-,-) 2: (-,+,-) 3: (+,+,-) 4: (-,-,+) 5: (+,-,+) 6: (-,+,+) 7: (+,+,+)
    [SerializeField] // This makes corners save with the scene/prefab
    private Vector3[] corners = new Vector3[8];

    // For hard edges we duplicate vertices per face. This maps each corner to the 3 (or fewer) vertex slots per faces that share it.
    // face order: -X, +X, -Y, +Y, -Z, +Z (each 4 verts => 24 total indices)
    // We’ll build once and then use these lists to “fan out” corner edits to mesh.vertices.
    int[][] cornerToMeshIndices;

    void Reset()
    {
        if (meshMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "EditableCube_MeshMat";
            meshMaterial = m;
        }
        if (vertexMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "EditableCube_VertexMat";
            vertexMaterial = m;
        }
        InitializeCornersFromSize();
        RebuildAll();
    }

    void OnEnable()
    {
        EnsureChildren();
        if (mesh == null) InitializeCornersFromSize();
        RebuildAll();
    }

    void OnValidate()
    {
        EnsureChildren();
        // If size changed in Inspector, re-seed cube from size while preserving center.
        InitializeCornersFromSize();
        RebuildAll();
    }

    void Update()
    {
        if (!enableHotkey) return;

        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) pressed = true;
#else
        if (Input.GetKeyDown(legacyToggleKey)) pressed = true;
#endif
        if (pressed) ToggleMode();
    }

    public void ToggleMode()
    {
        mode = (mode == DisplayMode.Mesh) ? DisplayMode.Vertices : DisplayMode.Mesh;
        ApplyModeActiveStates();
    }

    public void SetMode(DisplayMode m)
    {
        mode = m;
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
        
        // Only create MeshHolder if we don't have meshFilter/meshRenderer yet
        if (meshFilter == null || meshRenderer == null)
        {
            // Try to find existing MeshHolder first
            var existingHolder = meshRoot.Find("MeshHolder");
            if (existingHolder != null)
            {
                meshFilter = existingHolder.GetComponent<MeshFilter>();
                meshRenderer = existingHolder.GetComponent<MeshRenderer>();
            }
            
            // If still null, create new MeshHolder
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
            // Mesh mode: show mesh, hide vertices
            if (meshRoot)  meshRoot.gameObject.SetActive(true);
            if (vertsRoot) vertsRoot.gameObject.SetActive(false);
            
            // Use normal mesh material
            if (meshRenderer && meshMaterial) 
                meshRenderer.sharedMaterial = meshMaterial;
        }
        else // DisplayMode.Vertices
        {
            // Vertex mode: always show vertices
            if (vertsRoot) vertsRoot.gameObject.SetActive(true);
            
            if (showWireframeInVertexMode)
            {
                // Show mesh as wireframe overlay
                if (meshRoot) meshRoot.gameObject.SetActive(true);
                
                // Use wireframe material if available, otherwise use mesh material
                if (meshRenderer)
                {
                    meshRenderer.sharedMaterial = wireframeMaterial != null 
                        ? wireframeMaterial 
                        : meshMaterial;
                }
            }
            else
            {
                // Hide mesh completely
                if (meshRoot) meshRoot.gameObject.SetActive(false);
            }
        }
    }   

    void InitializeCornersFromSize()
    {
        float h = size * 0.5f;
        corners[0] = new Vector3(-h, -h, -h);
        corners[1] = new Vector3(+h, -h, -h);
        corners[2] = new Vector3(-h, +h, -h);
        corners[3] = new Vector3(+h, +h, -h);
        corners[4] = new Vector3(-h, -h, +h);
        corners[5] = new Vector3(+h, -h, +h);
        corners[6] = new Vector3(-h, +h, +h);
        corners[7] = new Vector3(+h, +h, +h);
    }

    public void RebuildAll()
    {
        BuildMesh();         // creates/updates mesh with 24 verts (hard edges)
        BuildVertexSpheres(); // places 8 spheres at logical corners
        ApplyModeActiveStates();
    }

    void BuildMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "EditableCubeMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            meshFilter.sharedMesh = mesh;
            if (meshRenderer) meshRenderer.sharedMaterial = meshMaterial;
        }

        // Build the 24-vertex cube from corners (hard edges = correct normals per face)
        // Face order: -X, +X, -Y, +Y, -Z, +Z. Each face: quad -> 2 triangles.
        // For each face, define 4 vertex positions by selecting the right corners.

        // Corner aliases for readability
        var c0 = corners[0]; var c1 = corners[1]; var c2 = corners[2]; var c3 = corners[3];
        var c4 = corners[4]; var c5 = corners[5]; var c6 = corners[6]; var c7 = corners[7];

        Vector3[] v = new Vector3[24];
        Vector3[] n = new Vector3[24];
        Vector2[] uv = new Vector2[24];

        int vi = 0;

        // -X face (looking at -X side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(-1,0,0), c0, c4, c6, c2);

        // +X face (looking at +X side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(+1,0,0), c1, c3, c7, c5);

        // -Y face (looking at -Y side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(0,-1,0), c0, c1, c5, c4);

        // +Y face (looking at +Y side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(0,+1,0), c2, c6, c7, c3);

        // -Z face (looking at -Z side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(0,0,-1), c0, c2, c3, c1);

        // +Z face (looking at +Z side from outside, counter-clockwise)
        AddFace(ref vi, new Vector3(0,0,+1), c4, c5, c7, c6);

        // Local function writes face data (populates v/n/uv)
        void AddFace(ref int baseIndex, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Vertex order (a,b,c,d): a—b—c—d in UV: (0,0)(1,0)(1,1)(0,1)
            v[baseIndex + 0] = a; n[baseIndex + 0] = normal; uv[baseIndex + 0] = new Vector2(0, 0);
            v[baseIndex + 1] = b; n[baseIndex + 1] = normal; uv[baseIndex + 1] = new Vector2(1, 0);
            v[baseIndex + 2] = c; n[baseIndex + 2] = normal; uv[baseIndex + 2] = new Vector2(1, 1);
            v[baseIndex + 3] = d; n[baseIndex + 3] = normal; uv[baseIndex + 3] = new Vector2(0, 1);
            baseIndex += 4;
        }

        // Triangles (two per face): Standard winding order
        int[] tris = new int[36];
        int ti = 0;
        for (int f = 0; f < 6; f++)
        {
            int o = f * 4;
            tris[ti++] = o + 0; tris[ti++] = o + 1; tris[ti++] = o + 2;
            tris[ti++] = o + 0; tris[ti++] = o + 2; tris[ti++] = o + 3;
        }

        mesh.Clear();
        mesh.vertices = v;
        mesh.normals = n; // normals are face-constant -> crisp edges
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        // Build/update mapping from logical corners -> mesh vertex indices that use them
        BuildCornerMapping();
    }

    void BuildCornerMapping()
    {
        // We know exactly how faces were constructed; enumerate which of the 24 slots correspond to which logical corner.
        // Order we added faces (each block of 4) - UPDATED for correct winding:
        // -X: [0..3] = c0,c4,c6,c2
        // +X: [4..7] = c1,c3,c7,c5
        // -Y: [8..11] = c0,c1,c5,c4
        // +Y: [12..15] = c2,c6,c7,c3
        // -Z: [16..19] = c0,c2,c3,c1
        // +Z: [20..23] = c4,c5,c7,c6
        cornerToMeshIndices = new int[8][];
        cornerToMeshIndices[0] = new[] { 0, 8, 16 };   // c0: -X[0], -Y[0], -Z[0]
        cornerToMeshIndices[1] = new[] { 4, 9, 19 };   // c1: +X[0], -Y[1], -Z[3]
        cornerToMeshIndices[2] = new[] { 3, 12, 17 };  // c2: -X[3], +Y[0], -Z[1]
        cornerToMeshIndices[3] = new[] { 5, 15, 18 };  // c3: +X[1], +Y[3], -Z[2]
        cornerToMeshIndices[4] = new[] { 1, 11, 20 };  // c4: -X[1], -Y[3], +Z[0]
        cornerToMeshIndices[5] = new[] { 7, 10, 21 };  // c5: +X[3], -Y[2], +Z[1]
        cornerToMeshIndices[6] = new[] { 2, 13, 23 };  // c6: -X[2], +Y[1], +Z[3]
        cornerToMeshIndices[7] = new[] { 6, 14, 22 };  // c7: +X[2], +Y[2], +Z[2]
    }

    void BuildVertexSpheres()
    {
        // Clear old spheres
        for (int i = vertsRoot.childCount - 1; i >= 0; --i)
            SafeDestroy(vertsRoot.GetChild(i).gameObject);

        for (int i = 0; i < 8; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = $"corner_{i}";
            g.transform.SetParent(vertsRoot, false);
            g.transform.localPosition = corners[i];
            float d = sphereRadius * 2f;
            g.transform.localScale = new Vector3(d, d, d);

            var r = g.GetComponent<MeshRenderer>();
            if (vertexMaterial != null) r.sharedMaterial = vertexMaterial;

            if (removeSphereColliders)
            {
                var c = g.GetComponent<Collider>();
                if (c) SafeDestroy(c);
            }
        }
    }

    static void SafeDestroy(Object o)
    {
        if (Application.isPlaying) 
        {
            Object.Destroy(o);
        }
        else 
        {
            #if UNITY_EDITOR
            // During OnValidate or other callbacks, defer the destruction
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (o != null) Object.DestroyImmediate(o);
            };
            #else
            Object.DestroyImmediate(o);
            #endif
        }
    }

    // ——————————————————— Editing API ———————————————————

    /// <summary>Returns a copy of the 8 logical corners (in the fixed index order shown in the header).</summary>
    public Vector3[] GetCorners() => (Vector3[])corners.Clone();

    /// <summary>Set a single corner’s local position. Mesh + spheres update automatically.</summary>
    public void SetCorner(int index, Vector3 localPos)
    {
        if (index < 0 || index > 7) return;
        corners[index] = localPos;

        // Update mesh vertices that reference this corner
        var v = mesh.vertices;
        foreach (int vi in cornerToMeshIndices[index])
            v[vi] = localPos;
        mesh.vertices = v;
        mesh.RecalculateBounds();

        // Update sphere
        var sphere = vertsRoot.Find($"corner_{index}");
        if (sphere) sphere.localPosition = localPos;
    }

    /// <summary>Uniformly resizes the cube around its local origin by updating corners (keeps current shape axis-aligned).</summary>
    public void SetUniformSize(float newSize)
    {
        size = Mathf.Max(0.0001f, newSize);
        InitializeCornersFromSize();
        RebuildAll();
    }
}