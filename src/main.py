import argparse
import sys
from dotenv import load_dotenv
import os

# Import necessary components
from CodeDocGenerator import CodeDocGenerator
from CodebaseEmbedding import CodeFileEmbedding, CodeTextEmbedding
from QueryService import QueryService
from agents.QueryAnalysisAgent import QueryAnalysisAgent
from searchEngine.SearchCodeEngine import SearchCodeEngine
from searchEngine.SearchCodeDocEngine import SearchCodeDocEngine
from searchEngine.SearchGraphDBEngine import SearchGraphDBEngine
from Reranker import RerankingEngine
from agents.BusinessDeterminerAgent import BusinessDeterminerAgent

def generate_knowledge(codebase_path):
    print("Generating knowledge from codebase...")
    doc_generator = CodeDocGenerator()
    doc_generator.generate_codebase_docs(codebase_path)
    print("Knowledge generation complete.")

def embed_knowledge(codebase_path):
    print("Embedding knowledge...")
    code_embedder = CodeFileEmbedding("./embeddings/code")
    doc_embedder = CodeTextEmbedding("./embeddings/docs")
    
    code_embedder.embed_directory(codebase_path)
    # Assuming doc_embedder needs to embed from a specific location
    doc_embedder.embed_directory("./generated_docs")
    
    print("Knowledge embedding complete.")

def run_query_service():
    print("Starting query service...")
    query_service = QueryService()
    
    while True:
        user_input = input("Enter your question (or 'exit' to quit): ")
        if user_input.lower() == 'exit':
            break
        
        result = query_service.process_query(user_input)
        print(f"\nAnswer: {result['answer']}")
        print("\nSources:")
        for source in result['sources']:
            print(f"- [{source['db']}] {source['title']} (Relevance: {source['relevance_score']})")
        print("\n")

    print("Query service stopped.")

def main():
    load_dotenv()  # Load environment variables from .env file
    
    parser = argparse.ArgumentParser(description="Codebase Knowledge System")
    parser.add_argument("mode", choices=['generate', 'embed', 'run'], 
                        help="Mode of operation: generate knowledge, embed knowledge, or run query service")
    parser.add_argument("--path", help="Path to the codebase (required for generate and embed modes)")
    
    args = parser.parse_args()

    if args.mode in ['generate', 'embed'] and not args.path:
        print("Error: --path argument is required for generate and embed modes")
        sys.exit(1)

    if args.mode == 'generate':
        generate_knowledge(args.path)
    elif args.mode == 'embed':
        embed_knowledge(args.path)
    else:  # run mode
        run_query_service()


if __name__ == "__main__":
    main()