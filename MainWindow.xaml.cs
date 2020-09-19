using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DriveSpaceAnalyzer_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer progressTimer;
        private readonly BackgroundWorker worker = new BackgroundWorker();
        public MainWindow()
        {
            InitializeComponent();

            EnumerateDrives();
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            progressTimer = new DispatcherTimer();
            progressTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            progressTimer.Tick += progressTimer_Tick;
            progressTimer.Start();
        }

        struct FolderStatus {
            public string Path { get; set; }
            public bool Expanded { get; set; }
            public FolderStatus(string path, bool expanded)
            {
                Path = path;
                Expanded = expanded;
            }
        }

        struct ResultEntry
        {
            public string Path { get; set; }
            public long Size { get; set; }

            public string SizeType { get; set; }

            public ResultEntry(string path, long size)
            {
                Path = path;
                Size = (long)((double)size / 1024.0 / 1024.0);
                SizeType = "MB";
            }
        }

        private List<ResultEntry> resultList;
        private List<string> pathQueue = new List<string>();
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            resultList = new List<ResultEntry>();
            while (pathQueue.Count > 0)
            {
                var path = pathQueue.First();
                foreach (var d in new DirectoryInfo(path).EnumerateDirectories())
                {
                    var size = DirSize(d);
                    resultList.Add(new ResultEntry(d.FullName, size));
                }
                pathQueue.Remove(path);
            }
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            foreach (var result in resultList)
            {
                var item = GetInResults(result.Path);
                if (item!=null)
                {
                    results.Items.Remove(item);
                }
                results.Items.Add(result);
            }
            resultList.Clear();
        }

        private void progressTimer_Tick(object sender, EventArgs args)
        {
            if (pathQueue.Count > 0 && progress.Visibility == Visibility.Hidden)
            {
                progress.Visibility = Visibility.Visible;
            }
            if (pathQueue.Count == 0 && progress.Visibility == Visibility.Visible)
            {
                progress.Visibility = Visibility.Hidden;
            }
            progress.Value++;
            if (progress.Value >= progress.Maximum)
                progress.Value = 0;
        }

        object GetInResults(string path)
        {
            foreach (ResultEntry row in results.Items)
            {
                if (row.Path == path)
                    return row;
            }
            return null;
        }

        void EnumerateDrives()
        {
            //enumerate drives
            var drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                var f = FindRootDriveInTree(d.Name);
                if (f==null)
                {
                    var t = new TreeViewItem();
                    t.Header = d.Name;
                    t.Tag = new FolderStatus(d.Name, false);
                    folders.Items.Add(t);

                    AddFolderChildren(d.Name, t);
                    t.IsExpanded = true;
                }
            }
        }

        TreeViewItem FindRootDriveInTree(string path)
        {
            foreach (TreeViewItem t in folders.Items)
            {
                if (((FolderStatus)t.Tag).Path == path)
                {
                    return t;
                }
            }
            return null;
        }

        void folders_Selected(object sender, RoutedEventArgs args)
        {
            TreeViewItem t = (TreeViewItem)folders.SelectedItem;
            AddFolderChildren(((FolderStatus)t.Tag).Path, t);
            //t.IsExpanded = true;
        }

        void AddFolderChildren(string path, TreeViewItem parent, bool stop = false)
        {
            var status = (FolderStatus)parent.Tag;
            if (!status.Expanded)
            {
                status.Expanded = true;
                parent.Tag = status;
            } 
            else
            {
                return;
            }

            try
            {
                var d = new DirectoryInfo(path);
            
                foreach (var sub in d.EnumerateDirectories())
                {
                    var t = new TreeViewItem();
                    t.Header = sub.Name;
                    t.Tag = new FolderStatus(sub.FullName, false);
                    t.FontWeight = FontWeight.FromOpenTypeWeight(400);
                    parent.Items.Add(t);

                    if (!stop)
                        AddFolderChildren(sub.FullName, t, true);
                }
            } 
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        private void btnCalculate_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem t = (TreeViewItem)folders.SelectedItem;
            if (t != null)
            {
                var status = (FolderStatus)t.Tag;
                if (!pathQueue.Contains(status.Path))
                    pathQueue.Add(status.Path);
                t.FontWeight = FontWeight.FromOpenTypeWeight(800);
                if (!worker.IsBusy)
                {
                    worker.RunWorkerAsync();
                }
            }
        }

        public long DirSize(DirectoryInfo d)
        {
            long size = 0;
            try
            {
                //Add file sizes.
                FileInfo[] fis = d.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }

                //Add subdirectory sizes.
                DirectoryInfo[] dis = d.GetDirectories();
                foreach (DirectoryInfo di in dis)
                    size += DirSize(di);
            }
            catch (Exception e) { 
                Debug.WriteLine(e.ToString()); //usually permissions issue. todo: alert user that some files couldnt be accessed
            }
            return size;
        }

        private void results_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (results.SelectedCells.Count > 0)
            {
                Clipboard.SetText(((ResultEntry)results.SelectedCells[0].Item).Path);
            }
        }

        private void folders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            btnCalculate_Click(sender, e);
        }
    }
}
