import base64
import json
import os
import shutil
from concurrent.futures import ThreadPoolExecutor, TimeoutError as FuturesTimeoutError
from copy import deepcopy

from flask import Flask, request, jsonify
import ollama
import whisper

app = Flask(__name__)
stt_model = whisper.load_model("base.en")
executor = ThreadPoolExecutor(max_workers=2)

# # Ensure ffmpeg is available even when PATH is trimmed (e.g. from a venv shell)
# if shutil.which("ffmpeg") is None:
#     fallback_ffmpeg_dir = r"C:\Users\htwagner\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg.Essentials_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0-essentials_build\bin"
#     if os.path.isdir(fallback_ffmpeg_dir):
#         os.environ["PATH"] = fallback_ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")

ALLOWED_COMMANDS = {
    "spawn_object",
    "delete_object",
    # "rename_object",
    # "select_object",
    "translate_mesh",
    "rotate_mesh",
    # "scale_mesh",
    # "move_vertex",
    # "move_vertices",
    # "set_vertex",
    # "reset_vertex",
    # "set_mode",
    # "toggle_labels",
    # "clear_all",
}


def is_vector(value):
    if not isinstance(value, dict):
        return False
    required_keys = {"x", "y", "z"}
    if not required_keys.issubset(value.keys()):
        return False
    try:
        float(value["x"])
        float(value["y"])
        float(value["z"])
    except (TypeError, ValueError):
        return False
    return True


def validate_payload(payload):
    if not isinstance(payload, dict):
        return False, "Response must be a JSON object."

    command = payload.get("command")
    if not command or not isinstance(command, str):
        return False, "Missing or invalid 'command' field."

    command_lower = command.lower()
    # command_lower, was_corrected = normalize_command(command_lower)
    # if was_corrected:
    #     print(f"[VLM Service] Auto-corrected command '{command}' -> '{command_lower}'")

    if command_lower not in ALLOWED_COMMANDS:
        return False, f"Command '{command}' is not in the allowed list."

    payload["command"] = command_lower

    if command_lower == "spawn_object":
        if not payload.get("primitive_type"):
            return False, "spawn_object requires 'primitive_type'."
    elif command_lower == "delete_object":
        if not payload.get("object_name"):
            return False, "delete_object requires 'object_name'."
    # elif command_lower == "rename_object":
    #     if not payload.get("object_name") or not payload.get("new_name"):
    #         return False, "rename_object requires 'object_name' and 'new_name'."
    # elif command_lower == "select_object":
    #     if not payload.get("object_name"):
    #         return False, "select_object requires 'object_name'."
    elif command_lower == "translate_mesh":
        if not (is_vector(payload.get("offset")) or is_vector(payload.get("position"))):
            return False, "translate_mesh requires 'offset' or 'position' vector."
    elif command_lower == "rotate_mesh":
        if not is_vector(payload.get("rotation")):
            return False, "rotate_mesh requires 'rotation' vector."
    # elif command_lower == "scale_mesh":
    #     has_scale = isinstance(payload.get("scale"), (int, float))
    #     has_vector = is_vector(payload.get("scaleVector"))
    #     if not (has_scale or has_vector):
    #         return False, "scale_mesh requires 'scale' or 'scaleVector'."
    # elif command_lower == "move_vertex":
    #     if not isinstance(payload.get("vertex"), int):
    #         return False, "move_vertex requires 'vertex' index."
    #     if not is_vector(payload.get("offset")):
    #         return False, "move_vertex requires 'offset' vector."
    # elif command_lower == "move_vertices":
    #     vertices = payload.get("vertices")
    #     if not isinstance(vertices, list) or len(vertices) == 0:
    #         return False, "move_vertices requires non-empty 'vertices' array."
    #     if not all(isinstance(v, int) for v in vertices):
    #         return False, "move_vertices 'vertices' must contain integers."
    #     if not is_vector(payload.get("offset")):
    #         return False, "move_vertices requires 'offset' vector."
    # elif command_lower == "set_vertex":
    #     if not isinstance(payload.get("vertex"), int):
    #         return False, "set_vertex requires 'vertex' index."
    #     if not is_vector(payload.get("position")):
    #         return False, "set_vertex requires 'position' vector."
    # elif command_lower == "reset_vertex":
    #     if not isinstance(payload.get("vertex"), int):
    #         return False, "reset_vertex requires 'vertex' index."
    # elif command_lower == "set_mode":
    #     if not payload.get("mode"):
    #         return False, "set_mode requires 'mode'."

    return True, None


# def normalize_command(command):
#     if command in ALLOWED_COMMANDS:
#         return command, False

#     best_match = None
#     best_distance = 3  # allow corrections up to distance 2

#     for candidate in ALLOWED_COMMANDS:
#         distance = levenshtein_distance(command, candidate)
#         if distance < best_distance:
#             best_distance = distance
#             best_match = candidate
#             if distance == 1:
#                 break

#     if best_match is not None:
#         return best_match, True

#     return command, False


# def levenshtein_distance(a, b):
#     if a == b:
#         return 0
#     if len(a) == 0:
#         return len(b)
#     if len(b) == 0:
#         return len(a)

#     previous_row = list(range(len(b) + 1))
#     for i, ca in enumerate(a, start=1):
#         current_row = [i]
#         for j, cb in enumerate(b, start=1):
#             insertions = previous_row[j] + 1
#             deletions = current_row[j - 1] + 1
#             substitutions = previous_row[j - 1] + (0 if ca == cb else 1)
#             current_row.append(min(insertions, deletions, substitutions))
#         previous_row = current_row

#     return previous_row[-1]


def request_with_validation(base_messages, max_attempts=2):
    """
    Call the VLM with validation and retry. Returns (payload, raw_output, success, error_message)
    """
    messages = deepcopy(base_messages)
    last_output = ""
    error_message = None

    for attempt in range(max_attempts):
        print(f"[VLM Service] Attempt {attempt + 1}/{max_attempts} - querying model...")
        future = None
        try:
            future = executor.submit(
                ollama.chat,
                model="llama3.2-vision",
                messages=messages,
                options={
                    "temperature": 0.0,
                    "top_p": 0.85,
                    "repeat_penalty": 1.1,
                },
            )
            response = future.result(timeout=30.0)
        except FuturesTimeoutError:
            error_message = "Ollama request failed: timeout"
            print(f"[VLM Service] {error_message}")
            if future:
                future.cancel()
            break
        except Exception as exc:
            error_message = f"Ollama request failed: {exc}"
            print(f"[VLM Service] {error_message}")
            break
        llm_output = response["message"]["content"].strip()
        last_output = llm_output
        print(f"[VLM Service] Raw model output: {llm_output}")

        try:
            payload = json.loads(llm_output)
        except json.JSONDecodeError as exc:
            error_message = f"Model response was not valid JSON: {exc}"
            print(f"[VLM Service] JSON parse error: {exc}")
            messages.extend(
                [
                    {"role": "assistant", "content": llm_output},
                    {
                        "role": "user",
                        "content": (
                            f"Your response could not be parsed as JSON ({exc}). "
                            "Reply again with JSON only, no code fences or commentary, using one of the allowed commands. "
                            "Do not include ellipses or placeholder values—every field must contain an explicit value. "
                            "If you cannot resolve the direction from the context, respond with {\"command\":\"unknown\",\"reason\":\"need clarification\"}."
                        ),
                    },
                ]
            )
            continue

        valid, validation_error = validate_payload(payload)
        if valid:
            print(f"[VLM Service] Validation passed on attempt {attempt + 1}")
            return payload, llm_output, True, None

        error_message = f"Invalid command payload: {validation_error}"
        print(f"[VLM Service] Validation failed: {validation_error}")
        messages.extend(
            [
                {"role": "assistant", "content": llm_output},
                {
                    "role": "user",
                    "content": (
                        f"{validation_error} Use one of the allowed command names exactly as listed "
                        "and include the required fields. Respond with JSON only and avoid ellipses or placeholder tokens. "
                        "If the context is insufficient, respond with {\"command\":\"unknown\",\"reason\":\"need clarification\"}."
                    ),
                },
            ]
        )

    print(f"[VLM Service] All {max_attempts} attempts failed.")
    return None, last_output, False, error_message or "Model response was not valid after retries"


# default_text = """
# You are a Unity scene editing assistant. Use the provided screenshot context and transcribed voice command to return a single JSON object that matches this schema. Output JSON only, with double quotes, no comments, no markdown fences, no extra keys.

# Schema (omit any fields that are not needed):
# {
#   "command": string,                   // required; use lower_snake_case
#   "object_name": string,               // optional
#   "new_name": string,                  // optional
#   "vertex": int,
#   "vertices": [int, ...],
#   "offset": {"x": float, "y": float, "z": float},
#   "position": {"x": float, "y": float, "z": float},
#   "rotation": {"x": float, "y": float, "z": float},
#   "scale": float,
#   "scaleVector": {"x": float, "y": float, "z": float},
#   "primitive_type": string,            // Cube | Sphere | Cylinder | Capsule | Plane
#   "mode": string,                      // Object | Edit
#   "state": string                      // on/off/true/false (for toggles)
# }

# Valid command values (exactly one of these):
# - "spawn_object"        (requires primitive_type)
# - "delete_object"       (requires object_name)
# - "rename_object"       (requires object_name, new_name)
# - "select_object"       (requires object_name)
# - "translate_mesh"      (requires offset OR position)
# - "rotate_mesh"         (requires rotation)
# - "scale_mesh"          (requires scale OR scaleVector)
# - "move_vertex"         (requires vertex, offset)
# - "move_vertices"       (requires vertices array, offset)
# - "set_vertex"          (requires vertex, position)
# - "reset_vertex"        (requires vertex)
# - "set_mode"            (requires mode; include object_name if targeting a specific object)
# - "toggle_labels"       (optional state field to force on/off)
# - "clear_all"

# If the request cannot be mapped to one of the commands above, respond instead with:
# {"command":"unknown","reason":"<brief explanation>"}   // reason is short plain text

# Rules:
# - Do not invent command names or additional fields.
# - Command names must match the spellings above exactly. Do not output variations such as "translate_messh" or "scale_mash".
# - Keep numbers in meters (convert centimetres to metres: 1 cm = 0.01).
# - Use explicit object names only when the user says them; otherwise omit object_name.
# - Final answer must be valid JSON, nothing else.
# - Interpret spatial phrases relative to the provided context:
#   * "left/right" → use the camera_right vector (left = -camera_right, right = +camera_right).
#   * "forward/back" → use camera_forward (forward = +camera_forward, back = -camera_forward).
#   * "up/down"/"above/below" → use camera_up (up/above = +camera_up, down/below = -camera_up).
#   * Refer to selected object axes (selected_forward/right/up) if the instruction is explicitly object-relative.
#   * When a distance like "one meter" or "50 centimetres" is mentioned, convert to metres and apply along the resolved direction vector.
#   * If the voice command lacks distance but the direction is clear, prefer a default value of 1.0 metre unless the context suggests otherwise.
#   * If ambiguity remains after using the context, request clarification by returning {"command":"unknown","reason":"need clarification"}.
#   * Do not misspell command names (e.g. "translate_mesh" not "translate_messh", "scale_mesh" not "scale_mash").
#   * Do not include ellipses or placeholder tokens (no "..." values); every field must contain an explicit value.


# Examples:
# {"command":"translate_mesh","object_name":"Cube_A","position":{"x":1.5,"y":0.0,"z":-2.0}}
# {"command":"unknown","reason":"No actionable instruction detected"}
# """

default_text = """
You are a Unity scene editing assistant. Use the provided screenshot context and transcribed voice command to return a single JSON object that matches this schema. OUTPUT A SINGLE JSON ONLY. NOTHING ELSE!!!

Schema (omit any fields that are not needed):
{
  "command": string,                   // required; use lower_snake_case
  "object_name": string,               
  "offset": {"x": float, "y": float, "z": float},
  "position": {"x": float, "y": float, "z": float},
  "rotation": {"x": float, "y": float, "z": float},
  "primitive_type": string,            // Cube | Sphere | Cylinder | Capsule | Plane
}

Valid command values (exactly one of these):
- "spawn_object"        (requires primitive_type)
- "delete_object"       (requires object_name)
- "translate_mesh"      (requires offset OR position)
- "rotate_mesh"         (requires rotation)

If the request cannot be mapped to one of the commands above, respond instead with:
{"command":"unknown","reason":"<brief explanation>"}   // reason is short plain text

Rules:
- Do not invent command names or additional fields.
- Command names must match the spellings above exactly. Do not output variations such as "translate_messh" or "rotate_mash".
- Keep numbers in meters (convert centimetres to metres: 1 cm = 0.01).
- Final answer must be valid JSON, nothing else!!!


Examples:
{"command":"translate_mesh","object_name":"Cube_A","position":{"x":1.5,"y":0.0,"z":-2.0}}
{"command":"unknown","reason":"No actionable instruction detected"}
"""

chat_history = ""

@app.route("/process", methods=["POST"])
def process_input():
    data = request.get_json()

    image_b64 = data.get("image")
    audio_b64 = data.get("audio")
    context = data.get("context")

    temp_audio_file = "temp.wav"
    try:
        audio_bytes = base64.b64decode(audio_b64)

        with open(temp_audio_file, "wb") as f:
            f.write(audio_bytes)

        print("Transcribing audio...")
        result = stt_model.transcribe(temp_audio_file)
        voice_command_text = result["text"]
        print(f"Transcribed text: {voice_command_text}")

    finally:
        if os.path.exists(temp_audio_file):
            os.remove(temp_audio_file)

    context_block = ""
    if context:
        context_json = json.dumps(context, indent=2)
        context_block = f"Context:\n{context_json}\n\n"

    full_prompt = f"{default_text}\n\n{context_block}\nUser request:\n{voice_command_text}\n\nChat History:{chat_history}"

    chat_history += f"Question: {voice_command_text}\n"

    messages = [
        {
            "role": "user",
            "content": full_prompt,
            "images": [image_b64] if image_b64 else None,
        }
    ]

    # Remove None image entries to avoid issues with the API
    if messages[0]["images"] is None:
        del messages[0]["images"]

    command_payload, llm_output, success, error = request_with_validation(messages)

    chat_history += f"Response: {llm_output}\n"

    return jsonify(
        {
            "success": success,
            "error": error,
            "raw": llm_output,
            "transcript": voice_command_text,
            "command": command_payload,
        }
    )

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
