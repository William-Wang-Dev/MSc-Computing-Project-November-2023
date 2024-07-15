from typing import List, Dict, Any
from query_analysis_service import QueryAnalysisService
from search_engines.search_code_engine import SearchCodeEngine
from search_engines.search_code_doc_engine import SearchCodeDocEngine
from search_engines.search_graph_db_engine import SearchGraphDBEngine
from reranking_engine import RerankingEngine

class QueryService:
    def __init__(self):
        self.query_analysis_service = QueryAnalysisService()
        self.code_search_engine = SearchCodeEngine()
        self.doc_search_engine = SearchCodeDocEngine()
        self.graph_db_search_engine = SearchGraphDBEngine()
        self.reranking_engine = RerankingEngine()

    def process_query(self, user_question: str) -> Dict[str, Any]:
        """
        Process a user query by analyzing it, searching relevant databases, and reranking results.

        Args:
            user_question (str): The user's input question

        Returns:
            Dict[str, Any]: A dictionary containing the query results and metadata
        """
        # Analyze the query
        analysis_result = self.query_analysis_service.analyze_query(user_question)
        
        if not analysis_result["success"]:
            return {"error": "Failed to analyze query", "details": analysis_result["error"]}

        # Perform searches based on the analysis
        search_results = self._perform_searches(user_question, analysis_result["databases_to_query"])

        # Combine and rerank results
        combined_results = self._combine_results(search_results)
        reranked_results = self.reranking_engine.rerank(user_question, combined_results)

        return {
            "question": user_question,
            "analyzed_databases": analysis_result["databases_to_query"],
            "results": reranked_results,
            "total_results": len(reranked_results)
        }

    def _perform_searches(self, question: str, databases: List[str]) -> Dict[str, List[Dict[str, Any]]]:
        """
        Perform searches across specified databases.

        Args:
            question (str): The user's question
            databases (List[str]): List of databases to search

        Returns:
            Dict[str, List[Dict[str, Any]]]: Search results for each database
        """
        search_results = {}
        
        if "code_db" in databases:
            search_results["code_db"] = self.code_search_engine.search(question)
        
        if "documentation_db" in databases:
            search_results["documentation_db"] = self.doc_search_engine.search(question)
        
        if "neo4j" in databases:
            search_results["neo4j"] = self.graph_db_search_engine.search(question)

        return search_results

    def _combine_results(self, search_results: Dict[str, List[Dict[str, Any]]]) -> List[Dict[str, Any]]:
        """
        Combine results from different databases into a single list.

        Args:
            search_results (Dict[str, List[Dict[str, Any]]]): Search results for each database

        Returns:
            List[Dict[str, Any]]: Combined list of results
        """
        combined = []
        for db_name, results in search_results.items():
            for result in results:
                result["source_db"] = db_name
                combined.append(result)
        return combined
