# Voice Command API Documentation

This document describes the JSON format your AI/VLM should generate to control mesh editing via voice commands.

## Important Notes

1. **Units**: Unity uses meters. If the user says "7 centimeters", convert to 0.07 meters
2. **Coordinate System**: 
   - X: Right (+) / Left (-)
   - Y: Up (+) / Down (-)
   - Z: Forward (+) / Back (-)
3. **Vertex Indices**: Start at 0 (V0, V1, V2, etc.)
4. **Multiple Objects**: Use `object_name` to target specific objects (e.g., "Cube_1", "Sphere_2")
   - If `object_name` is omitted, the command affects the **currently selected** object
   - Object names are shown in the scene and can be queried with voice commands

## Object Targeting

All commands support an optional `object_name` field to target specific objects:

```json
{
    "command": "move_vertex",
    "object_name": "Cube_1",
    "vertex": 1,
    "offset": {"x": 0, "y": 0.1, "z": 0}
}
```

**Targeting Priority**:
1. If `object_name` is specified → Find object by that exact name
2. If no `object_name` → Use currently **selected** object (clicked object)
3. If nothing selected → Use fallback target mesh

**Getting Object Names**:
- Objects are automatically named: `Cube_1`, `Sphere_2`, `Cylinder_1`, etc.
- User can say "what objects are in the scene?" to list them
- Names are visible in the Transform Panel when objects are selected

## Command Reference

### 1. Move Vertex (Relative)

Move a single vertex by an offset vector.

**Voice Example**: "Move vertex 1 back 7 centimeters"

**JSON Output**:
```json
{
    "command": "move_vertex",
    "vertex": 1,
    "offset": {"x": 0, "y": 0, "z": -0.07}
}
```

**Voice Example**: "Move V3 up 5cm and right 2cm"

**JSON Output**:
```json
{
    "command": "move_vertex",
    "vertex": 3,
    "offset": {"x": 0.02, "y": 0.05, "z": 0}
}
```

---

### 2. Move Multiple Vertices (Relative)

Move several vertices by the same offset.

**Voice Example**: "Move vertices 0, 1, and 2 forward 10 centimeters"

**JSON Output**:
```json
{
    "command": "move_vertices",
    "vertices": [0, 1, 2],
    "offset": {"x": 0, "y": 0, "z": 0.1}
}
```

---

### 3. Set Vertex Position (Absolute)

Set a vertex to an exact position in local space.

**Voice Example**: "Set vertex 4 to position 0.5, 0.5, 0.5"

**JSON Output**:
```json
{
    "command": "set_vertex",
    "vertex": 4,
    "position": {"x": 0.5, "y": 0.5, "z": 0.5}
}
```

---

### 4. Reset Vertex

Reset a vertex to its original position from the source mesh.

**Voice Example**: "Reset vertex 2"

**JSON Output**:
```json
{
    "command": "reset_vertex",
    "vertex": 2
}
```

---

### 5. Translate Mesh

Move the entire mesh by an offset.

**Voice Example**: "Move the mesh forward 1 meter"

**JSON Output**:
```json
{
    "command": "translate_mesh",
    "offset": {"x": 0, "y": 0, "z": 1}
}
```

**Voice Example**: "Move the mesh up 50cm and left 30cm"

**JSON Output**:
```json
{
    "command": "translate_mesh",
    "offset": {"x": -0.3, "y": 0.5, "z": 0}
}
```

---

### 6. Rotate Mesh

Rotate the mesh by euler angles (degrees).

**Voice Example**: "Rotate the mesh 90 degrees clockwise"

**JSON Output**:
```json
{
    "command": "rotate_mesh",
    "rotation": {"x": 0, "y": -90, "z": 0}
}
```

**Voice Example**: "Tilt the mesh forward 45 degrees"

**JSON Output**:
```json
{
    "command": "rotate_mesh",
    "rotation": {"x": 45, "y": 0, "z": 0}
}
```

---

### 7. Scale Mesh (Uniform)

Scale the mesh uniformly.

**Voice Example**: "Make the mesh twice as big"

**JSON Output**:
```json
{
    "command": "scale_mesh",
    "scale": 2.0
}
```

**Voice Example**: "Scale the mesh to half size"

**JSON Output**:
```json
{
    "command": "scale_mesh",
    "scale": 0.5
}
```

---

### 8. Scale Mesh (Non-uniform)

Scale the mesh differently on each axis.

**Voice Example**: "Make the mesh twice as tall but keep width the same"

**JSON Output**:
```json
{
    "command": "scale_mesh",
    "scaleVector": {"x": 1, "y": 2, "z": 1}
}
```

---

### 9. Rebuild Mesh

Reset the entire mesh to its original state.

**Voice Example**: "Reset the mesh" or "Undo all changes"

**JSON Output**:
```json
{
    "command": "rebuild_mesh"
}
```

---

### 10. List Objects

Get a list of all editable objects in the scene with their properties.

**Voice Example**: "What objects are in the scene?" or "List all objects"

**JSON Output**:
```json
{
    "command": "list_objects"
}
```

**Returns**: List of object names, vertex counts, and positions
```
Objects in scene:
- Cube_1: 8 vertices, position (0.00, 0.00, 0.00)
- Sphere_1: 382 vertices, position (2.50, 0.00, 0.00)
- Cylinder_1: 66 vertices, position (-2.00, 1.00, 0.00)
```

---

## Direction Mappings for AI

When the user says directional words, convert them to vectors:

| User Says | X | Y | Z |
|-----------|---|---|---|
| Forward   | 0 | 0 | + |
| Back/Backward | 0 | 0 | - |
| Up        | 0 | + | 0 |
| Down      | 0 | - | 0 |
| Right     | + | 0 | 0 |
| Left      | - | 0 | 0 |

## Unit Conversions for AI

| User Says | Unity Value (meters) |
|-----------|---------------------|
| 1 centimeter (cm) | 0.01 |
| 5 centimeters | 0.05 |
| 10 centimeters | 0.1 |
| 1 meter (m) | 1.0 |
| 1 millimeter (mm) | 0.001 |

## Complex Command Examples

### Example 1: "Move vertex 0 down 3cm and back 5cm" (on selected object)
```json
{
    "command": "move_vertex",
    "vertex": 0,
    "offset": {"x": 0, "y": -0.03, "z": -0.05}
}
```

### Example 2: "Move vertex 2 of Cube_1 up 10cm"
```json
{
    "command": "move_vertex",
    "object_name": "Cube_1",
    "vertex": 2,
    "offset": {"x": 0, "y": 0.1, "z": 0}
}
```

### Example 3: "Move the top vertices of Sphere_2 forward"
(Assuming vertices 0-3 are top vertices based on context)
```json
{
    "command": "move_vertices",
    "object_name": "Sphere_2",
    "vertices": [0, 1, 2, 3],
    "offset": {"x": 0, "y": 0, "z": 0.1}
}
```

### Example 4: "Rotate Cylinder_1 by 180 degrees"
```json
{
    "command": "rotate_mesh",
    "object_name": "Cylinder_1",
    "rotation": {"x": 0, "y": 180, "z": 0}
}
```

### Example 5: Multi-object workflow
User: "What's in the scene?"
```json
{"command": "list_objects"}
```

User: "Move vertex 0 of Cube_2 up 5cm"
```json
{
    "command": "move_vertex",
    "object_name": "Cube_2",
    "vertex": 0,
    "offset": {"x": 0, "y": 0.05, "z": 0}
}
```

## Integration Flow

```
User Voice Input → Speech-to-Text → AI/VLM Processing → JSON Command → VoiceCommandProcessor.ProcessCommand(json)
```

### Example Code for Your AI Integration:

```csharp
// In your AI integration script:
public VoiceCommandProcessor commandProcessor;

void OnAIResponseReceived(string jsonFromAI)
{
    var result = commandProcessor.ProcessCommand(jsonFromAI);
    
    if (result.success)
    {
        Debug.Log($"✓ Command executed: {result.message}");
        // Provide voice feedback to user: "Done!"
    }
    else
    {
        Debug.LogError($"✗ Command failed: {result.message}");
        // Provide voice feedback to user: "Sorry, that didn't work"
    }
}
```

## Context Information for AI

Your AI can query the current mesh state to provide better context:

```csharp
// Get info about current mesh state
Vector3[] allVertices = commandProcessor.GetAllVertexPositions();
Vector3 specificVertex = commandProcessor.GetVertexPosition(1);
var (position, rotation, scale) = commandProcessor.GetMeshTransform();
```

This allows the AI to:
- Know where vertices currently are
- Make relative movements more intelligent
- Provide feedback like "Vertex 1 is now at position..."

## Testing

1. Add `VoiceCommandProcessor` to a GameObject
2. Add `VoiceCommandTester` to test commands
3. Use Inspector context menu to try example commands
4. Press 'T' in Play mode to execute the JSON in the inspector field

