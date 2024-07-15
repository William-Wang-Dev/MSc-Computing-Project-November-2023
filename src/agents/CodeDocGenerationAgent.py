import os
import ollama

class CodeDocGenerationAgent:
    def __init__(self):
        self._model_name = "codeqwen:7b-chat-v1.5-q8_0"
        self._model_options = {
            "temperature": 0.1,
            "num_ctx": 10000,
            "stop": ["<|im_start|>", "<|im_end|>"]
        }
        self._prompt_template = self._load_prompt_template()
        
    def _load_prompt_template(self):
        prompt_path = os.path.join(os.path.dirname(__file__), '..', 'prompts', 'code_explanation_prompt.txt')
        with open(prompt_path, 'r') as file:
            return file.read()
        
    def generate_docs(self, code_context, code_snippet, language_name="C#", doc_formatting_name="XML"):
        prompt = self._prompt_template.format(
            language_name=language_name,
            doc_formatting_name=doc_formatting_name,
            code_context=code_context,
            code_snippet=code_snippet
        )
        
        # Initialize the model (this step might be optimized to avoid repeated initialization)
        ollama.generate(model=self._model_name, prompt=prompt, options=self._model_options, keep_alive=0)
        
        # Generate the actual response
        response = ollama.generate(model=self._model_name, prompt=prompt, options=self._model_options, keep_alive=0)
        return response['response']  # Assuming the response is in the correct format