import ollama

class PseudocodeGenerationAgent:
    def __init__(self):
        self.model_name = "model_name"
        self.system_prompt = system_prompt
        self.model_cfg = "model_cfg"
    
    def generate_pseudocode(self, code):
        messages = [
            ollama.Message(self.system_prompt, "system"),
            ollama.Message(user_input, "user")
        ]

        response = ollama.create_chat(self.model_name, self.model_cfg, messages)
        return response.text