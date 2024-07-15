import json
from typing import List, Dict, Any
import ollama

class ReRankingAgent:
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self.model_name = model_name
        self.prompt_template = self._load_prompt_template()

    def _load_prompt_template(self) -> str:
        with open('prompts/reranking_prompt.txt', 'r') as file:
            return file.read()

    def evaluate_relevance(self, question: str, data_item: str) -> Dict[str, Any]:
        prompt = self.prompt_template.replace("(question)", question).replace("(searched_context)", data_item)
        
        response = ollama.generate(model=self.model_name, prompt=prompt)
        
        try:
            result = json.loads(response['response'])
            return result
        except json.JSONDecodeError:
            # If the response is not valid JSON, return a default low score
            return {
                "question": question,
                "data_item": data_item,
                "relevance_score": 0
            }
