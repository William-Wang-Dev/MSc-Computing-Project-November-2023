import os
from typing import List, Dict, Any

import ollama


class AnswerGenerationAgent:
    
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self.model_name = model_name
        self.prompt_template = self._load_prompt_template()

    def _load_prompt_template(self) -> str:
        prompt_path = os.path.join(os.path.dirname(__file__), '..', 'prompts', 'answer_generation_prompt.txt')
        with open(prompt_path, 'r') as file:
            return file.read()

    def generate_answer(self, question: str, context: str) -> str:
        prompt = self.prompt_template.format(question=question, context=context)
        response = ollama.generate(model=self.model_name, prompt=prompt)
        return response['response']

class AnswerGenerationService:
    def __init__(self):
        self.agent = AnswerGenerationAgent()

    def generate_answer(self, question: str, search_results: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Generate an answer based on the question and search results.

        Args:
            question (str): The user's question
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            Dict[str, Any]: A dictionary containing the generated answer and metadata
        """
        context = self._format_context(search_results)
        answer = self.agent.generate_answer(question, context)

        return {
            "question": question,
            "answer": answer,
            "sources": self._extract_sources(search_results)
        }

    def _format_context(self, search_results: List[Dict[str, Any]]) -> str:
        """
        Format the search results into a context string for the AI model.

        Args:
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            str: Formatted context string
        """
        context_parts = []
        for result in search_results[:5]:  # Use top 5 results
            content = result.get('content', result.get('text', 'No content available'))
            source = result.get('source_db', 'Unknown source')
            context_parts.append(f"Source: {source}\nContent: {content}\n")
        
        return "\n".join(context_parts)

    def _extract_sources(self, search_results: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Extract source information from search results.

        Args:
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            List[Dict[str, Any]]: List of source information
        """
        sources = []
        for result in search_results[:5]:  # Use top 5 results
            source = {
                "db": result.get('source_db', 'Unknown'),
                "title": result.get('title', 'Untitled'),
                "relevance_score": result.get('relevance_score', 0)
            }
            sources.append(source)
        return sources
