import os
from collections import OrderedDict
from neo4j import GraphDatabase

class MethodGraphAnalyzer:
    
    def __init__(self):
        self.driver = GraphDatabase.driver(os.getenv('NEO4J_DATABASE_HOST'), auth=(os.getenv('NOE4J_DATABASE_USER'), os.getenv('NOE4J_DATABASE_PW')))
        self._create_method_graph()
    
    def __del__(self):
        if self.driver:
            self.driver.close()
            self.driver = None

    def _create_method_graph(self):
        create_graph_cypher = '''
        CALL gds.graph.project.cypher(
          'methodGraph',
          'MATCH (m:Method) RETURN id(m) AS id',
          'MATCH (m1:Method)-[:INVOKES]->(m2:Method) RETURN id(m1) AS source, id(m2) AS target')
        '''
        with self.driver.session() as session:
            session.run(create_graph_cypher)
            print("Method graph created successfully.")

    def generate_topology_order(self) -> list[str]:
        generate_topology_order_cypher = '''
        CALL gds.dag.topologicalSort.stream('methodGraph')
        YIELD nodeId
        RETURN gds.util.asNode(nodeId).FullyQualifiedName AS methodName
        ORDER BY nodeId
        '''
        with self.driver.session() as session:
            result = session.run(generate_topology_order_cypher)
            method_names = OrderedDict((record["methodName"], None) for record in result)
            return list(method_names.keys())
