# VLM Service Bridge

This folder contains the helper scripts required to run the local multimodal model that powers the voice-driven workflow.

| File | Purpose |
| --- | --- |
| `vlm.py` | Flask service that accepts the screenshot and audio payload, performs ASR with Whisper, queries the Ollama vision model, and returns a structured command JSON. |
| `requirements.txt` | Python dependencies for `vlm.py`. Install them in a virtual environment before launching the server. |

## Quick start

```bash
cd Tools/VLMService
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
python vlm.py
```

The Unity scene expects the service to run on `http://localhost:5000/process` (changeable via the `VoiceCommandVLMClient` component).

