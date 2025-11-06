import base64
import io
from flask import Flask, request, jsonify
from PIL import Image
import google.generativeai as genai

app = Flask(__name__)

genai.configure(api_key="YOUR_GEMINI_API_KEY")

model = genai.GenerativeModel("gemini-1.5-pro")

@app.route("/process", methods=["POST"])
def process_input():
    data = request.get_json()

    image_b64 = data.get("image")
    audio_b64 = data.get("audio")

    # Decode image/audio
    image_data = base64.b64decode(image_b64)
    image = Image.open(io.BytesIO(image_data))
    audio_data = base64.b64decode(audio_b64)

    response = model.generate_content(
        [
            {"text": """
                    You are a Unity Scene Assistant.
                    Using the image and the voice command, respond ONLY with a JSON that fits one of the following commands.
                    If the command is to add an object, output:
                    {{
                        \"command\": \"add_object\", 
                        \"name\": \"\", 
                        \"primitive\": \"\", 
                        \"position\": []
                    }}
                    If the command is to delete an object, output:
                    {{
                        \"command\": \"delete_object\", 
                        \"name\": \"\"
                    }}
                    If the command is to rotate an object, output:
                    {{
                        \"command\": \"rotate_object\", 
                        \"name\": \"\", 
                        \"rotation\": []
                    }}
                    If the command is to translate an object, output:
                    {{
                        \"command\": \"rotate_object\", 
                        \"name\": \"\", 
                        \"translation\": []
                    }}
             """},
            {"image": image},
            {"audio": {"data": audio_data, "mime_type": "audio/wav"}},
        ],
        generation_config={"response_mime_type": "application/json"}
    )

    return jsonify({"result": response.text})

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
