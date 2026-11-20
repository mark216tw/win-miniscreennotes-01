using System.Windows;
using System.Windows.Controls;
using Com.MiniScreenNotes.Resources.Strings;
using Com.MiniScreenNotes.ViewModels;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Com.MiniScreenNotes.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = Strings.Settings_Title;
    }

    public event EventHandler? OpenAboutRequested;

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddNewProfile();
    }

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var newName = ShowRenameDialog(this, _viewModel.SelectedProfile.Name);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _viewModel.RenameSelectedProfile(newName);
        }
    }

    private static string? ShowRenameDialog(Window owner, string currentName)
    {
        var dialog = new Window
        {
            Title = "重新命名組態",
            Owner = owner,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        dialog.SetResourceReference(BackgroundProperty, "SurfaceBackgroundBrush");

        var root = new StackPanel { Margin = new Thickness(16) };

        var label = new TextBlock
        {
            Text = "請輸入新的組態名稱：",
            Style = (Style)owner.FindResource("BodyTextStyle"),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var input = new WpfTextBox
        {
            Text = currentName,
            Height = 30,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var cancelBtn = new WpfButton
        {
            Content = "取消",
            MinWidth = 76,
            IsCancel = true,
            Style = (Style)owner.FindResource("SecondaryButtonStyle"),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var okBtn = new WpfButton
        {
            Content = "確定",
            MinWidth = 76,
            IsDefault = true,
            Style = (Style)owner.FindResource("PrimaryButtonStyle")
        };
        okBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return;
            }

            dialog.DialogResult = true;
        };

        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);

        root.Children.Add(label);
        root.Children.Add(input);
        root.Children.Add(buttons);
        dialog.Content = root;

        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanDeleteSelectedProfile)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            this,
            Strings.Confirm_DeleteProfile,
            Strings.Confirm_Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedProfile();
        }
    }

    private void MoveToolUp_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MoveSelectedTool(-1);
    }

    private void MoveToolDown_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MoveSelectedTool(1);
    }

    private void ShowAllTools_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowAllTools();
    }

    private void ApplyHotkey_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyHotkey();
    }

    private void BrowseQuickSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "選擇一鍵快速儲存資料夾",
            InitialDirectory = _viewModel.QuickSaveDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.QuickSaveDirectory = dialog.SelectedPath;
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        OpenAboutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
