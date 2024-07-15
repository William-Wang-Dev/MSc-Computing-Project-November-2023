from agents import RerankingAgent

class Reranker:
    def __init__(self, model_name: str = "codeqwen:7b-chat-v1.5-q8_0"):
        self._agent = RerankingAgent(model_name)

    def rerank(self, question: str, data_items: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Rerank the given data items based on their relevance to the question.

        Args:
            question (str): The user's question
            data_items (List[Dict[str, Any]]): List of data items to be reranked

        Returns:
            List[Dict[str, Any]]: Reranked list of data items with relevance scores
        """
        reranked_items = []
        for item in data_items:
            content = self._extract_content(item)
            relevance_result = self._agent.evaluate_relevance(question, content)
            
            reranked_item = item.copy()
            reranked_item['relevance_score'] = relevance_result['relevance_score']
            reranked_items.append(reranked_item)

        # Sort the items by relevance score in descending order
        reranked_items.sort(key=lambda x: x['relevance_score'], reverse=True)
        return reranked_items

    def _extract_content(self, item: Dict[str, Any]) -> str:
        """
        Extract the main content from a data item.
        Adjust this method based on the structure of your data items.
        """
        if 'content' in item:
            return item['content']
        elif 'text' in item:
            return item['text']
        else:
            return str(item)  # fallback to string representation
