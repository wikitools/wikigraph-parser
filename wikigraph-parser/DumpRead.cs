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
            CATEGORY,
            PAGELINKS,
            CATEGORYLINKS
        }

        private MainWindow window;
        private int filesCreated = 0;
        private Dictionary<string, string> pageTitles;
        private Dictionary<string, string> categoryTitles;

        public DumpRead(MainWindow window) {
            this.window = window;
            this.pageTitles = new Dictionary<string, string>();
            this.categoryTitles = new Dictionary<string, string>();
        }

        public async Task Start(WikiDump dump, string path) {
            try {
                List<FileInfo> files = new List<FileInfo>();
                window.UpdateProgress(3, 1, "Processed " + filesCreated + " out of 4 files");

                // Page titles
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("page.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreateTitlesMap(files[0], DUMP_TYPE.PAGE);

                // Category titles
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("category.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreateTitlesMap(files[1], DUMP_TYPE.CATEGORY);

                // Titles sorting (for later use)
                /*
                foreach (FileInfo file in files) {
                    string fileNameWithoutExtension = file.FullName.Substring(0, file.FullName.Length - 4);
                    SortFile.SortDelimitedByAlphaNumKey(
                        sourcefilePath: fileNameWithoutExtension + ".map",
                        destinationFolder: file.DirectoryName,
                        delimiter: Delimiters.Tab,
                        keyColumn: 1,
                        keyLength: 0,
                        hasHeader: false,
                        isUniqueKey: false,
                        sortDir: SortDirection.Ascending
                    );
                    // Replace unsorted with sorted ones
                    if (File.Exists(fileNameWithoutExtension + ".map") && File.Exists(fileNameWithoutExtension + "_sorted.map")) {
                        File.Delete(fileNameWithoutExtension + ".map");
                        File.Move(fileNameWithoutExtension + "_sorted.map", fileNameWithoutExtension + ".map");
                    }
                    pageTitlesPath = fileNameWithoutExtension.Contains("page") ? fileNameWithoutExtension + ".map" : pageTitlesPath;
                    categoryTitlesPath = fileNameWithoutExtension.Contains("category") ? fileNameWithoutExtension + ".map" : categoryTitlesPath;
                }
                */

                // Page links
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("pagelinks.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreatePageLinksMap(files[2], DUMP_TYPE.PAGELINKS);
                sortMapnumeric(files[2].FullName.Substring(0, files[2].FullName.Length - 4), files[2].DirectoryName);

                // Category links
                files.Add(new FileInfo(path + Array.Find(dump.Files.ToArray(), (el) => el.Contains("categorylinks.sql")).Replace('/', '\\').Replace(".gz", "")));
                await CreateCategoryLinksMap(files[3], DUMP_TYPE.CATEGORYLINKS);
                sortMapnumeric(files[3].FullName.Substring(0, files[3].FullName.Length - 4), files[3].DirectoryName);

                // Deleting sql files
                foreach (FileInfo file in files) {
                    File.Delete(file.FullName);
                }

            } catch (Exception ex) {
                window.ErrorProgress(2, "Error occured when trying to read dump:\n" + ex.Message, "Dump reading error");
            }
        }

        private void sortMapnumeric(string fileName, string directoryName, int keyColumn = 0) {
            //Sort csv-like map
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
        
        public async Task CreateTitlesMap(FileInfo file, DUMP_TYPE type) {
            // Create map file with category or page titles
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
                            var data = value.Split(',');
                            if(type == DUMP_TYPE.PAGE && data[1] == "0") {
                                pageTitles[data[2].Trim('\'')] = new String(data[0].Where(Char.IsDigit).ToArray());
                                await sw.WriteLineAsync((new String(data[0].Where(Char.IsDigit).ToArray())) + "\t" + data[2].Trim('\''));
                            }
                            if (type == DUMP_TYPE.CATEGORY) {
                                categoryTitles[data[1].Trim('\'')] = new String(data[0].Where(Char.IsDigit).ToArray());
                                await sw.WriteLineAsync((new String(data[0].Where(Char.IsDigit).ToArray())) + "\t" + data[1].Trim('\''));
                            }
                        }
                    }
                }
                window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of 4 files");
            }
        }

        public async Task CreatePageLinksMap(FileInfo file, DUMP_TYPE type) {
            // Create map file with page links
            Dictionary<string, HashSet<string>> pageLinkIndex = new Dictionary<string, HashSet<string>>();
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
                                if (to_page_id != null) {
                                    if (!pageLinkIndex.ContainsKey(from_page_id)) {
                                        pageLinkIndex[from_page_id] = new HashSet<string>();
                                    }
                                    pageLinkIndex[from_page_id].Add(to_page_id);
                                }
                            }
                        }
                    }
                }
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + ".map")) {
                foreach(KeyValuePair<string, HashSet<string>> from_page in pageLinkIndex) {
                    await sw.WriteLineAsync(from_page.Key + "\t" + string.Join(",", from_page.Value));
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of 4 files");
        }

        public async Task CreateCategoryLinksMap(FileInfo file, DUMP_TYPE type) {
            // Create map file with category links
            Dictionary<string, HashSet<string>> categoryLinkIndex = new Dictionary<string, HashSet<string>>();
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
                        var from_category_id = new String(data[0].Where(Char.IsDigit).ToArray());
                        var to_category_title = data[1].Trim('\'');
                        categoryTitles.TryGetValue(to_category_title, out to_category_id);
                        if (to_category_id != null) {
                            if (!categoryLinkIndex.ContainsKey(from_category_id)) {
                                categoryLinkIndex[from_category_id] = new HashSet<string>();
                            }
                            categoryLinkIndex[from_category_id].Add(to_category_id);
                        }
                    }
                }
            }
            Debug.WriteLine("INDEX: " + categoryLinkIndex.Count);
            using (StreamWriter sw = new StreamWriter(file.FullName.Substring(0, file.FullName.Length - 4) + ".map")) {
                foreach (KeyValuePair<string, HashSet<string>> from_category in categoryLinkIndex) {
                    await sw.WriteLineAsync(from_category.Key + "\t" + string.Join(",", from_category.Value));
                }
            }
            window.UpdateProgress(3, 1, "Processed " + (++filesCreated) + " out of 4 files");
        }

    }

}
