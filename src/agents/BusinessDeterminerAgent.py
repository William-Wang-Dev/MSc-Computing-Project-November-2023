import ollama

class BusinessDeterminerAgent:
    def __init__(self):
        self.model_name = "model_name"
        self.system_prompt = system_prompt
        self.model_cfg = "model_cfg"
    
    def purpose_business_keyword(self, code):
        messages = [
            ollama.create_system_message(self.system_prompt),
            ollama.create_user_message(code)
        ]
        
        response = ollama.process_messages(self.model_name, self.model_cfg, messages)
        return response.text