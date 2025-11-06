# Voice Command Integration Guide

This guide explains how to integrate voice/AI control into the Unity mesh editor application.

## Table of Contents
1. [System Overview](#system-overview)
2. [VoiceCommandTester (Testing Tool)](#voicecommandtester-testing-tool)
3. [Integrating with an LLM/VLM](#integrating-with-an-llmvlm)
4. [Complete Command Reference](#complete-command-reference)
5. [Integration Examples](#integration-examples)

---

## System Overview

The voice command system consists of three main components:

### **1. VoiceCommandProcessor**
- **Location**: `Assets/Scripts/VoiceCommandProcessor.cs`
- **Purpose**: Executes JSON commands to control the application
- **Setup**: Attach to a GameObject (e.g., "VoiceCommands")
- **Auto-finds**: All required components (ObjectSelector, MeshSpawner, TransformPanel, etc.)

### **2. VoiceCommandTester**
- **Location**: `Assets/Scripts/VoiceCommandTester.cs`
- **Purpose**: Testing tool for manual JSON command execution
- **Temporary**: Will be replaced by LLM integration
- **How it works**:
  1. Enter JSON in the `testJsonCommand` field in Inspector
  2. Press **T** key in Play mode to execute
  3. See results in Console

### **3. MeshCommand (Data Structure)**
- **Location**: Defined in `VoiceCommandProcessor.cs`
- **Purpose**: JSON-serializable command format
- **Fields**: See [VOICE_COMMAND_API.md](VOICE_COMMAND_API.md) for full reference

---

## VoiceCommandTester (Testing Tool)

### What It Is
A simple testing harness that simulates what an AI/LLM would do:
- Takes JSON input manually entered in Inspector
- Passes it to `VoiceCommandProcessor`
- Displays success/failure in Console

### How to Use It

#### **Setup:**
1. Find the `VoiceCommandTester` GameObject in your scene Hierarchy
2. In the Inspector, you'll see:
   - **Command Processor**: Reference to `VoiceCommandProcessor`
   - **Test Json Command**: Text field for your JSON
   - **Test Key**: "T" (the key to execute)

#### **Testing Workflow:**
1. **Enter a command** in the `Test Json Command` field:
   ```json
   {"command":"spawn_object", "primitive_type":"Cube"}
   ```

2. **Press Play** to enter Play mode

3. **Press T** key to execute the command

4. **Check Console** for results:
   - ✓ Success: `✓ Command succeeded: Created Cube: Cube_1`
   - ✗ Error: `✗ Command failed: No primitive_type specified`

5. **Try another command** - just change the JSON in Inspector and press T again

#### **Example Test Sequence:**
```json
{"command":"list_objects"}
```
Press T → See all objects

```json
{"command":"spawn_object", "primitive_type":"Sphere"}
```
Press T → Creates sphere at origin

```json
{"command":"move_vertex", "vertex":0, "offset":{"x":0,"y":0.1,"z":0}}
```
Press T → Moves vertex 0 of selected object up 10cm

### Why It's Temporary
`VoiceCommandTester` manually executes one command at a time via keyboard input. It's meant for:
- ✅ Testing command syntax
- ✅ Debugging command execution
- ✅ Validating the API before LLM integration

Once you integrate an LLM, you'll replace this with a script that:
- Receives voice input
- Sends it to an LLM/VLM
- Gets JSON response
- Passes JSON to `VoiceCommandProcessor.ProcessCommand()`

---

## Integrating with an LLM/VLM

### Architecture

```
┌─────────────┐    ┌──────────────┐    ┌─────────────┐    ┌──────────────────────┐
│   User      │───▶│  Speech-to-  │───▶│   LLM/VLM   │───▶│ VoiceCommand         │
│   Voice     │    │    Text      │    │  (GPT-4/    │    │ Processor            │
│   Input     │    │   (Unity or  │    │   Claude)   │    │ .ProcessCommand()    │
│             │    │    External) │    │             │    │                      │
└─────────────┘    └──────────────┘    └─────────────┘    └──────────────────────┘
                                              │                       │
                                              ▼                       ▼
                                       ┌──────────────┐      ┌──────────────┐
                                       │ JSON Command │      │  Unity Scene │
                                       │  (formatted) │      │   Updated    │
                                       └──────────────┘      └──────────────┘
```

### Step-by-Step Integration

#### **1. Choose Your Speech-to-Text Solution**

**Option A: Unity's Speech-to-Text** (Windows only)
```csharp
using UnityEngine.Windows.Speech;

public class VoiceCapturer : MonoBehaviour
{
    private DictationRecognizer dictation;
    
    void Start()
    {
        dictation = new DictationRecognizer();
        dictation.DictationResult += OnDictationResult;
        dictation.Start();
    }
    
    void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        Debug.Log($"User said: {text}");
        SendToLLM(text);
    }
}
```

**Option B: External API** (OpenAI Whisper, Google Speech-to-Text, etc.)
- Use Unity's `UnityWebRequest` to call external APIs
- More platform-independent
- Requires internet connection

#### **2. Connect to LLM/VLM**

**Example: OpenAI API Integration**

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class LLMIntegration : MonoBehaviour
{
    public VoiceCommandProcessor commandProcessor;
    
    [Header("API Settings")]
    public string apiKey = "YOUR_API_KEY";
    public string model = "gpt-4";
    
    // System prompt to instruct the LLM
    private const string SYSTEM_PROMPT = @"You are a 3D modeling assistant. 
User will give you voice commands to edit 3D meshes. 
You must respond ONLY with valid JSON commands.
Available commands are documented in VOICE_COMMAND_API.md.
Examples:
- User: 'Move vertex 0 up 5 centimeters' → {""command"":""move_vertex"", ""vertex"":0, ""offset"":{""x"":0,""y"":0.05,""z"":0}}
- User: 'Create a sphere' → {""command"":""spawn_object"", ""primitive_type"":""Sphere""}
- User: 'What's in the scene?' → {""command"":""list_objects""}
Respond with ONLY the JSON, no explanations.";
    
    public void ProcessVoiceInput(string userInput)
    {
        StartCoroutine(SendToLLM(userInput));
    }
    
    IEnumerator SendToLLM(string userText)
    {
        // Prepare request
        string url = "https://api.openai.com/v1/chat/completions";
        
        // Build JSON request
        var requestData = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = SYSTEM_PROMPT },
                new { role = "user", content = userText }
            },
            temperature = 0.3,
            max_tokens = 200
        };
        
        string jsonRequest = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
        
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            string response = request.downloadHandler.text;
            // Parse response and extract JSON command
            string jsonCommand = ExtractJSONFromResponse(response);
            
            // Execute command
            var result = commandProcessor.ProcessCommand(jsonCommand);
            
            if (result.success)
                Debug.Log($"✓ {result.message}");
            else
                Debug.LogError($"✗ {result.message}");
        }
        else
        {
            Debug.LogError($"LLM request failed: {request.error}");
        }
    }
    
    string ExtractJSONFromResponse(string apiResponse)
    {
        // Parse the API response to extract the JSON command
        // Implementation depends on your LLM's response format
        // For OpenAI: response.choices[0].message.content
        return "{}"; // Placeholder
    }
}
```

**Example: Claude API Integration**

Similar to OpenAI but using Anthropic's Claude API:
```csharp
string url = "https://api.anthropic.com/v1/messages";
request.SetRequestHeader("x-api-key", apiKey);
request.SetRequestHeader("anthropic-version", "2023-06-01");
```

#### **3. Replace VoiceCommandTester**

Once your LLM integration works:

**Before (Testing):**
```csharp
// VoiceCommandTester manually sends commands via T key
VoiceCommandTester → VoiceCommandProcessor
```

**After (Production):**
```csharp
// Your integration sends commands from LLM
VoiceInput → LLM → Your Script → VoiceCommandProcessor
```

You can:
- Delete `VoiceCommandTester.cs` and the GameObject
- Or keep it disabled for testing/debugging

### **4. Vision-Language Model (VLM) Integration**

For visual context (seeing the mesh), use a VLM like GPT-4V or Claude 3:

```csharp
public class VLMIntegration : MonoBehaviour
{
    public Camera screenshotCamera;
    public VoiceCommandProcessor commandProcessor;
    
    public void ProcessVoiceWithVisualContext(string userInput)
    {
        // Capture screenshot
        Texture2D screenshot = CaptureScreenshot();
        byte[] imageBytes = screenshot.EncodeToPNG();
        string base64Image = System.Convert.ToBase64String(imageBytes);
        
        // Send to VLM with image
        StartCoroutine(SendToVLM(userInput, base64Image));
    }
    
    Texture2D CaptureScreenshot()
    {
        RenderTexture rt = new RenderTexture(1920, 1080, 24);
        screenshotCamera.targetTexture = rt;
        screenshotCamera.Render();
        
        RenderTexture.active = rt;
        Texture2D screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenshot.Apply();
        
        screenshotCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        
        return screenshot;
    }
    
    IEnumerator SendToVLM(string userText, string base64Image)
    {
        // Send image + text to VLM
        // VLM can see vertex labels, object names, and scene state
        // Returns JSON command
        
        // Example response from VLM after seeing screenshot:
        // User: "Move that top-left vertex up"
        // VLM sees V2 label in top-left → {"command":"move_vertex", "vertex":2, "offset":{"x":0,"y":0.1,"z":0}}
        
        yield return null; // Implementation depends on your VLM API
    }
}
```

---

## Complete Command Reference

### **Mesh Editing** (19 commands)
See [VOICE_COMMAND_API.md](VOICE_COMMAND_API.md) for detailed docs.

#### Vertex Commands (auto-enter Edit mode):
- `move_vertex` - Move single vertex by offset
- `move_vertices` - Move multiple vertices
- `set_vertex` - Set vertex to absolute position
- `reset_vertex` - Reset vertex to original

#### Mesh Transform Commands (work in any mode):
- `translate_mesh` - Move entire mesh
- `rotate_mesh` - Rotate mesh
- `scale_mesh` - Scale uniformly or per-axis
- `rebuild_mesh` - Reset mesh to original

#### Query Commands:
- `get_vertex_position` - Get position of specific vertex
- `get_all_vertices` - List all vertex positions
- `get_mesh_transform` - Get position/rotation/scale
- `list_objects` - List all objects in scene

### **Navigation & UI** (10 commands)

#### Object Management:
- `spawn_object` - Create primitive (Cube, Sphere, etc.)
- `delete_object` - Remove object by name
- `select_object` - Select object by name
- `set_mode` - Switch Object/Edit mode
- `clear_all` - Delete all spawned objects

#### Camera Control:
- `move_camera` - Position or offset camera

#### UI Toggles:
- `toggle_transform_panel` - Show/hide transform info
- `toggle_orientation_gizmo` - Show/hide XYZ gizmo
- `toggle_mouse_look` - Enable/disable crosshair mode

---

## Integration Examples

### Example 1: Basic Command Flow

**User says:** "Create a cube and make it twice as big"

**Your integration script:**
```csharp
void ProcessUserInput(string userSpeech)
{
    // Send to LLM
    string llmResponse = await CallLLM(userSpeech);
    
    // LLM returns: 
    // [{"command":"spawn_object","primitive_type":"Cube"},
    //  {"command":"scale_mesh","scale":2}]
    
    // Parse multiple commands if needed
    ProcessCommands(llmResponse);
}

void ProcessCommands(string jsonResponse)
{
    // If LLM returns array of commands
    if (jsonResponse.StartsWith("["))
    {
        // Parse as array and execute sequentially
        MeshCommand[] commands = JsonHelper.FromJson<MeshCommand>(jsonResponse);
        foreach (var cmd in commands)
        {
            string singleJson = JsonUtility.ToJson(cmd);
            commandProcessor.ProcessCommand(singleJson);
        }
    }
    else
    {
        // Single command
        commandProcessor.ProcessCommand(jsonResponse);
    }
}
```

### Example 2: Multi-Step Editing

**User says:** "Move the top vertices of the cube forward"

**LLM needs context:**
1. Which cube? (If multiple)
2. Which vertices are "top"? (Needs to see current state)

**Solution: Query first, then act**
```csharp
// Step 1: LLM requests context
string context1 = commandProcessor.ProcessCommand("{\"command\":\"list_objects\"}");
// Returns: "Objects in scene: Cube_1: 8 vertices..."

string context2 = commandProcessor.ProcessCommand("{\"command\":\"get_all_vertices\"}");
// Returns: "V0: (0.5, 0.5, 0.5)\nV1: (0.5, 0.5, -0.5)..."

// Step 2: LLM analyzes which vertices are "top" (highest Y values)
// Determines vertices [0,1,2,3] are top

// Step 3: LLM generates edit command
string command = "{\"command\":\"move_vertices\", \"vertices\":[0,1,2,3], \"offset\":{\"x\":0,\"y\":0,\"z\":0.1}}";
commandProcessor.ProcessCommand(command);
```

### Example 3: VLM with Visual Context

**User says:** "Move that vertex" (pointing at screen)

**VLM sees screenshot:**
- Vertex labels (V0, V1, V2...)
- Object names (Cube_1, Sphere_2)
- Current mode (Object/Edit)
- UI state

**VLM response:**
```json
{
    "command": "move_vertex",
    "object_name": "Cube_1",
    "vertex": 2,
    "offset": {"x": 0, "y": 0.1, "z": 0}
}
```

### Example 4: Conversational Workflow

**Conversation:**

User: "What's in the scene?"
```json
{"command":"list_objects"}
```
→ "Objects: Cube_1, Sphere_2"

User: "Delete the sphere"
```json
{"command":"delete_object", "object_name":"Sphere_2"}
```
→ "Deleted Sphere_2"

User: "Make the cube bigger"
```json
{"command":"scale_mesh", "object_name":"Cube_1", "scale":1.5}
```
→ "Scaled Cube_1 to 1.5"

User: "Now edit its vertices"
```json
{"command":"set_mode", "object_name":"Cube_1", "mode":"Edit"}
```
→ "Set Cube_1 to Edit mode"

User: "Move vertex 0 up 5 centimeters"
```json
{"command":"move_vertex", "vertex":0, "offset":{"x":0,"y":0.05,"z":0}}
```
→ "Moved vertex 0"

---

## Testing Checklist

Before integrating with LLM, verify these work with VoiceCommandTester:

### ✅ Object Creation
- [ ] `{"command":"spawn_object", "primitive_type":"Cube"}`
- [ ] `{"command":"spawn_object", "primitive_type":"Sphere"}`
- [ ] `{"command":"list_objects"}`

### ✅ Object Selection & Mode
- [ ] `{"command":"select_object", "object_name":"Cube_1"}`
- [ ] `{"command":"set_mode", "mode":"Edit"}`
- [ ] `{"command":"set_mode", "mode":"Object"}`

### ✅ Vertex Editing
- [ ] `{"command":"move_vertex", "vertex":0, "offset":{"x":0,"y":0.1,"z":0}}`
- [ ] `{"command":"move_vertices", "vertices":[0,1], "offset":{"x":0.05,"y":0,"z":0}}`
- [ ] `{"command":"set_vertex", "vertex":2, "position":{"x":0.5,"y":0.5,"z":0.5}}`

### ✅ Mesh Transforms
- [ ] `{"command":"translate_mesh", "offset":{"x":1,"y":0,"z":0}}`
- [ ] `{"command":"rotate_mesh", "rotation":{"x":0,"y":45,"z":0}}`
- [ ] `{"command":"scale_mesh", "scale":2}`

### ✅ UI Control
- [ ] `{"command":"toggle_transform_panel"}`
- [ ] `{"command":"toggle_orientation_gizmo"}`
- [ ] `{"command":"toggle_mouse_look"}`

### ✅ Cleanup
- [ ] `{"command":"delete_object", "object_name":"Cube_1"}`
- [ ] `{"command":"clear_all"}`

---

## Best Practices

### **1. Error Handling**
Always check `CommandResult.success`:
```csharp
var result = commandProcessor.ProcessCommand(jsonCommand);
if (result.success)
{
    // Provide voice feedback: "Done!"
    SpeakToUser("Done!");
}
else
{
    // Provide voice feedback: "Sorry, that didn't work"
    SpeakToUser($"Error: {result.message}");
}
```

### **2. Context Awareness**
LLM should track:
- What objects exist (`list_objects`)
- What's currently selected
- Current mode (Object vs Edit)
- Vertex positions for relative commands

### **3. Multi-Step Commands**
Break complex requests into steps:

User: "Create a cube, make it bigger, and move the top up"

LLM generates sequence:
```json
[
  {"command":"spawn_object", "primitive_type":"Cube"},
  {"command":"scale_mesh", "scale":1.5},
  {"command":"set_mode", "mode":"Edit"},
  {"command":"move_vertices", "vertices":[0,1,2,3], "offset":{"x":0,"y":0.1,"z":0}}
]
```

### **4. Unit Conversion**
Always convert user units to meters:
- "5 centimeters" → `0.05`
- "1 meter" → `1.0`
- "50 millimeters" → `0.05`

### **5. Direction Mapping**
Map natural language to vectors:
- "up" → `{"x":0, "y":+, "z":0}`
- "forward" → `{"x":0, "y":0, "z":+}`
- "right" → `{"x":+, "y":0, "z":0}`

See [VOICE_COMMAND_API.md](VOICE_COMMAND_API.md) for complete direction reference.

---

## Recommended LLM Prompt Template

```
You are a 3D mesh editing assistant for Unity. 
Users will give voice commands to create and edit 3D objects.

AVAILABLE COMMANDS:
- spawn_object, delete_object, select_object, list_objects, clear_all
- move_vertex, move_vertices, set_vertex, reset_vertex
- translate_mesh, rotate_mesh, scale_mesh, rebuild_mesh
- set_mode, move_camera
- toggle_transform_panel, toggle_orientation_gizmo, toggle_mouse_look

COORDINATE SYSTEM:
- X: Right(+) / Left(-)
- Y: Up(+) / Down(-)
- Z: Forward(+) / Back(-)

UNITS: Unity uses meters
- "5 cm" = 0.05
- "10 cm" = 0.1
- "1 m" = 1.0

OBJECT TARGETING:
- Include "object_name" to target specific objects (e.g., "Cube_1")
- Omit "object_name" to affect currently selected object
- Use "list_objects" to see what's available

MODES:
- Object mode: Transform whole mesh
- Edit mode: Edit individual vertices (required for vertex commands)

RESPOND WITH ONLY VALID JSON. NO EXPLANATIONS.

Examples:
User: "Create a sphere"
You: {"command":"spawn_object", "primitive_type":"Sphere"}

User: "Move vertex 0 up 5cm"
You: {"command":"move_vertex", "vertex":0, "offset":{"x":0,"y":0.05,"z":0}}

User: "What objects are here?"
You: {"command":"list_objects"}
```

---

## Debugging Tips

### Command Not Executing?
1. Check Console for error messages
2. Verify JSON syntax (use jsonlint.com)
3. Check object_name matches exactly
4. Ensure target object exists (`list_objects`)

### Vertex Not Moving?
1. Mesh must be in Edit mode (auto-switches)
2. Vertex index must be valid (0 to vertexCount-1)
3. Check vertex labels in Game view (V0, V1, V2...)

### Selection Issues?
1. Ensure ObjectSelector exists in scene
2. Check `useSelectedObject = true` in VoiceCommandProcessor
3. Manually select object first, then try command

### Reference Not Found?
1. VoiceCommandProcessor auto-finds components on Start()
2. Or manually assign in Inspector
3. Check component is enabled and GameObject is active

---

## Performance Considerations

- **Network Latency**: LLM calls take 1-3 seconds
- **Rate Limiting**: Batch related commands
- **Context Size**: Send only necessary vertex data
- **Caching**: Cache object lists, update only on changes

---

## Next Steps

1. **Test all commands** with VoiceCommandTester
2. **Choose your LLM** (GPT-4, Claude, Gemini, etc.)
3. **Implement speech-to-text** (Unity Windows.Speech or external API)
4. **Create LLM integration script** using examples above
5. **Add voice feedback** (text-to-speech for responses)
6. **Replace VoiceCommandTester** with your integration

The voice command API is production-ready and waiting for your LLM integration! 🎤🤖

