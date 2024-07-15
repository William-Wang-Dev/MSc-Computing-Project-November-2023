from typing import Dict, Any, List
from agents.Neo4jQueryAgent import Neo4jQueryAgent

class SearchGraphDBEngine:
    def __init__(self, neo4j_uri: str, neo4j_user: str, neo4j_password: str):
        """
        Initialize the SearchGraphDBEngine with Neo4j connection details.

        Args:
            neo4j_uri (str): URI for the Neo4j database
            neo4j_user (str): Username for Neo4j authentication
            neo4j_password (str): Password for Neo4j authentication
        """
        self.query_agent = Neo4jQueryAgent(neo4j_uri, neo4j_user, neo4j_password)

    def search(self, query: str) -> Dict[str, Any]:
        """
        Execute a search query on the graph database.

        Args:
            query (str): The natural language query to execute

        Returns:
            Dict[str, Any]: A dictionary containing the search results and metadata
        """
        try:
            result = self.query_agent.query(query)
            if result["success"]:
                return {
                    "status": "success",
                    "cypher_query": result["query"],
                    "results": self._process_results(result["results"]),
                    "metadata": self._generate_metadata(result)
                }
            else:
                return {
                    "status": "error",
                    "message": f"Query execution failed: {result.get('error', 'Unknown error')}"
                }
        except Exception as e:
            return {
                "status": "error",
                "message": f"An unexpected error occurred: {str(e)}"
            }

    def _process_results(self, results: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Process the raw results from Neo4j query.
        This method can be extended to format or filter results as needed.

        Args:
            results (List[Dict[str, Any]]): Raw results from Neo4j query

        Returns:
            List[Dict[str, Any]]: Processed results
        """
        # For now, we'll just return the raw results
        # You can add processing logic here in the future if needed
        return results

    def _generate_metadata(self, result: Dict[str, Any]) -> Dict[str, Any]:
        """
        Generate metadata about the search results.

        Args:
            result (Dict[str, Any]): The full result from the query agent

        Returns:
            Dict[str, Any]: Metadata about the search results
        """
        return {
            "result_count": len(result["results"]),
            "query_type": self._infer_query_type(result["query"])
            # Add more metadata as needed
        }

    def _infer_query_type(self, cypher_query: str) -> str:
        """
        Infer the type of query based on the Cypher statement.

        Args:
            cypher_query (str): The Cypher query executed

        Returns:
            str: Inferred query type
        """
        cypher_lower = cypher_query.lower()
        if cypher_lower.startswith("match") and "return" in cypher_lower:
            return "READ"
        elif any(keyword in cypher_lower for keyword in ["create", "merge", "set", "delete", "remove"]):
            return "WRITE"
        else:
            return "UNKNOWN"

    def get_schema(self) -> Dict[str, Any]:
        """
        Retrieve the current schema of the graph database.

        Returns:
            Dict[str, Any]: The database schema
        """
        return self.query_agent.schema