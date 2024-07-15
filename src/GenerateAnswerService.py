from typing import List, Dict, Any
from agents import AnswerGenerationAgent

class GenerateAnswerService:
    
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self.agent = AnswerGenerationAgent(model_name)

    def generate_answer(self, question: str, search_results: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Generate an answer based on the question and search results.

        Args:
            question (str): The user's question
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            Dict[str, Any]: A dictionary containing the generated answer and metadata
        """
        try:
            context = self._prepare_context(search_results)
            answer = self.agent.generate_answer(question, context)
            
            return {
                "question": question,
                "answer": answer,
                "sources": self._extract_sources(search_results),
                "success": True
            }
        except Exception as e:
            return {
                "question": question,
                "error": str(e),
                "success": False
            }

    def _prepare_context(self, search_results: List[Dict[str, Any]]) -> str:
        """
        Prepare the context for the AI model based on search results.

        Args:
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            str: Formatted context string
        """
        context_parts = []
        for idx, result in enumerate(search_results[:5], 1):  # Use top 5 results
            content = result.get('content', result.get('text', 'No content available'))
            source = result.get('source_db', 'Unknown source')
            context_parts.append(f"[Source {idx}: {source}]\n{content}\n")
        
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

    def validate_answer(self, question: str, answer: str, search_results: List[Dict[str, Any]]) -> bool:
        """
        Validate the generated answer against the search results.
        This is a placeholder method and should be implemented based on specific requirements.

        Args:
            question (str): The user's question
            answer (str): The generated answer
            search_results (List[Dict[str, Any]]): Reranked search results

        Returns:
            bool: True if the answer is valid, False otherwise
        """
        # Implement validation logic here
        # For now, we'll assume all answers are valid
        return True

    def format_answer(self, answer: str) -> str:
        """
        Format the generated answer for better readability.
        This is a placeholder method and can be expanded based on specific formatting needs.

        Args:
            answer (str): The generated answer

        Returns:
            str: Formatted answer
        """
        # Implement formatting logic here
        # For now, we'll just return the original answer
        return answer
