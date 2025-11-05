# 598 VR Project - EditableCube

A Unity VR/Desktop project featuring an editable cube system with universal XR/Desktop support.

## Features

- **EditableCube**: Interactive mesh editing with runtime vertex manipulation
- **Universal XR/Desktop Support**: Automatically works in both VR and desktop modes
- **Grid & Axis Visualization**: World-aligned reference grid and axis lines
- **Vertex Editor**: Click and drag vertices to deform the cube in real-time
- **VR Ready**: Full XR Interaction Toolkit integration

## Unity Version

- Unity 6.2 (6000.2.10f1)
- Universal Render Pipeline (URP)

## Getting Started

### Clone the Repository

```bash
git clone <your-repo-url>
cd 598_v1
```

### Open in Unity

1. Open Unity Hub
2. Click "Add" → "Add project from disk"
3. Navigate to the cloned folder
4. Click "Open"

### Desktop Mode (No VR Headset)

1. Press Play
2. Controls:
   - **WASD** - Move
   - **Mouse** - Look
   - **Tab** - Toggle Mesh/Vertex editing mode
   - **Click & Drag** - Edit vertices (in Vertex mode)
   - **ESC** - Toggle cursor lock

### VR Mode (With Headset)

1. Connect VR headset
2. Press Play
3. Use VR controllers as normal
4. Automatically detects and switches to VR mode

## Project Structure

```
Assets/
├── Materials/          # Materials and shaders
│   ├── Axis.shader     # X/Z axis lines shader
│   ├── GridURP.shader  # Grid shader (URP compatible)
│   └── *.mat          # Material assets
├── Scripts/           # C# scripts
│   ├── EditableCube.cs           # Main cube editing logic
│   ├── RuntimeVertexEditor.cs    # Play mode vertex editing
│   ├── XRAutoSetup.cs           # Automatic XR/Desktop detection
│   ├── DesktopCameraController.cs # FPS camera controls
│   ├── UniversalInput.cs         # Cross-platform input wrapper
│   └── Editor/                   # Editor scripts
│       └── EditableCubeEditor.cs # Scene view editing tools
├── Scenes/            # Unity scenes
└── VRTemplateAssets/  # VR template resources
```

## Key Scripts

### EditableCube
Manages a procedurally generated cube with editable vertices.
- 8 logical corners drive 24 mesh vertices (for hard edges)
- Switch between Mesh and Vertices display modes
- Runtime and editor editing support

### RuntimeVertexEditor
Enables vertex editing during Play mode:
- Press **Tab** to toggle Vertex mode
- Click near vertices to select (shows labels V0-V7)
- Drag to move vertices in 3D space
- Changes automatically save

### XRAutoSetup
Automatically detects XR device and configures scene:
- VR headset detected → Use XR Origin
- No VR headset → Use Desktop camera + controls
- No manual switching required

## Controls

### Desktop Mode
- **W/A/S/D** - Forward/Left/Back/Right
- **Q/E** - Down/Up
- **Mouse** - Look around
- **Shift** - Sprint
- **Tab** - Toggle Mesh/Vertex mode
- **ESC** - Unlock cursor

### VR Mode
- **Trigger** - Interact/Select
- **Thumbstick** - Move
- **Grip** - Grab
- **Menu** - Settings

## Troubleshooting

### Cube faces rendering wrong
- Set material **Render Face** (or **Cull**) to **Back**
- Normals point outward after recent fixes

### Grid/Axes overlaying objects
- Set Grid material Render Queue to 2450-2451
- Ensure objects are opaque or use depth-writing shaders

### Vertex editing not working
- Ensure EditableCube is in **Vertices mode**
- RuntimeVertexEditor must be in the scene
- Check Console for debug messages

### No XR/Desktop mode detection
- Check XRAutoSetup component is in scene
- Assign DesktopCamera and XR Rig references

## Development Notes

- Uses `[ExecuteAlways]` for editor-time cube updates
- Corners are serialized for persistence
- Input works with both legacy and new Input System
- VR-ready with stereo rendering support in shaders

## License

Educational project for course 598.

## Contributors

- Henry Wagner

