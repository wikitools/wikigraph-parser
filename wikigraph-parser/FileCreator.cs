using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace wikigraph_parser {

	class FileCreator {

		private MainWindow window;

		private int BYTES_PER_OBJECT = 12;

		public String outOffsetFileName;
		public String outTitleFileName;
		public String outGraphFileName;
		public String outInfoFileName;
		public String outSortedFileName;
		public string pageTitlesMap;
		string categoryTitlesMap;
		string pageLinksMap;
		string catFromCatMap;
		string catFromPageMap;

		string pathToMaps;

		public int amountOfObjects = 0;
		public int amountOfPages = 0;
		public int currentAmount = 0;

		private int currentTitleOffset = 0;
		private int currentGraphOffset = 0;
		private int[] pageLinksOffset = new int[3] { 0, 0, 0 };

		private Dictionary<int, int> pageDict = new Dictionary<int, int>();
		private Dictionary<int, int> catDict = new Dictionary<int, int>();


		public FileCreator(MainWindow window) {
			this.window = window;
		}

		public async Task PrepareMaps(WikiDump dump, string path) {
			// Dump name variables: 
			// dump.Name => "simplewiki"
			// dump.Date => "20191101"
			this.pathToMaps = path + "\\" + dump.Name + "\\" + dump.Date + "\\";
			this.pageTitlesMap = dump.Name + "-" + dump.Date + "-" + "page.map";
			this.categoryTitlesMap = dump.Name + "-" + dump.Date + "-" + "category.map";
			this.pageLinksMap = dump.Name + "-" + dump.Date + "-" + "pagelinks.map";
			this.catFromCatMap = dump.Name + "-" + dump.Date + "-" + "categorylinksfromcategory.map";
			this.catFromPageMap = dump.Name + "-" + dump.Date + "-" + "categorylinksfrompage.map";

			string extension = ".wg";
			this.outOffsetFileName = pathToMaps + dump.Name + extension + "m";
			this.outTitleFileName = pathToMaps + dump.Name + extension + "t";
			this.outGraphFileName = pathToMaps + dump.Name + extension + "g";
			this.outInfoFileName = pathToMaps + dump.Name + extension + "i";
			this.outSortedFileName = pathToMaps + dump.Name + extension + "s";

			// Progress update:
			window.UpdateProgress(4, 1, "Generating reverse maps: Page Links");
			
			// ZLICZANIE WSZYSTKICH STRON
			using (FileStream fs = File.Open(pathToMaps + pageTitlesMap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					this.amountOfPages += 1;
				}
			}
			// TWORZENIE ODWROTNYCH ODWZOROWAŃ
			await Task.Run( () => createReverseMap(pageLinksMap));
			window.UpdateProgress(4, 1, "Generating reverse maps: Category From Page");
			await Task.Run(() => createReverseMap(catFromPageMap));
			//await createReverseMap(catFromPageMap);
			window.UpdateProgress(4, 1, "Generating reverse maps: Category From Category");
			await Task.Run(() => createReverseMap(catFromCatMap));
			//await createReverseMap(catFromCatMap);
			window.UpdateProgress(4, 1, "Sorting Titles and mapping Wiki IDs");
			await Task.Delay(5000);

			Dictionary<int, string> sortedTitles = new Dictionary<int, string>();
			// Przechodzi po tytułach wszytstkich artykułów, tworzy odwzorowanie ID artykułu na jego miejsce w kolejności
			using (FileStream fs = File.Open(pathToMaps + this.pageTitlesMap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					GraphObject g = new GraphObject();
					g.id = System.Convert.ToInt32(line.Split('\t')[0]);
					g.title = line.Split('\t')[1];
					g.order = this.currentAmount;

					pageDict[g.id] = g.order;
					sortedTitles[g.order] = g.title;

					this.currentAmount += 1;
				}
			}

			// odwzorowanie kategorii
			using (FileStream fs = File.Open(pathToMaps + this.categoryTitlesMap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					GraphObject g = new GraphObject();
					g.id = System.Convert.ToInt32(line.Split('\t')[0]);
					g.title = line.Split('\t')[1];
					g.order = this.currentAmount;

					sortedTitles[g.order] = g.title;
					catDict[g.id] = g.order;

					this.currentAmount += 1;
				}
			}

			// Sortowanie i zapis tytułów
			// PLIK .wgs
			BinaryWriter bwSortedTitles = createNewBinaryFile(this.outSortedFileName);
			List<KeyValuePair<int, string>> sortedTitlesList = sortedTitles.ToList();
			// Zwalnianie pamieci
			sortedTitles.Clear();

			sortedTitlesList.Sort(
				delegate (KeyValuePair<int, string> pair1,
				KeyValuePair<int, string> pair2) {
					return pair1.Value.CompareTo(pair2.Value);
				}
			);
			foreach (var v in sortedTitlesList) {
				bwSortedTitles.Write(Encoding.UTF8.GetBytes(v.Value));
				bwSortedTitles.Write(Encoding.UTF8.GetBytes(";"));
				bwSortedTitles.Write(Encoding.UTF8.GetBytes(v.Key.ToString()));
				bwSortedTitles.Write(Encoding.UTF8.GetBytes("\n"));
			}
			bwSortedTitles.Close();
		}

		public async Task CreateGraphFiles() {
			// Progress update:
			window.UpdateProgress(5, 1, "Parsing page maps");
			// PLIK .wgi
			BinaryWriter bwInfoOffset = createNewBinaryFile(this.outInfoFileName);
			// PLIK .wgm
			BinaryWriter bwStreamOffset = createNewBinaryFile(this.outOffsetFileName);
			// PLIK .wgt
			BinaryWriter bwStreamTitles = createNewBinaryFile(this.outTitleFileName);
			// PLIK .wgg
			BinaryWriter bwStreamGraph = createNewBinaryFile(this.outGraphFileName);

			FileStream[] linkStream_Page = new FileStream[3];
			linkStream_Page[0] = new FileStream(pathToMaps + this.pageLinksMap, FileMode.Open);  // Strona -> Linki w dol (strony)
			linkStream_Page[1] = new FileStream(pathToMaps + "R_" + this.pageLinksMap, FileMode.Open); // Strona -> Linki w gore (strony)
			linkStream_Page[2] = new FileStream(pathToMaps + this.catFromPageMap, FileMode.Open); // Strona -> Powiazane kategorie 


			FileStream[] linkStream_Cat = new FileStream[3];
			linkStream_Cat[0] = new FileStream(pathToMaps + "R_" + this.catFromCatMap, FileMode.Open);  // Kategoria -> Linki w dol (kategorie)
			linkStream_Cat[1] = new FileStream(pathToMaps + this.catFromCatMap, FileMode.Open); // Kategoria -> Linki w gore (kategorie)
			linkStream_Cat[2] = new FileStream(pathToMaps + "R_" + this.catFromPageMap, FileMode.Open); // Kategoria -> Artykuly w kategorii

			this.currentTitleOffset = 0;
			this.currentGraphOffset = 0;
			// Na podstawie wygenerowanych map odwzorowan id na numer porzadkowy tworzymy MAP i GRAPH
			using (FileStream fs = File.Open(pathToMaps + this.pageTitlesMap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					GraphObject g = new GraphObject();
					g.id = System.Convert.ToInt32(line.Split('\t')[0]);
					g.isArticle = true;
					g.title = line.Split('\t')[1];

					await saveGraphObjectInitialParameters(g, bwStreamOffset);
					await saveGraphObjectNeightbours(g, bwStreamGraph, linkStream_Page);
					await saveGraphObjectTitle(g, bwStreamTitles);

					this.currentTitleOffset += Encoding.UTF8.GetBytes(g.title).Length;
					this.amountOfObjects += 1;
				}
			}
			window.UpdateProgress(5, 1, "Parsing category maps");
			// teraz kategorie
			pageLinksOffset = new int[3] { 0, 0, 0 };
			using (FileStream fs = File.Open(pathToMaps + this.categoryTitlesMap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					GraphObject g = new GraphObject();
					g.id = System.Convert.ToInt32(line.Split('\t')[0]);
					g.isArticle = false;
					g.title = line.Split('\t')[1];

					await saveGraphObjectInitialParameters(g, bwStreamOffset);
					await saveGraphObjectNeightbours(g, bwStreamGraph, linkStream_Cat);
					await saveGraphObjectTitle(g, bwStreamTitles);

					this.currentTitleOffset += Encoding.UTF8.GetBytes(g.title).Length;
					this.amountOfObjects += 1;
				}
			}
			bwInfoOffset.Write(BitConverter.GetBytes(this.amountOfPages));
			bwInfoOffset.Close();
			bwStreamGraph.Close();
			bwStreamOffset.Close();
			bwStreamTitles.Close();
			for (int i = 0; i < 3; ++i) {
				linkStream_Page[i].Close();
				linkStream_Cat[i].Close();
			}
			window.UpdateProgress(5, 1, "Removing map files");
			await removeMapFiles();
		}


		private BinaryWriter createNewBinaryFile(String outFileName) {
			return new BinaryWriter(new FileStream(outFileName, FileMode.Create));
		}

		private async Task saveGraphObjectInitialParameters(GraphObject g, BinaryWriter bw) {
			g.offsetGraph = this.currentGraphOffset;
			g.offsetTitle = this.currentTitleOffset;

			bw.Write(BitConverter.GetBytes(g.offsetGraph));
			bw.Write(BitConverter.GetBytes(g.offsetTitle));
			bw.Write(BitConverter.GetBytes(g.id));
		}

		private void appendPagelinkInfo(BinaryWriter bw, string[] upArray, string[] nbArray) {
			byte[] upArr = BitConverter.GetBytes(upArray.Length);
			bw.Write(upArr[0]);
			bw.Write(upArr[1]);
			bw.Write(upArr[2]);
			for (int i = 0; i < upArray.Length; ++i) {
				byte[] nid = BitConverter.GetBytes(Int32.Parse(upArray[i]));
				bw.Write(nid[0]);
				bw.Write(nid[1]);
				bw.Write(nid[2]);
			}
			for (int i = 0; i < nbArray.Length; ++i) {
				byte[] nid = BitConverter.GetBytes(Int32.Parse(nbArray[i]));
				bw.Write(nid[0]);
				bw.Write(nid[1]);
				bw.Write(nid[2]);
			}
		}

		private async Task saveGraphObjectTitle(GraphObject g, BinaryWriter bw) {
			byte[] nodeData = Encoding.UTF8.GetBytes(g.title);
			bw.Write(nodeData);
		}

		private string findOrderOfId(string _id, bool isPage) {
			int id = Int32.Parse(_id);
			if (isPage) return pageDict[id].ToString();
			else return catDict[id].ToString();
		}

		private async Task createReverseMap(string filename) {
			// Bierze plik o strukturze linia = "<ID1>    <link1>,<link2>..."
			// I tworzy plik            linia = "<link1>    <ID1>,<ID2>,<ID3>...

			Dictionary<int, List<string>> reverseMap = new Dictionary<int, List<string>>();

			using (FileStream fs = File.Open(pathToMaps + filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs)) {
				string line;
				while ((line = sr.ReadLine()) != null) {
					String id = line.Split('\t')[0];
					String[] links = (line.Split('\t')[1]).Split(',');
					foreach (String s in links) {
						int castedId = Convert.ToInt32(s);
						if (!reverseMap.ContainsKey(castedId)) reverseMap.Add(castedId, new List<string>());
						reverseMap[castedId].Add(id);
					}
				}
			}
			SortedDictionary<int, List<string>> sortedReverseMap = new SortedDictionary<int, List<string>>(reverseMap);

			var outputFileStream = new FileStream(pathToMaps + "R_" + filename, FileMode.OpenOrCreate, FileAccess.Write);
			outputFileStream.Close();

			using (var sw = new StreamWriter(pathToMaps + "R_" + filename)) {
				foreach (var v in sortedReverseMap) {
					await sw.WriteAsync(v.Key.ToString());
					await sw.WriteAsync("\t");
					foreach (var s in v.Value) {
						var last = v.Value.Last();
						if (!s.Equals(last)) {
							await sw.WriteAsync(s);
							await sw.WriteAsync(",");
						}
						else {
							await sw.WriteAsync(last);
							await sw.WriteAsync("\n");
						}
					}
				}
			}
		}

		private string retrieveConnections(FileStream fs, int fileNumber, int mainId) {
			int count = 0;

			byte[] buffer;
			int readByte;
			fs.Seek(this.pageLinksOffset[fileNumber], SeekOrigin.Begin);
			while (((readByte = fs.ReadByte()) != 0xA) && (readByte != -1)) {
				count++;
			}
			if (count == 0) {
				return "0\t0";
			}

			buffer = new byte[count];
			fs.Seek(this.pageLinksOffset[fileNumber], SeekOrigin.Begin);
			for (int i = 0; i < count; ++i) {
				buffer[i] = Convert.ToByte(fs.ReadByte());
			}

			string line = Encoding.UTF8.GetString(buffer, 0, count);

			string id = line.Split('\t')[0];
			if (mainId == Int32.Parse(id)) {
				this.pageLinksOffset[fileNumber] += count + 1;
			}
			return line;
		}

		private async Task saveGraphObjectNeightbours(GraphObject g, BinaryWriter bw, FileStream[] linkStream) {
			const int BYTES_MAP_ID = 3;
			const int UP_NB_HEADER_BYTES = 3;
			// linkStream[0] - linki w dol
			// linkStream[1] - linki w gore
			// linkStream[2] - powiazane artykuly z kategoria / kategorie do ktorych nalezy artykul
			int upNb = 0, oldUpNb = 0;
			int downNb = 0, oldDownNb = 0;
			string[] nbArray = { };
			string[] upArray = { };
			string[] thirdArray = { };

			string[] lines = new string[3];
			lines[0] = this.retrieveConnections(linkStream[0], 0, g.id);
			lines[1] = this.retrieveConnections(linkStream[1], 1, g.id);
			lines[2] = this.retrieveConnections(linkStream[2], 2, g.id);

			string id;
			id = lines[0].Split('\t')[0];
			if (g.id == Int32.Parse(id)) {
				nbArray = (lines[0].Split('\t')[1]).Split(',');
				downNb = nbArray.Length;
			}
			id = lines[1].Split('\t')[0];
			if (g.id == Int32.Parse(id)) {
				upArray = (lines[1].Split('\t')[1]).Split(',');
				upNb = upArray.Length;
			}
			oldUpNb = upNb;
			oldDownNb = downNb;

			id = lines[2].Split('\t')[0];
			if (g.id == Int32.Parse(id)) {
				thirdArray = (lines[2].Split('\t')[1]).Split(',');
				if (g.isArticle) {
					upNb += thirdArray.Length;
					string[] newArray = new string[upNb];
					Array.Copy(upArray, newArray, upArray.Length);
					Array.Copy(thirdArray, 0, newArray, upArray.Length, thirdArray.Length);
					upArray = newArray;
				}
				else {
					downNb += thirdArray.Length;
					string[] newArray = new string[downNb];
					Array.Copy(nbArray, newArray, nbArray.Length);
					Array.Copy(thirdArray, 0, newArray, nbArray.Length, thirdArray.Length);
					nbArray = newArray;
				}
			}


			string[] nbArrayFixed = new string[downNb];
			string[] upArrayFixed = new string[upNb];

			for (int i = 0; i < downNb; ++i) {
				if (g.isArticle == true) nbArrayFixed[i] = this.findOrderOfId(nbArray[i], true); // Artykuly "w dol" lacza sie tylko z artykulami
				else {
					if (i < oldDownNb) nbArrayFixed[i] = this.findOrderOfId(nbArray[i], false); // Kategorie "w dol" lacza sie z artykulami i kategoriami
					else nbArrayFixed[i] = this.findOrderOfId(nbArray[i], true);
				}
			}
			for (int i = 0; i < upNb; ++i) {
				if (g.isArticle == false) upArrayFixed[i] = this.findOrderOfId(upArray[i], false);// Kategorie "w gore" lacza sie tylko z kategoriami
				else {
					if (i < oldUpNb) upArrayFixed[i] = this.findOrderOfId(upArray[i], true); // Artykuly "w dol" lacza sie z artykulami i kategoriami
					else upArrayFixed[i] = this.findOrderOfId(upArray[i], false);
				}
			}
			this.appendPagelinkInfo(bw, upArrayFixed, nbArrayFixed);
			this.currentGraphOffset += (upNb + downNb) * BYTES_MAP_ID + UP_NB_HEADER_BYTES;

		}

		public async Task removeMapFiles() {
			await DeleteAsync(pathToMaps + pageTitlesMap);
			await DeleteAsync(pathToMaps + categoryTitlesMap);
			await DeleteAsync(pathToMaps + pageLinksMap);
			await DeleteAsync(pathToMaps + catFromCatMap);
			await DeleteAsync(pathToMaps + catFromPageMap);
			await DeleteAsync(pathToMaps + "R_" + pageLinksMap);
			await DeleteAsync(pathToMaps + "R_" + catFromCatMap);
			await DeleteAsync(pathToMaps + "R_" + catFromPageMap);
		}


		private Task DeleteAsync(string filename){
			return Task.Factory.StartNew(() => new FileInfo(filename).Delete());
		}

	}
}