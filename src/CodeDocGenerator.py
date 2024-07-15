from typing import List, Dict

from neo4j import GraphDatabase

from agents.CodeDocGenerationAgent import CodeDocGenerationAgent
from agents.FormattingAgent import FormattingAgent
from agents.PseudocodeGenerationAgent import PseudocodeGenerationAgent
from MethodGraphAnalyzer import MethodGraphAnalyzer
from CodeEntity import CodeEntity, MethodEntity, CodeEntityFactory


class CodeDocGenerator:
    '''The class will generate code documentation and pseudocode'''
    
    def __init__(self):
        self._doc_generation_agent = CodeDocGenerationAgent()
        self._pseudocode_agent = PseudocodeGenerationAgent()
        self._format_agent = FormattingAgent()
        self._graph_analyzer = MethodGraphAnalyzer()
        self._load_prompt_templates()

    def _generate_method_docs(self, method: MethodEntity) -> Dict[str, str]:
        # todo: refine the code context
        code_context = f"Method in class {method.namespace}.{method.name}"
        docs = self._doc_generation_agent.generate_docs(
            code_context=code_context,
            code_snippet=method.code_snippet
        )
    
    def _generate_pseudocode(self, method: MethodEntity) -> str:
        # todo: refine the code context
        code_context = f"Method in class {method.namespace}.{method.name}"
        return self._pseudocode_agent.generate_pseudocode(code_context, method.code_snippet)
    
    def generate_codebase_docs(self):
        method_names: List[str] = self._graph_analyzer.generate_topology_order()
        codebase_docs = []
        
        for method_name in method_names:
            with self._graph_analyzer.driver.session() as session:
                result = session.run(
                    "MATCH (m:Method {FullyQualifiedName: $name}) RETURN m",
                    name=method_name
                )
                method_node = result.single()['m']
            
            method_entity = self._entity_factory.create_code_entity_from_node(method_node)
            docs = self._generate_method_docs(method_entity)
            pseudocode = self._generate_pseudocode(method_entity)
            formatted_code = self._format_agent.format_code(docs["code_with_comments"])
            
            method_info = {
                "name": method_entity.name,
                "fully_qualified_name": method_entity.fully_qualified_name,
                "documentation": docs["documentation"],
                "code_with_comments": formatted_code,
                "pseudocode": pseudocode,
                "raw_declaration": method_entity.raw_declaration,
                "accessibility": method_entity.accessibility,
                "is_abstract": method_entity.is_abstract,
                "is_construct": method_entity.is_construct,
                "is_destructor": method_entity.is_destructor,
                "return_type": method_entity.return_type,
                "variable_context": method_entity.variable_context,
                "invoked_context": method_entity.invoked_context,
            }
            
            codebase_docs.append(method_info)
