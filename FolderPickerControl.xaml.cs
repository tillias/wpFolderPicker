using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
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

        #endregion

        public FolderPickerControl()
        {
            InitializeComponent();

            Init();
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

        #endregion

        #region Private fields

        private TreeItem root;
        private TreeItem selectedItem;

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

}
