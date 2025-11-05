#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for EditableCube that allows dragging vertex spheres in the Scene view.
/// Changes are automatically saved to the prefab/scene.
/// </summary>
[CustomEditor(typeof(EditableCube))]
public class EditableCubeEditor : Editor
{
    private int selectedCorner = -1;
    private Tool lastTool = Tool.None;
    
    void OnSceneGUI()
    {
        EditableCube cube = (EditableCube)target;
        
        // Only show handles in Vertices mode
        if (cube.mode != EditableCube.DisplayMode.Vertices)
            return;
        
        Vector3[] corners = cube.GetCorners();
        Transform cubeTransform = cube.transform;
        
        // Draw position handles for each corner
        for (int i = 0; i < 8; i++)
        {
            // Convert local position to world position
            Vector3 worldPos = cubeTransform.TransformPoint(corners[i]);
            
            // Draw a position handle
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                // Record undo
                Undo.RecordObject(cube, "Move Cube Corner");
                
                // Convert back to local position
                Vector3 newLocalPos = cubeTransform.InverseTransformPoint(newWorldPos);
                
                // Update the corner
                cube.SetCorner(i, newLocalPos);
                
                // Mark as dirty so changes are saved
                EditorUtility.SetDirty(cube);
            }
            
            // Draw labels
            Handles.Label(worldPos + Vector3.up * 0.1f, $"Corner {i}");
        }
    }
    
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        EditableCube cube = (EditableCube)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Switch to Vertices mode and use the position handles in the Scene view to drag corners. " +
            "Changes are automatically saved.", 
            MessageType.Info
        );
        
        // Quick reset button
        if (GUILayout.Button("Reset to Cube"))
        {
            Undo.RecordObject(cube, "Reset Cube");
            
            float h = cube.size * 0.5f;
            cube.SetCorner(0, new Vector3(-h, -h, -h));
            cube.SetCorner(1, new Vector3(+h, -h, -h));
            cube.SetCorner(2, new Vector3(-h, +h, -h));
            cube.SetCorner(3, new Vector3(+h, +h, -h));
            cube.SetCorner(4, new Vector3(-h, -h, +h));
            cube.SetCorner(5, new Vector3(+h, -h, +h));
            cube.SetCorner(6, new Vector3(-h, +h, +h));
            cube.SetCorner(7, new Vector3(+h, +h, +h));
            
            EditorUtility.SetDirty(cube);
        }
    }
}
#endif

