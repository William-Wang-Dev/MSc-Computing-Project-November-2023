from typing import List, Dict
from chromadb import Client, Settings

class SearchCodeDocEngine:
    def __init__(self, persistence_directory: str):
        self.chroma_client = Client(Settings(
            persist_directory=persistence_directory,
            anonymized_telemetry=False
        ))
        self.collection = self.chroma_client.get_collection("code_doc_embeddings")

    def query_similar_docs(self, query: str, n_results: int = 5) -> List[Dict]:
        results = self.collection.query(
            query_texts=[query],
            n_results=n_results,
            include=["metadatas", "documents", "distances"]
        )

        similar_docs = []
        for i in range(len(results['ids'][0])):
            similar_docs.append({
                "class_name": results['metadatas'][0][i]['class_name'],
                "type": results['metadatas'][0][i]['type'],
                "content": results['documents'][0][i],
                "similarity_score": 1 - results['distances'][0][i]
            })

        return similar_docs
