import json
from typing import List
import ollama

class QueryAnalysisAgent:
    
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self.model_name = model_name
        self.prompt_template = self._load_prompt_template()

    def _load_prompt_template(self) -> str:
        with open('prompts/query_analysis_prompt.txt', 'r') as file:
            return file.read()

    def analyze_query(self, user_question: str) -> List[str]:
        prompt = f"{self.prompt_template}\n\nUser Question: \"{user_question}\"\nResponse:"
        
        response = ollama.generate(model=self.model_name, prompt=prompt)
        
        try:
            result = json.loads(response['response'])
            if isinstance(result, list):
                return result
            elif result == "all":
                return ["documentation_db", "code_db", "neo4j"]
            else:
                raise ValueError("Unexpected response format")
        except json.JSONDecodeError:
            # If the response is not valid JSON, return all databases
            return ["documentation_db", "code_db", "neo4j"]

class QueryAnalysisService:
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self.agent = QueryAnalysisAgent(model_name)

    def analyze_query(self, user_question: str) -> dict:
        """
        Analyze the user's question and determine which databases should be queried.

        Args:
            user_question (str): The user's input question

        Returns:
            dict: A dictionary containing the analysis result
        """
        try:
            databases_to_query = self.agent.analyze_query(user_question)
            return {
                "question": user_question,
                "databases_to_query": databases_to_query,
                "success": True
            }
        except Exception as e:
            return {
                "question": user_question,
                "error": str(e),
                "success": False,
                "databases_to_query": ["documentation_db", "code_db", "neo4j"]  # Default to all if there's an error
            }

    def get_available_databases(self) -> List[str]:
        """
        Return a list of available databases that can be queried.

        Returns:
            List[str]: List of available database names
        """
        return ["documentation_db", "code_db", "neo4j"]
