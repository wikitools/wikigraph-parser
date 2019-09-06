using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace wikigraph_parser
{
    class DumpDownload {

        private MainWindow window;
        private string rootURL = "https://dumps.wikimedia.org";
        private Dictionary<string, long> progress = new Dictionary<string, long>();
        private double totalToRecive = 0;

        public DumpDownload(MainWindow window) {
            this.window = window;
        }

        public async Task Start(WikiDump dump, string path) {
            try {
                var tasks = new List<Task>();
                totalToRecive = dump.Size * 1000000;
                foreach (string file in dump.Files) {
                    using (WebClient wc = new WebClient()) {
                        progress.Add(file, 0);
                        tasks.Add(DownloadFile(wc, file, path));
                    }
                }
                await Task.WhenAll(tasks.ToArray());
                window.UpdateProgress(1, 1);
            } catch (WebException ex) {
                window.ErrorProgress(1, "Error occured when trying to download dump from server:\n" + ex.Message, "Dump download error");
            }
        }

        private Task DownloadFile(WebClient wc, string file, string path) {
            // Download and update progress task
            wc.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
            {
                progress[file] = e.BytesReceived;
                if (progress.Sum(x => x.Value) != 0 && totalToRecive != 0) {
                    var progressDone = (double)progress.Sum(x => x.Value) / totalToRecive;
                    window.UpdateProgress(1, progressDone <= 1 ? progressDone : 1);
                }
            };
            Directory.CreateDirectory(path + '\\' + file.Split('/')[1] + '\\' + file.Split('/')[2]);
            return wc.DownloadFileTaskAsync(new System.Uri(this.rootURL + file), path + file.Replace('/', '\\'));
        }
        
    }
}
