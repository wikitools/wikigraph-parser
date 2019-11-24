using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sortiously;
using static Sortiously.Framework.Constants;

namespace wikigraph_parser {

    class DumpRead {

        public enum DUMP_TYPE {
            PAGE,
            PAGELINKS,
            CATEGORYLINKS
        }

        private MainWindow window;
        private int numberOfFiles = 5;
        private int filesCreated = 0;
		private Dictionary<string, string> pageTitles;
		private Dictionary<string, string> categoryTitles;
		private Dictionary<string, bool> pageIDs;
		private Dictionary<string, bool> categoryIDs;

		public DumpRead(MainWindow window) {
            this.window = window;
			this.pageTitles = new Dictionary<string, string>();
			this.categoryTitles = new Dictionary<string, string>();
			this.pageIDs = new Dictionary<string, bool>();
			this.categoryIDs = new Dictionary<string, bool>();
		}

        public async Task Start(WikiDump dump, string path) {
            try {
                List<FileInfo> files = new List<FileInfo>();
                window.UpdateProgress(3, 1, "Processed " + filesCreated + " out of " + numberOfFiles + " files");

                // Page and category titles
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("page.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreateTitlesMaps(files[0]);
                await sortMapnumeric(files[0].FullName.Substring(0, files[0].FullName.Length - 4), files[0].DirectoryName);
                await sortMapnumeric(files[0].FullName.Substring(0, files[0].FullName.Length - 8) + "category", files[0].DirectoryName);

				// Titles sorting (for search use)
				//await sortTitles(files[0].FullName.Substring(0, files[0].FullName.Length - 4), files[0].DirectoryName);
				//await sortTitles(files[0].FullName.Substring(0, files[0].FullName.Length - 8) + "category", files[0].DirectoryName);

				// Page to page links
				files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("pagelinks.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreatePageLinksMap(files[1], DUMP_TYPE.PAGELINKS);
                await sortMapnumeric(files[1].FullName.Substring(0, files[1].FullName.Length - 4), files[1].DirectoryName);

                // Category links
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("categorylinks.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreateCategoryLinksMap(files[2], DUMP_TYPE.CATEGORYLINKS);
                await sortMapnumeric(files[2].FullName.Substring(0, files[2].FullName.Length - 4) + "frompage", files[2].DirectoryName);
                await sortMapnumeric(files[2].FullName.Substring(0, files[2].FullName.Length - 4) + "fromcategory", files[2].DirectoryName);

                // Deleting sql files
                foreach (FileInfo file in files) {
                    File.Delete(file.FullName);
                }

            } catch (Exception ex) {
                window.ErrorProgress(3, "Error occured when trying to read dump:\n" + ex.Message, "Dump reading error");
            }
        }

		private async Task sortTitles(string fileName, string directoryName) {
			// Create sorted txt file for node search
			SortFile.SortDelimitedByAlphaNumKey(
				sourcefilePath: fileName + ".map",
				destinationFolder: directoryName,
				delimiter: Delimiters.Tab,
				keyColumn: 1,
				keyLength: 0,
				hasHeader: false,
				isUniqueKey: false,
				sortDir: SortDirection.Ascending
			);
			// Renaming sorted file
			if (File.Exists(fileName + "_sorted.map")) {
				File.Move(fileName + "_sorted.map", fileName + "-search.map");
			}
		}

		private async Task sortMapnumeric(string fileName, string directoryName, int keyColumn = 0) {
            // Sort csv-like map
            SortFile.SortDelimitedByNumericKey(
                sourcefilePath: fileName + ".map",
                destinationFolder: directoryName,
                delimiter: Delimiters.Tab,
                keyColumn: keyColumn,
                hasHeader: false,
                isUniqueKey: false,
                sortDir: SortDirection.Ascending
            );
            // Replace unsorted with sorted ones
            if (File.Exists(fileName + ".map") && File.Exists(fileName + "_sorted.map")) {
                File.Delete(fileName + ".map");
                File.Move(fileName + "_sorted.map", fileName + ".map");
            }
        }
        
        public async Task CreateTitlesMaps(FileInfo file) {
            // Create map files with category and page titles
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4 ) + ".map")) {
                using (StreamReader sr = new StreamReader(file.FullName)) {
                    while (sr.Peek() >= 0) {
                        var line = await sr.ReadLineAsync();
                        if (!line.StartsWith("INSERT INTO") || line.Split(' ').Length != 5) {
                            continue;
                        }
                        string[] separatingStrings = { "),(" };
                        var inserts = string.Join("", line.Split(' ').Skip(4));
                        var values = inserts.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (string value in values) {

							string title;
							var data = value.Split(',');
							if(data.Length > 13) {
								title = "";
								for(int j = 2; j < data.Length-10; j++) {
									title += data[j] + (j != data.Length - 11 ? "," : "");
								}
								title = title.Trim('\'');
							} else {
								title = data[2].Trim('\'');
							}
							if (data[1] == "0") {
                                pageTitles[title] = new String(data[0].Where(Char.IsDigit).ToArray());
								pageIDs[new String(data[0].Where(Char.IsDigit).ToArray())] = true;
							}
                            if (data[1] == "14") {
                                categoryTitles[title] = new String(data[0].Where(Char.IsDigit).ToArray());
								categoryIDs[new String(data[0].Where(Char.IsDigit).ToArray())] = true;
							}
                        }
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + ".map")) {
                foreach (KeyValuePair<string, string> data in pageTitles) {
                    await sw.WriteLineAsync(data.Value + "\t" + data.Key);
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of " + numberOfFiles + " files");
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 8) + "category.map")) {
                foreach (KeyValuePair<string, string> data in categoryTitles) {
                    await sw.WriteLineAsync(data.Value + "\t" + data.Key);
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of " + numberOfFiles + " files");
        }

        public async Task CreatePageLinksMap(FileInfo file, DUMP_TYPE type) {
            // Create map file with page links
            Dictionary<string, HashSet<string>> pageToPageLinkIndex = new Dictionary<string, HashSet<string>>();
            using (StreamReader sr = new StreamReader(file.FullName)) {
                    while (sr.Peek() >= 0) {
                        var line = await sr.ReadLineAsync();
                        if (!line.StartsWith("INSERT INTO") || line.Split(' ').Length != 5) {
                            continue;
                        }
                        string[] separatingStrings = { "),(" };
                        var inserts = string.Join("", line.Split(' ').Skip(4));
                        var values = inserts.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                        string to_page_id;
                        foreach (string value in values) {
                            var data = value.Split(',');
                            var from_page_id = new String(data[0].Where(Char.IsDigit).ToArray());
                            var to_page_title = data[2].Trim('\'');
                            if (data[1] == "0" && data[3] == "0") {
                                pageTitles.TryGetValue(to_page_title, out to_page_id);
                                if (to_page_id != null && pageIDs.ContainsKey(from_page_id)) {
                                    if (!pageToPageLinkIndex.ContainsKey(from_page_id)) {
                                        pageToPageLinkIndex[from_page_id] = new HashSet<string>();
                                    }
                                    pageToPageLinkIndex[from_page_id].Add(to_page_id);
                                }
                            }
                        }
                    }
            }
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + ".map")) {
                foreach (KeyValuePair<string, HashSet<string>> from_page in pageToPageLinkIndex) {
                    await sw.WriteLineAsync(from_page.Key + "\t" + string.Join(",", from_page.Value));
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of " + numberOfFiles + " files");
        }

        public async Task CreateCategoryLinksMap(FileInfo file, DUMP_TYPE type) {
            // Create map file with category links
            Dictionary<string, HashSet<string>> pageToCategoryLinkIndex = new Dictionary<string, HashSet<string>>();
            Dictionary<string, HashSet<string>> categoryToCategoryLinkIndex = new Dictionary<string, HashSet<string>>();
            using (StreamReader sr = new StreamReader(file.FullName)) {
                while (sr.Peek() >= 0) {
                    var line = await sr.ReadLineAsync();
                    if (!line.StartsWith("INSERT INTO") || line.Split(' ').Length < 5) {
                        continue;
                    }
                    string[] separatingStrings = { "),(" };
                    var inserts = string.Join("", line.Split(' ').Skip(4));
                    var values = inserts.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                    string to_category_id;
                    foreach (string value in values) {
                        var data = value.Split(',');
                        var from_id = new String(data[0].Where(Char.IsDigit).ToArray());
                        var to_category_title = data[1].Trim('\'');
                        categoryTitles.TryGetValue(to_category_title, out to_category_id);
                        if (to_category_id != null) {
                            if (pageIDs.ContainsKey(from_id)) {
                                if (!pageToCategoryLinkIndex.ContainsKey(from_id)) {
                                    pageToCategoryLinkIndex[from_id] = new HashSet<string>();
                                }
                                pageToCategoryLinkIndex[from_id].Add(to_category_id);
                            }
                            if (categoryIDs.ContainsKey(from_id)) {
                                if (!categoryToCategoryLinkIndex.ContainsKey(from_id)) {
                                    categoryToCategoryLinkIndex[from_id] = new HashSet<string>();
                                }
                                categoryToCategoryLinkIndex[from_id].Add(to_category_id);
                            }
                        }
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + "frompage.map")) {
                foreach (KeyValuePair<string, HashSet<string>> from_page in pageToCategoryLinkIndex) {
                    await sw.WriteLineAsync(from_page.Key + "\t" + string.Join(",", from_page.Value));
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of " + numberOfFiles + " files");
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + "fromcategory.map")) {
                foreach (KeyValuePair<string, HashSet<string>> from_category in categoryToCategoryLinkIndex) {
                    await sw.WriteLineAsync(from_category.Key + "\t" + string.Join(",", from_category.Value));
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of " + numberOfFiles + " files");
        }

    }

}
