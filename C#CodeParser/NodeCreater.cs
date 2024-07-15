using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using RapidScadaParser.CodeElement;
using RapidScadaParser.CodeElementProcessor;

namespace RapidScadaParser
{
    internal class NodeCreater
    {
        private List<ICodeElementProcessor> m_processors = new List<ICodeElementProcessor>();
        
        private List<AbsCodeElement> m_codeElementNodes = new List<AbsCodeElement>();

        public List<AbsCodeElement> CodeElementNodes
        {
            get
            {
                // Console.WriteLine($"the current size is {m_codeElementNodes.Count}");
                return new List<AbsCodeElement>(m_codeElementNodes);
            }
        }

        public NodeCreater() 
        {
            m_processors.Add(new ClassElementProcessor());
            m_processors.Add(new StructElementProcessor());
            m_processors.Add(new InterfaceElementProcessor());
            m_processors.Add(new EnumElementProcessor());
            m_processors.Add(new DelegateElementProcessor());
            m_processors.Add(new MethodElementProcessor());
            m_processors.Add(new EventElementProcessor());
            m_processors.Add(new FieldElementProcessor());
            m_processors.Add(new PropertyElementProcessor());
        }

        public void CreateNode(SyntaxNode node, SemanticModel model)
        {
            foreach (var processor in m_processors)
            {
                var element = processor.Process(node, model);
                if (element != null)
                {
                    // Console.WriteLine($"process node {node.ToString()}");
                    m_codeElementNodes.Add(element);
                    // Element processed successfully, handle the result
                    break; // Exit the loop once processed
                }
            }

            // Recursively process child nodes
            foreach (var childNode in node.ChildNodes())
            {
                // Console.WriteLine($"child node is {childNode.ToString()}");
                CreateNode(childNode, model);
            }
        }
    }
}
