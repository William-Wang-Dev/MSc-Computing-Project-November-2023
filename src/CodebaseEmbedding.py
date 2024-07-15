import os
import ollama

from typing import List, Dict, Optional
from neo4j import GraphDatabase
from chromadb import Client, Settings
from chromadb.utils import embedding_functions


class CodeFileEmbedding:
    
    def __init__(self, persistence_directory: str, model_name: str = "jina-embeddings-v2-base-code"):
        self.model_name = model_name
        self.chroma_client = Client(Settings(
            persist_directory=persistence_directory,
            anonymized_telemetry=False
        ))
        self.embedding_function = embedding_functions.OllamaEmbeddingFunction(
            model_name=self.model_name
        )
        self.collection = self.chroma_client.get_or_create_collection(
            name="code_embeddings",
            embedding_function=self.embedding_function
        )

    def embed_codebase(self, codebase_path: str, file_extensions: List[str]) -> Dict[str, any]:
        """
        Embed all files with specified extensions in the given codebase directory and its subdirectories.
        
        Args:
            codebase_path (str): Path to the codebase directory.
            file_extensions (List[str]): List of file extensions to include.
        
        Returns:
            Dict[str, any]: A summary of the embedding process, including:
                - total_files_embedded: Number of files embedded
                - total_embedding_size: Total size of all embeddings
                - embedded_files: List of embedded file paths
                - persistence_path: Path where embeddings are stored
        """
        embedded_files = []
        total_embedding_size = 0

        for root, _, files in os.walk(codebase_path):
            for file in files:
                if any(file.endswith(ext) for ext in file_extensions):
                    file_path = os.path.join(root, file)
                    embedding = self._embed_file(file_path)
                    if embedding:
                        embedded_files.append(file_path)
                        total_embedding_size += len(embedding)

        return {
            "total_files_embedded": len(embedded_files),
            "total_embedding_size": total_embedding_size,
            "embedded_files": embedded_files,
            "persistence_path": self.chroma_client.persist_directory
        }

    def _embed_file(self, file_path: str) -> List[float]:
        """
        Embed a single file and store it in the Chroma collection.
        
        Args:
            file_path (str): Path to the file to be embedded.
        
        Returns:
            List[float]: The generated embedding vector.
        """
        try:
            with open(file_path, 'r', encoding='utf-8') as file:
                content = file.read()

            embedding = ollama.embed(model=self.model_name, prompt=content)['embedding']
            
            file_name = os.path.basename(file_path)
            doc_id = f"file_{file_name}"
            self.collection.upsert(
                ids=[doc_id],
                embeddings=[embedding],
                metadatas=[{"file_name": file_name, "file_path": file_path}],
                documents=[content]
            )

            return embedding
        except Exception as e:
            print(f"Error embedding file {file_path}: {str(e)}")
            return None


class CodeDocEmbedding:
    def __init__(self, neo4j_uri: str, neo4j_user: str, neo4j_password: str, persistence_directory: str):
        self.neo4j_driver = GraphDatabase.driver(neo4j_uri, auth=(neo4j_user, neo4j_password))
        self.model_name = "nomic-embed-text-v1.5"
        self.chroma_client = Client(Settings(
            persist_directory=persistence_directory,
            anonymized_telemetry=False
        ))
        self.embedding_function = embedding_functions.OllamaEmbeddingFunction(
            model_name=self.model_name
        )
        self.collection = self.chroma_client.get_or_create_collection(
            name="code_doc_embeddings",
            embedding_function=self.embedding_function
        )

    def __del__(self):
        if self.neo4j_driver:
            self.neo4j_driver.close()

    def embed_class_documentation(self, class_name: str) -> Dict[str, any]:
        """
        Retrieve documentation and pseudocode for a class from Neo4j,
        generate embeddings, and store them in Chroma.

        Args:
            class_name (str): The fully qualified name of the class.

        Returns:
            Dict[str, any]: A summary of the embedding process, including:
                - class_name: Name of the processed class
                - doc_embedding_size: Size of the documentation embedding
                - pseudo_embedding_size: Size of the pseudocode embedding
                - success: Boolean indicating if the process was successful
        """
        class_data = self._get_class_data_from_neo4j(class_name)
        if not class_data:
            return {"class_name": class_name, "success": False, "error": "Class not found in Neo4j"}

        doc_embedding = self._generate_embedding(class_data['documentation'])
        pseudo_embedding = self._generate_embedding(class_data['pseudocode'])

        if doc_embedding and pseudo_embedding:
            self._store_embeddings(class_name, doc_embedding, pseudo_embedding, class_data)
            return {
                "class_name": class_name,
                "doc_embedding_size": len(doc_embedding),
                "pseudo_embedding_size": len(pseudo_embedding),
                "success": True
            }
        else:
            return {"class_name": class_name, "success": False, "error": "Failed to generate embeddings"}

    def _get_class_data_from_neo4j(self, class_name: str) -> Optional[Dict[str, str]]:
        with self.neo4j_driver.session() as session:
            result = session.run("""
                MATCH (c:Class {FullyQualifiedName: $class_name})
                OPTIONAL MATCH (c)-[:HAS_METHOD]->(m:Method)
                WITH c, COLLECT(m.documentation) AS method_docs, COLLECT(m.pseudo_code) AS method_pseudocodes
                RETURN c.documentation AS class_doc, 
                       method_docs, 
                       method_pseudocodes
            """, class_name=class_name)
            
            record = result.single()
            if record:
                return {
                    "documentation": f"{record['class_doc']}\n" + "\n".join(filter(None, record['method_docs'])),
                    "pseudocode": "\n".join(filter(None, record['method_pseudocodes']))
                }
            return None

    def _generate_embedding(self, text: str) -> Optional[List[float]]:
        try:
            embedding = ollama.embed(model=self.model_name, prompt=text)
            return embedding['embedding']
        except Exception as e:
            print(f"Error generating embedding: {str(e)}")
            return None

    def _store_embeddings(self, class_name: str, doc_embedding: List[float], pseudo_embedding: List[float], class_data: Dict[str, str]):
        self.collection.upsert(
            ids=[f"doc_{class_name}", f"pseudo_{class_name}"],
            embeddings=[doc_embedding, pseudo_embedding],
            metadatas=[
                {"type": "documentation", "class_name": class_name},
                {"type": "pseudocode", "class_name": class_name}
            ],
            documents=[class_data['documentation'], class_data['pseudocode']]
        )

    def embed_project_documentation(self, project_namespace: str) -> Dict[str, any]:
        """
        Embed documentation for all classes within a project namespace.

        Args:
            project_namespace (str): The namespace of the project.

        Returns:
            Dict[str, any]: A summary of the embedding process, including:
                - total_classes_embedded: Number of classes processed
                - successful_embeddings: Number of successful embeddings
                - failed_embeddings: List of classes that failed to embed
        """
        class_names = self._get_project_classes(project_namespace)
        results = {
            "total_classes_embedded": len(class_names),
            "successful_embeddings": 0,
            "failed_embeddings": []
        }

        for class_name in class_names:
            result = self.embed_class_documentation(class_name)
            if result['success']:
                results['successful_embeddings'] += 1
            else:
                results['failed_embeddings'].append(class_name)

        return results

    def _get_project_classes(self, project_namespace: str) -> List[str]:
        with self.neo4j_driver.session() as session:
            result = session.run("""
                MATCH (c:Class)
                WHERE c.FullyQualifiedName STARTS WITH $namespace
                RETURN c.FullyQualifiedName AS class_name
            """, namespace=project_namespace)
            return [record["class_name"] for record in result]
