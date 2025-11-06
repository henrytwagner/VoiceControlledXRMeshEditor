#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for EditableMesh that provides Scene view handles for:
/// - Moving individual vertices
/// - Translating the entire mesh
/// </summary>
[CustomEditor(typeof(EditableMesh))]
public class EditableMeshEditor : Editor
{
    private enum HandleMode { MoveWhole, MoveVertex }
    
    private HandleMode handleMode = HandleMode.MoveWhole;
    private int selectedVertex = -1;
    
    void OnSceneGUI()
    {
        EditableMesh mesh = (EditableMesh)target;
        
        // Only show handles in Edit mode
        if (mesh.mode != EditableMesh.DisplayMode.Edit)
            return;
        
        Transform meshTransform = mesh.transform;
        
        // Toggle handle mode with 'G' key (G for Grab/Global)
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.G)
        {
            handleMode = (handleMode == HandleMode.MoveWhole) ? HandleMode.MoveVertex : HandleMode.MoveWhole;
            e.Use();
            SceneView.RepaintAll();
        }
        
        // Show instructions
        Handles.BeginGUI();
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        
        string modeText = handleMode == HandleMode.MoveWhole ? "MOVE WHOLE MESH" : "MOVE VERTICES";
        GUI.Box(new Rect(10, 10, 200, 40), $"Mode: {modeText}\nPress 'G' to toggle", style);
        Handles.EndGUI();
        
        if (handleMode == HandleMode.MoveWhole)
        {
            DrawMoveWholeHandle(mesh, meshTransform);
        }
        else
        {
            DrawVertexHandles(mesh, meshTransform);
        }
    }
    
    void DrawMoveWholeHandle(EditableMesh mesh, Transform meshTransform)
    {
        // Position handle at mesh center
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(meshTransform.position, Quaternion.identity);
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(meshTransform, "Move Mesh");
            meshTransform.position = newPosition;
            EditorUtility.SetDirty(mesh);
        }
        
        // Also show rotation handle
        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.RotationHandle(meshTransform.rotation, meshTransform.position);
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(meshTransform, "Rotate Mesh");
            meshTransform.rotation = newRotation;
            EditorUtility.SetDirty(mesh);
        }
    }
    
    void DrawVertexHandles(EditableMesh mesh, Transform meshTransform)
    {
        Vector3[] vertices = mesh.GetVertices();
        
        // Draw handles for each vertex
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            
            // Smaller handles for vertices
            float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.15f;
            
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mesh, $"Move Vertex {i}");
                Vector3 newLocalPos = meshTransform.InverseTransformPoint(newWorldPos);
                mesh.SetVertex(i, newLocalPos);
                EditorUtility.SetDirty(mesh);
            }
            
            // Draw label
            Handles.Label(worldPos + Vector3.up * handleSize * 2, $"V{i}");
        }
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditableMesh mesh = (EditableMesh)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Switch to Edit mode to edit.\n" +
            "In Scene view:\n" +
            "• Press 'G' to toggle between Move Whole Mesh and Move Vertices\n" +
            "• Drag handles to move vertices or entire mesh", 
            MessageType.Info
        );
        
        if (GUILayout.Button("Rebuild from Source Mesh"))
        {
            Undo.RecordObject(mesh, "Rebuild Mesh");
            mesh.RebuildFromSource();
            EditorUtility.SetDirty(mesh);
        }
        
        if (mesh.GetVertexCount() > 0)
        {
            EditorGUILayout.LabelField($"Unique Vertices: {mesh.GetVertexCount()}");
        }
    }
}
#endif

