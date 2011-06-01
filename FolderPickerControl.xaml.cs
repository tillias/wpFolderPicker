using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace FolderPickerLib
{
    /// <summary>
    /// Interaction logic for FolderPicker.xaml
    /// </summary>
    public partial class FolderPickerControl : UserControl, INotifyPropertyChanged
    {
        #region Constants

        private static readonly string EmptyItemName = "Empty";

        #endregion

        #region Properties

        public TreeItem Root
        {
            get
            {
                return root;
            }
            private set
            {
                root = value;
                NotifyPropertyChanged(() => Root);
            }
        }

        public TreeItem SelectedItem
        {
            get
            {
                return selectedItem;
            }
            private set
            {
                selectedItem = value;
                NotifyPropertyChanged(() => SelectedItem);
            }
        }

        public string SelectedPath { get; private set; }

        public string InitialPath
        {
            get
            {
                return initialPath;
            }
            set
            {
                initialPath = value;
                UpdateInitialPathUI();
            }
        }

        public Style ItemContainerStyle
        {
            get
            {
                return itemContainerStyle;
            }
            set
            {
                itemContainerStyle = value;
                OnPropertyChanged("ItemContainerStyle");
            }
        }

        #endregion

        public FolderPickerControl()
        {
            InitializeComponent();

            Init();
        }

        public void CreateNewFolder()
        {
            if (SelectedItem == null)
                return;

            string newDirName = "New Directory";

            var parentPath = SelectedItem.GetFullPath();
            var newPath = Path.Combine(parentPath, newDirName);
            SelectedItem.Childs.Add(new TreeItem(newDirName, SelectedItem));

        }

        #region INotifyPropertyChanged Members

        public void NotifyPropertyChanged<TProperty>(Expression<Func<TProperty>> property)
        {
            var lambda = (LambdaExpression)property;
            MemberExpression memberExpression;
            if (lambda.Body is UnaryExpression)
            {
                var unaryExpression = (UnaryExpression)lambda.Body;
                memberExpression = (MemberExpression)unaryExpression.Operand;
            }
            else memberExpression = (MemberExpression)lambda.Body;
            OnPropertyChanged(memberExpression.Member.Name);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Private methods

        private void Init()
        {
            root = new TreeItem("root", null);
            var systemDrives = DriveInfo.GetDrives();

            foreach (var sd in systemDrives)
            {
                var item = new DriveTreeItem(sd.Name, sd.DriveType, root);
                item.Childs.Add(new TreeItem(EmptyItemName, item));

                root.Childs.Add(item);
            }

            Root = root; // to notify UI
        }

        private void TreeView_Selected(object sender, RoutedEventArgs e)
        {
            var tvi = e.OriginalSource as TreeViewItem;
            if (tvi != null)
            {
                SelectedItem = tvi.DataContext as TreeItem;
                SelectedPath = SelectedItem.GetFullPath();
            }
        }

        private void TreeView_Expanded(object sender, RoutedEventArgs e)
        {
            var tvi = e.OriginalSource as TreeViewItem;
            var treeItem = tvi.DataContext as TreeItem;

            if (treeItem != null)
            {
                if (!treeItem.IsFullyLoaded)
                {
                    treeItem.Childs.Clear();

                    string path = treeItem.GetFullPath();

                    DirectoryInfo dir = new DirectoryInfo(path);

                    try
                    {
                        var subDirs = dir.GetDirectories();
                        foreach (var sd in subDirs)
                        {
                            TreeItem item = new TreeItem(sd.Name, treeItem);
                            item.Childs.Add(new TreeItem(EmptyItemName, item));

                            treeItem.Childs.Add(item);
                        }
                    }
                    catch { }

                    treeItem.IsFullyLoaded = true;
                }
            }
            else
                throw new Exception();
        }

        private void UpdateInitialPathUI()
        {
            if (!Directory.Exists(InitialPath))
                return;

            var initialDir = new DirectoryInfo(InitialPath);

            if (!initialDir.Exists)
                return;

            var stack = TraverseUpToRoot(initialDir);
            var containerGenerator = TreeView.ItemContainerGenerator;
            var uiContext = TaskScheduler.FromCurrentSynchronizationContext();
            DirectoryInfo currentDir = null;
            var dirContainer = Root;

            AutoResetEvent waitEvent = new AutoResetEvent(true);

            Task processStackTask = Task.Factory.StartNew(() =>
                {
                    while (stack.Count > 0)
                    {
                        waitEvent.WaitOne();

                        currentDir = stack.Pop();

                        Task waitGeneratorTask = Task.Factory.StartNew(() =>
                        {
                            if (containerGenerator == null)
                                return;

                            while (containerGenerator.Status != GeneratorStatus.ContainersGenerated)
                                Thread.Sleep(50);
                        }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

                        Task updateUiTask = waitGeneratorTask.ContinueWith((r) =>
                        {
                            try
                            {
                                var childItem = dirContainer.Childs.Where(c => c.Name == currentDir.Name).FirstOrDefault();
                                var tvi = containerGenerator.ContainerFromItem(childItem) as TreeViewItem;
                                dirContainer = tvi.DataContext as TreeItem;
                                tvi.IsExpanded = true;

                                tvi.Focus();

                                containerGenerator = tvi.ItemContainerGenerator;
                            }
                            catch { }

                            waitEvent.Set();
                        }, uiContext);
                    }

                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }

        private Stack<DirectoryInfo> TraverseUpToRoot(DirectoryInfo child)
        {
            if (child == null)
                return null;

            if (!child.Exists)
                return null;

            Stack<DirectoryInfo> queue = new Stack<DirectoryInfo>();
            queue.Push(child);
            DirectoryInfo ti = child.Parent;

            while (ti != null)
            {
                queue.Push(ti);
                ti = ti.Parent;
            }

            return queue;
        }

        //private TreeViewItem FindContaier(TreeItem item)
        //{
        //    //create parents queue up to root node
        //    var curItem = item;
        //    var stack = new Stack<TreeItem>();
        //    while (curItem.Parent != null)
        //    {
        //        stack.Push(curItem);
        //        curItem = curItem.Parent;
        //    }

        //    var containerGenerator = TreeView.ItemContainerGenerator;
        //    while (stack.Count > 0)
        //    {
        //        var last = stack.Pop();
        //    }

        //    //containers must be generated before this code executes
        //    var containerGenerator = TreeView.ItemContainerGenerator;


        //    return null;
        //}

        #endregion

        #region Private fields

        private TreeItem root;
        private TreeItem selectedItem;
        private string initialPath;
        private Style itemContainerStyle;

        #endregion
    }

    public class DriveIconConverter : IValueConverter
    {
        private static BitmapImage removable;
        private static BitmapImage drive;
        private static BitmapImage netDrive;
        private static BitmapImage cdrom;
        private static BitmapImage ram;
        private static BitmapImage folder;

        public DriveIconConverter()
        {
            if (removable == null)
                removable = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_8.ico");

            if (drive == null)
                drive = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_9.ico");

            if (netDrive == null)
                netDrive = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_10.ico");

            if (cdrom == null)
                cdrom = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_12.ico");

            if (ram == null)
                ram = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_303.ico");

            if (folder == null)
                folder = CreateImage("pack://application:,,,/FolderPickerLib;component/Images/shell32_264.ico");
        }

        private BitmapImage CreateImage(string uri)
        {
            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(uri);
            img.EndInit();
            return img;
        }

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var treeItem = value as TreeItem;
            if (treeItem == null)
                throw new ArgumentException("Illegal item type");

            if (treeItem is DriveTreeItem)
            {
                DriveTreeItem driveItem = treeItem as DriveTreeItem;
                switch (driveItem.DriveType)
                {
                    case DriveType.CDRom:
                        return cdrom;
                    case DriveType.Fixed:
                        return drive;
                    case DriveType.Network:
                        return netDrive;
                    case DriveType.NoRootDirectory:
                        return drive;
                    case DriveType.Ram:
                        return ram;
                    case DriveType.Removable:
                        return removable;
                    case DriveType.Unknown:
                        return drive;
                }
            }
            else
            {
                return folder;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class NullToBoolConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


}
