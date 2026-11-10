using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ProjectDesk
{
    internal sealed class ProjectDeskControl : UserControl
    {
        private ProjectDeskPackage _package;
        private readonly TextBox _pathBox;
        private readonly ListBox _entries;
        private readonly TextBlock _status;
        private string _rootPath;
        private string _currentPath;
        private bool _initialized;

        public ProjectDeskControl()
        {
            var layout = new DockPanel { Margin = new Thickness(12) };
            var header = new StackPanel();
            DockPanel.SetDock(header, Dock.Top);

            var projectActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            projectActions.Children.Add(CreateButton("Open solution", OpenSolution));
            projectActions.Children.Add(CreateButton("Create project", CreateProject));
            projectActions.Children.Add(CreateButton("Build solution", BuildSolution));
            header.Children.Add(projectActions);

            var pathRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var browse = CreateButton("Choose folder", ChooseFolder);
            DockPanel.SetDock(browse, Dock.Right);
            pathRow.Children.Add(browse);
            _pathBox = new TextBox { IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center };
            pathRow.Children.Add(_pathBox);
            header.Children.Add(pathRow);

            var fileActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            fileActions.Children.Add(CreateButton("Up", GoUp));
            fileActions.Children.Add(CreateButton("New file", CreateFile));
            fileActions.Children.Add(CreateButton("New folder", CreateFolder));
            fileActions.Children.Add(CreateButton("Rename", RenameEntry));
            fileActions.Children.Add(CreateButton("Delete", DeleteEntry));
            header.Children.Add(fileActions);

            layout.Children.Add(header);
            _status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(_status, Dock.Bottom);
            layout.Children.Add(_status);

            _entries = new ListBox { DisplayMemberPath = nameof(FileEntry.DisplayName) };
            _entries.MouseDoubleClick += OpenOrEnterSelected;
            layout.Children.Add(_entries);
            Content = layout;
        }

        internal void SetPackage(ProjectDeskPackage package)
        {
            _package = package;
            if (!_initialized)
            {
                _initialized = true;
                _ = InitializeAsync();
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                var dte = await _package.GetDteAsync();
                if (!string.IsNullOrWhiteSpace(dte.Solution.FullName))
                {
                    SetRoot(Path.GetDirectoryName(dte.Solution.FullName));
                }
                else
                {
                    SetStatus("Open a solution or choose a folder to get started.");
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        }

        private static Button CreateButton(string label, Action action)
        {
            var button = new Button
            {
                Content = label,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            button.Click += (_, __) => action();
            return button;
        }

        private async void OpenSolution()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a Visual Studio solution",
                Filter = "Visual Studio solutions (*.sln;*.slnx)|*.sln;*.slnx",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var dte = await _package.GetDteAsync();
                dte.Solution.Open(dialog.FileName);
                SetRoot(Path.GetDirectoryName(dialog.FileName));
                SetStatus("Solution opened.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private async void CreateProject()
        {
            try
            {
                var dte = await _package.GetDteAsync();
                if (dte.Solution.IsOpen == false || string.IsNullOrWhiteSpace(dte.Solution.FullName))
                {
                    SetStatus("Open and save a solution before creating a project.");
                    return;
                }

                var templateDialog = new OpenFileDialog
                {
                    Title = "Select a Visual Studio project template",
                    Filter = "Project templates (*.zip;*.vstemplate)|*.zip;*.vstemplate",
                    CheckFileExists = true
                };
                if (templateDialog.ShowDialog() != true)
                {
                    return;
                }

                var projectName = PromptForName("Project name", Path.GetFileNameWithoutExtension(templateDialog.FileName));
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    return;
                }

                if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    !string.Equals(Path.GetFileName(projectName), projectName, StringComparison.Ordinal) ||
                    projectName == "." || projectName == "..")
                {
                    throw new ArgumentException("Enter a project name without a path.");
                }

                var solutionDirectory = Path.GetDirectoryName(dte.Solution.FullName);
                dte.Solution.AddFromTemplate(
                    templateDialog.FileName,
                    Path.Combine(solutionDirectory, projectName),
                    projectName,
                    false);
                SetRoot(solutionDirectory);
                SetStatus("Project created from the selected template.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private async void BuildSolution()
        {
            try
            {
                var dte = await _package.GetDteAsync();
                if (!dte.Solution.IsOpen)
                {
                    SetStatus("Open a solution before building.");
                    return;
                }

                dte.Solution.SolutionBuild.Build(true);
                var build = dte.Solution.SolutionBuild;
                SetStatus(build.LastBuildInfo == 0
                    ? "Build completed successfully."
                    : $"Build completed with {build.LastBuildInfo} error(s). Check the Output window for details.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ChooseFolder()
        {
            using (var dialog = new Forms.FolderBrowserDialog { Description = "Choose a project or solution folder" })
            {
                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    try
                    {
                        SetRoot(dialog.SelectedPath);
                        SetStatus("Folder selected.");
                    }
                    catch (Exception ex)
                    {
                        ShowError(ex);
                    }
                }
            }
        }

        private void GoUp()
        {
            if (_currentPath == null || string.Equals(_currentPath, _rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var parent = Directory.GetParent(_currentPath)?.FullName;
                if (parent != null && IsWithinRoot(parent))
                {
                    _currentPath = NormalizeDirectoryPath(parent);
                    RefreshEntries();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void CreateFile()
        {
            var name = PromptForName("New file name", "NewFile.txt");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                var path = GetSafeChildPath(name);
                using (File.Create(path)) { }
                RefreshEntries();
                SetStatus("File created.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void CreateFolder()
        {
            var name = PromptForName("New folder name", "NewFolder");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(GetSafeChildPath(name));
                RefreshEntries();
                SetStatus("Folder created.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RenameEntry()
        {
            var entry = _entries.SelectedItem as FileEntry;
            if (entry == null)
            {
                SetStatus("Select a file or folder to rename.");
                return;
            }

            var name = PromptForName("New name", entry.Name);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, entry.Name, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var destination = GetSafeChildPath(name);
                if (entry.IsDirectory)
                {
                    Directory.Move(entry.FullPath, destination);
                }
                else
                {
                    File.Move(entry.FullPath, destination);
                }

                RefreshEntries();
                SetStatus("Item renamed.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteEntry()
        {
            var entry = _entries.SelectedItem as FileEntry;
            if (entry == null)
            {
                SetStatus("Select a file or folder to delete.");
                return;
            }

            var answer = MessageBox.Show(
                $"Move '{entry.Name}' to the Recycle Bin?",
                "Confirm deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (entry.IsDirectory)
                {
                    FileSystem.DeleteDirectory(entry.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    FileSystem.DeleteFile(entry.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }

                RefreshEntries();
                SetStatus("Item moved to the Recycle Bin.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private async void OpenOrEnterSelected(object sender, MouseButtonEventArgs e)
        {
            var entry = _entries.SelectedItem as FileEntry;
            if (entry == null)
            {
                return;
            }

            try
            {
                if (entry.IsDirectory)
                {
                    _currentPath = entry.FullPath;
                    RefreshEntries();
                }
                else
                {
                    var dte = await _package.GetDteAsync();
                    dte.ItemOperations.OpenFile(entry.FullPath);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private static string PromptForName(string title, string initialValue)
        {
            var win = new System.Windows.Window
            {
                Title = title,
                Width = 380,
                Height = 145,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var panel = new StackPanel { Margin = new Thickness(12) };
            var input = new TextBox { Text = initialValue, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(input);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 72, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 72 };

            ok.Click += (_, __) => win.DialogResult = true;
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            win.Content = panel;

            return win.ShowDialog() == true ? input.Text.Trim() : null;
        }

        private string GetSafeChildPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name == "." || name == "..")
            {
                throw new ArgumentException("Enter a valid file or folder name.");
            }

            var path = Path.GetFullPath(Path.Combine(_currentPath, name));
            if (!string.Equals(NormalizeDirectoryPath(Path.GetDirectoryName(path)), _currentPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Items can only be created or renamed inside the current folder.");
            }

            if (File.Exists(path) || Directory.Exists(path))
            {
                throw new IOException("An item with that name already exists.");
            }

            return path;
        }

        private bool IsWithinRoot(string path)
        {
            var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return string.Equals(path, _rootPath, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private void SetRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException("The selected solution folder does not exist.");
            }

            _rootPath = NormalizeDirectoryPath(path);
            _currentPath = _rootPath;
            RefreshEntries();
        }

        private static string NormalizeDirectoryPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var volumeRoot = Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, volumeRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private void RefreshEntries()
        {
            _pathBox.Text = _currentPath ?? string.Empty;
            _entries.Items.Clear();
            if (_currentPath == null)
            {
                return;
            }

            var directories = Directory.EnumerateDirectories(_currentPath)
                .Select(path => new FileEntry(path, true));
            var files = Directory.EnumerateFiles(_currentPath)
                .Select(path => new FileEntry(path, false));

            foreach (var entry in directories.Concat(files).OrderByDescending(item => item.IsDirectory).ThenBy(item => item.Name))
            {
                _entries.Items.Add(entry);
            }
        }

        private void SetStatus(string message)
        {
            _status.Text = message;
        }

        private void ShowError(Exception exception)
        {
            SetStatus(exception.Message);
            MessageBox.Show(exception.Message, "Project Desk", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private sealed class FileEntry
        {
            public FileEntry(string fullPath, bool isDirectory)
            {
                FullPath = fullPath;
                IsDirectory = isDirectory;
                Name = Path.GetFileName(fullPath);
                DisplayName = isDirectory ? $"[Folder] {Name}" : Name;
            }

            public string FullPath { get; }
            public bool IsDirectory { get; }
            public string Name { get; }
            public string DisplayName { get; }
        }
    }
}
