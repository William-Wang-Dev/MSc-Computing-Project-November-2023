using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElement
{
    internal interface ICodeElement
    {
        public string ToStr();

        public string ToCypherCreateNode();

        public List<string> ToCypherCreateRelationships();
    }
}
