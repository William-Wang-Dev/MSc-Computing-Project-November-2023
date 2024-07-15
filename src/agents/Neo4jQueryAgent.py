import os
from typing import Dict, Any
from neo4j import GraphDatabase
import ollama


class Neo4jQueryAgent:
    
    def __init__(self, neo4j_uri: str, neo4j_user: str, neo4j_password: str):
        self._neo4j_driver = GraphDatabase.driver(neo4j_uri, auth=(neo4j_user, neo4j_password))
        self._model_name = "codeqwen:7b-chat-v1.5-q8_0"
        self._prompt_template = self._load_prompt_template()
        self._schema = self._load_schema()

    def __del__(self):
        if self._neo4j_driver:
            self._neo4j_driver.close()

    def _load_prompt_template(self) -> str:
        prompt_path = os.path.join(os.path.dirname(__file__), '..', 'prompts', 'cypher_generation_prompt.txt')
        with open(prompt_path, 'r') as file:
            return file.read()

    def _load_schema(self) -> Dict[str, str]:
        with self._neo4j_driver.session() as session:
            # Get node labels and their properties
            node_schema_query = """
            CALL db.schema.nodeTypeProperties()
            YIELD nodeLabels, propertyName, propertyTypes
            RETURN nodeLabels, collect(propertyName + ': ' + propertyTypes[0]) as properties
            """
            node_result = session.run(node_schema_query)
            node_schema = {}
            for record in node_result:
                label = record["nodeLabels"][0]  # Assuming single label per node
                properties = record["properties"]
                node_schema[label] = properties

            # Get relationship types
            rel_schema_query = """
            CALL db.schema.relTypeProperties()
            YIELD relType
            RETURN collect(distinct relType) as relTypes
            """
            rel_result = session.run(rel_schema_query)
            rel_types = rel_result.single()["relTypes"]

        # Format node schema
        property_schema = "\n".join([f"{label} {{{', '.join(props)}}}" for label, props in node_schema.items()])

        # Format relationship schema
        relationship_schema = ", ".join([f"()-[:{rel_type}]->()" for rel_type in rel_types])

        return {
            "property_schema": property_schema,
            "relationship_schema": relationship_schema
        }

    def generate_cypher_query(self, user_question: str) -> str:
        prompt = self._prompt_template.format(
            property_schema=self._schema["property_schema"],
            relationship_schema=self._schema["relationship_schema"],
            graph_question=user_question
        )

        response = ollama.generate(model=self._model_name, prompt=prompt)
        return response['response'].strip()

    def execute_query(self, cypher_query: str) -> List[Dict[str, Any]]:
        with self._neo4j_driver.session() as session:
            result = session.run(cypher_query)
            return [record.data() for record in result]

    def query(self, user_question: str) -> Dict[str, Any]:
        try:
            cypher_query = self.generate_cypher_query(user_question)
            results = self.execute_query(cypher_query)
            return {
                "query": cypher_query,
                "results": results,
                "success": True
            }
        except Exception as e:
            return {
                "error": str(e),
                "success": False
            }
