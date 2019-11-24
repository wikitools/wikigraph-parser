using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Globalization;
using System.Threading;
using System.Windows.Threading;

namespace wikigraph_parser {

    public class WikiDump {

		public string Name { get; set; }
		public string Date { get; set; }
		public DateTime LastUpdated { get; set; }
        public double Size { get; set; }
        public List<string> Files { get; set; }
        public bool IsReady { get; set; }

        public WikiDump() {
            this.Files = new List<string>();
            this.IsReady = true;
        }

    }

    public partial class MainWindow : Window {

        public bool fetchAllDumpsLoading = false;
        public bool allInputsReady = false;
        public string dumpFetchURL = "http://dumps.wikimedia.your.org/index.json";
        public string[] neededJobs = {"pagetable", "pagelinkstable", "categorylinkstable"};
        public int numberOfFiles = 3;
        public int memoryMultiplier = 10;
        public Stopwatch stopWatch = new Stopwatch();
        public DispatcherTimer dispatcherTimer = new DispatcherTimer();
        public string currentTime = string.Empty;

        private void AnimateLoader(Image image, bool condition) {
            if (condition) {
                RotateTransform rt = new RotateTransform();
                DoubleAnimation iconAnimation = new DoubleAnimation();
                iconAnimation.From = 0;
                iconAnimation.To = 360;
                iconAnimation.Duration = new Duration(TimeSpan.FromSeconds(5));
                iconAnimation.RepeatBehavior = RepeatBehavior.Forever;
                image.RenderTransform = rt;
                rt.BeginAnimation(RotateTransform.AngleProperty, iconAnimation);
            } else {
                var rotateTransform = (RotateTransform)image.RenderTransform;
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }
        }

        private void FetchAllDumpsCompleted(object sender, DownloadStringCompletedEventArgs e) {
            if (e.Error != null)
                return;
            int inProgressCount = 0;
            JObject json = JObject.Parse(e.Result);
            IList<JToken> results = json["wikis"].Children().ToList();
            foreach (JToken result in results) {
                WikiDump dump = new WikiDump();
                dump.Name = ((JProperty)result).Name;
                long fileSize = 0;
                foreach (string job in neededJobs) {
                    if ((string)result.Children().ToList()[0]["jobs"][job]["status"] == "done") {
                        dump.LastUpdated = DateTime.Parse((string)result.Children().ToList()[0]["jobs"][job]["updated"]);
						dump.Date = ((string)result.Children().ToList()[0]["jobs"][job]["files"].First.Children().ToList()[0]["url"]).Substring(dump.Name.Length+2, 8);
						dump.Files.Add((string)result.Children().ToList()[0]["jobs"][job]["files"].First.Children().ToList()[0]["url"]);
                        fileSize += (long)result.Children().ToList()[0]["jobs"][job]["files"].First.Children().ToList()[0]["size"];
                    } else {
                        dump.Name = ((JProperty)result).Name + " (in progress)"; 
                        dump.IsReady = false;
                        inProgressCount++;
                    }
                }
                dump.Size = (double)fileSize/1000000;
                dump_list.Items.Add(dump);
            }
            
            fetching_status.Content = "Fetched " + results.Count + " items.";
            loading_icon.Source = new BitmapImage(new Uri(@"/Assets/ok.png", UriKind.Relative));
            this.fetchAllDumpsLoading = false;
            AnimateLoader(loading_icon, this.fetchAllDumpsLoading);
            if(inProgressCount > 0) {
                MessageBox.Show("Some dumps are being created right now. Try fetching them again in a few hours or choose created one.", "Dumps status");
            }
        }

        private void FetchAllDumps(string url) {
            using (WebClient wc = new WebClient()) {
                this.fetchAllDumpsLoading = true;
                AnimateLoader(loading_icon, this.fetchAllDumpsLoading);
                wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(FetchAllDumpsCompleted);
                wc.DownloadStringAsync(new Uri(url)); 
            }
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            ListViewItem item = sender as ListViewItem;
            if (item != null && item.IsSelected) {
                var size = (((WikiDump)dump_list.SelectedItems[0]).Size*this.memoryMultiplier/1000).ToString("N", CultureInfo.CreateSpecificCulture("pl-PL"));
                memory_hint.Content = "You will need at least "+size+" GB of space";
            } else {
                memory_hint.Content = "";
            }
        }

        public MainWindow() {
            InitializeComponent();
            FetchAllDumps(this.dumpFetchURL);
            dispatcherTimer.Tick += new EventHandler(dt_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
        }

        void dt_Tick(object sender, EventArgs e) {
            if (stopWatch.IsRunning) {
                TimeSpan ts = stopWatch.Elapsed;
                currentTime = String.Format("Time elapsed: {0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                progress_time.Content = currentTime;
            }
        }

        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e) {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null && (string)headerClicked.Column.Header != "#") {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding) {
                    if (headerClicked != _lastHeaderClicked) {
                        direction = ListSortDirection.Ascending;
                    } else {
                        if (_lastDirection == ListSortDirection.Ascending) {
                            direction = ListSortDirection.Descending;
                        } else {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending) {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    } else {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked) {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        
        private void Sort(string sortBy, ListSortDirection direction) {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(dump_list.Items);
            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void Path_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var dialog = new WinForms.FolderBrowserDialog();
            dialog.Description = "Search for WikiGraph build folder.";
            WinForms.DialogResult result = dialog.ShowDialog();
            if (result == WinForms.DialogResult.OK) {
                path.Text = dialog.SelectedPath;
            }
        }
        
        private void BackgroudAnimation(bool show) {
            CircleEase easing = new CircleEase();
            easing.EasingMode = EasingMode.EaseInOut;
            ThicknessAnimation animation = new ThicknessAnimation();
            animation.Duration = TimeSpan.FromSeconds(0.5);
            animation.EasingFunction = easing;
            if(show) {
                animation.To = new Thickness(0, 0, 0, 0);
            } else {
                animation.To = new Thickness(0, -520, 0, 520);
            }
            background.BeginAnimation(Grid.MarginProperty, animation);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void Start_Click(object sender, RoutedEventArgs e) {
            if (!this.fetchAllDumpsLoading && path.Text != "\\" && dump_list.SelectedIndex != -1) {
                this.allInputsReady = true;
                BackgroudAnimation(true);
                StartProcess((WikiDump)dump_list.SelectedItem, path.Text);
            }
        }

        private void ActivateProgressStep(int step) {
            if (step > 1) {
                var progressbar = ((ProgressBar)this.FindName("progress" + (step - 1)));
                if ((string)progressbar.Tag == "indeterminate") {
                    progressbar.IsIndeterminate = false;
                    progressbar.Value = 100;
                }
                ((Label)this.FindName("progress" + (step - 1) + "_hint")).Visibility = Visibility.Collapsed;
                var icon = ((Image)this.FindName("progress" + (step - 1) + "_icon"));
                icon.Source = new BitmapImage(new Uri(@"/Assets/ok-white.png", UriKind.Relative));
                AnimateLoader(icon, false);
            }
            if (step != 6) {
                var icon = ((Image)this.FindName("progress" + step + "_icon"));
                icon.Source = new BitmapImage(new Uri(@"/Assets/circle-dashed-white.png", UriKind.Relative));
                AnimateLoader(icon, true);
            }
            if (step == 6) {
                progress_status.Content = "Parsing data completed!";
            }
        }

        public void ErrorProgress(int step, string message, string caption) {
            var progressbar = ((ProgressBar)this.FindName("progress" + step));
            progressbar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7FFF6A6A"));
            progressbar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF6A6A"));
            stopWatch.Stop();
            MessageBox.Show(message, caption);
            this.Close();
        }

        public void UpdateProgress(int step, double value, string hint=null) {
            var progressbar = ((ProgressBar)this.FindName("progress" + step));
            if ((string)progressbar.Tag == "indeterminate") {
                progressbar.IsIndeterminate = (value == 0) ? false : true;
            } else {
                progressbar.Value = value*100;
            }
            ((Label)this.FindName("progress" + step + "_hint")).Content = (hint != null) ? hint : value.ToString("P");
        }

        private async void StartProcess(WikiDump dump, string path) {
            await Task.Delay(500);
            stopWatch.Start();
            dispatcherTimer.Start();

            // Dump download
            ActivateProgressStep(1);
            DumpDownload dumpDownload = new DumpDownload(this);
            await dumpDownload.Start(dump, path);

            // Dump decompression
            ActivateProgressStep(2);
            DumpDecompress dumpDecompress = new DumpDecompress(this);
            await dumpDecompress.Start(dump, path);

            // Reading dump
            ActivateProgressStep(3);
            DumpRead dumpRead = new DumpRead(this);
            await dumpRead.Start(dump, path);

            // Creating WG files
            ActivateProgressStep(4);
			FileCreator fileCreator = new FileCreator(this);
			await fileCreator.PrepareMaps(dump, path);

			// Finishing up (clean up)
			ActivateProgressStep(5);
			await fileCreator.CreateGraphFiles();

			ActivateProgressStep(6);
            stopWatch.Stop();

        }

    }

}
