MSc Computing Project Novem# C# Code Parser

## Build 
This is a parser for C# code. You can use .NET 8.0 or above to build the project.

## Usage
After building the executable file, you can pass a C# solution file as a parameter. Ensure you have a running Neo4j database with no authentication. The parser will analyze the C# code in the solution files and store the results in the Neo4j database.

# Code Knowledge System

## Requirements
Please use Python version >= 3.11.0 and the `requirements.txt` file to install third-party libraries.

## Usage
You can refer to `main.py` to pass appropriate parameters to build and run the knowledge system. The Python program supports the following parameters:
- `generate`
- `embed`
- `run`

You also need to pass a code base path using the `--path` parameter.

Currently, it only supports C# code bases.
ber 2023
