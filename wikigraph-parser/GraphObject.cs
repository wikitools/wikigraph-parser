using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wikigraph_parser {
	class GraphObject {
		public bool isArticle;
		public int id;
		public string title;
		public int offsetTitle;
		public int offsetGraph;
		public int order;

		public GraphObject() { }
	}
}
