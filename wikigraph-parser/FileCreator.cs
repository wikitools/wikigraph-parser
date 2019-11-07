using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace wikigraph_parser {

	class FileCreator {

		private MainWindow window;

		public FileCreator(MainWindow window) {
			this.window = window;
		}

		public async Task Start(WikiDump dump, string path) {
			// Dump name variables: 
			// dump.Name => "simplewiki"
			// dump.Date => "20191101"
			// Example file path of page titles map:
			string pageTitlesMap = path + "\\" + dump.Name + "-" + dump.Date + "-" + "page.map";

			// Progress update: (optional)
			window.UpdateProgress(4, 1, "Processed " + 0 + " out of " + 0 + " files");

			await Task.Delay(1000);
		}

	}

}
