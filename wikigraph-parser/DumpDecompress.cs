using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wikigraph_parser
{
    class DumpDecompress {

        private MainWindow window;
        private int filesDecompressed = 0;

        public DumpDecompress(MainWindow window) {
            this.window = window;
        }

        public async Task Start(WikiDump dump, string path) {
            try {
                var tasks = new List<Task>();
                window.UpdateProgress(2, 0, "Decompressed " + filesDecompressed + " out of 4 files");
                foreach (string file in dump.Files) {
                    tasks.Add(DecompressGZip(new FileInfo(path + file.Replace('/', '\\'))));
                }
                await Task.WhenAll(tasks.ToArray()).ContinueWith((action) => {
                    // Deleting compressed gz files
                    foreach (string file in dump.Files) {
                        File.Delete(path + file.Replace('/', '\\'));
                    }
                });
            } catch (Exception ex) {
                window.ErrorProgress(2, "Error occured when trying to decompress downloaded dump:\n" + ex.Message, "Dump decompressing error");
            }
        }

        public async Task DecompressGZip(FileInfo fileToDecompress) {
            // Decompress and upate progress
            string currentFileName = fileToDecompress.FullName;
            string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);
            using (var input = File.OpenRead(currentFileName))
            using (var output = File.OpenWrite(newFileName))
            using (var gz = new GZipStream(input, CompressionMode.Decompress)) {
                await gz.CopyToAsync(output);
                window.UpdateProgress(2, 0.25*(++filesDecompressed), "Decompressed " + filesDecompressed + " out of 4 files");
            }
        }

    }
}
