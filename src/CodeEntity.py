from dataclasses import dataclass
from typing import List, Dict

@dataclass
class CodeEntity:
    name: str
    entity_type: str
    fully_qualified_name: str
    raw_declaration: str
    label: str
    accessibility: str
    namespace: str
    file_location: List[str]


@dataclass
class ClassEntity(CodeEntity):
    entity_type: str = 'class'
    is_abstract: bool
    is_static: bool
    is_sealed: bool
    members: Dict[str, str] # [fully_qualified_name, variable_name]
    methods: List[str]      # [fully_qualified_name]
    code_docs: str = ''


@dataclass
class InterfaceEntity(CodeEntity):
    entity_type: str = 'interface'
    methods: List[str]       # [fully_qualified_name]


@dataclass
class MethodEntity(CodeEntity):
    entity_type: str = 'method'
    variable_context: Dict[str, str] # [fully_qualified_name, variable_name]
    invoked_context: List[str] # [fully_qualified_name]
    code_snippet: str
    is_destructor: bool
    is_construct: bool
    is_abstract: bool
    return_type: str
    code_docs: str = ''
    pseudo_code: str = ''


@dataclass
class EnumEntity(CodeEntity):
    entity_type: str = 'enum'
    code_snippet: str


class CodeEntityFactory:
    @staticmethod
    def create_code_entity_from_node(node) -> CodeEntity:
        entity_map = {
            'Class': ClassEntity,
            'Interface': InterfaceEntity,
            'Method': MethodEntity,
            'Enum': EnumEntity,
        }
        entity_type = list(node.labels)[0]  # Assume the first label is the entity type
        EntityClass = entity_map.get(entity_type)
        
        if not EntityClass:
            raise ValueError(f"Unknown entity type: [{entity_type}]")
        
        common_attrs = {
            'name': node.get('Name', ''),
            'fully_qualified_name': node.get('FullyQualifiedName', ''),
            'raw_declaration': node.get('RawDeclaration', ''),
            'label': node.get('Label', ''),
            'accessibility': node.get('Accessibility', ''),
            'namespace': node.get('Namespace', ''),
            'file_location': node.get('FileLocation', [])
        }

        # Get the fields of the EntityClass
        entity_fields = {f.name for f in EntityClass.__dataclass_fields__.values()}
        
        # Filter node properties to only include those in the EntityClass
        entity_attrs = {k: v for k, v in node.items() if k in entity_fields}
        
        # Combine common attributes and entity-specific attributes
        combined_attrs = {**common_attrs, **entity_attrs}

        return EntityClass(**combined_attrs)
