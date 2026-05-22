using Microsoft.Win32;
using MotionStudio.Core.Security;
using MotionStudio.Core.Project;
using System.Windows;
using System.Windows.Controls;

namespace MotionStudio.App.Services;

/// <summary>
/// 文件对话框服务。
/// </summary>
public sealed class DialogService
{
    public sealed class LoginRequest
    {
        public required UserRole Role { get; init; }

        public string Password { get; init; } = string.Empty;
    }

    public string? ShowSaveProjectDialog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MotionStudio Project (*.motion.json)|*.motion.json|JSON (*.json)|*.json",
            FileName = "Project1.motion.json",
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenProjectDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MotionStudio Project (*.motion.json;*.json)|*.motion.json;*.json|All files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public ProjectTemplateInfo? ShowProjectTemplateDialog(IReadOnlyList<ProjectTemplateInfo> templates)
    {
        if (templates.Count == 0)
        {
            return null;
        }

        var listBox = new ListBox
        {
            Margin = new Thickness(12),
            MinWidth = 420,
            MinHeight = 220,
            DisplayMemberPath = nameof(ProjectTemplateInfo.DisplayName),
            ItemsSource = templates
        };
        listBox.SelectedIndex = 0;

        var description = new TextBlock
        {
            Margin = new Thickness(12, 0, 12, 8),
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshDescription()
        {
            if (listBox.SelectedItem is ProjectTemplateInfo selected)
            {
                description.Text = $"{selected.Category} | {selected.Description}";
            }
        }

        listBox.SelectionChanged += (_, _) => RefreshDescription();
        RefreshDescription();

        var okButton = new Button
        {
            Content = "确定",
            MinWidth = 86,
            Margin = new Thickness(4)
        };
        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 86,
            Margin = new Thickness(4),
            IsCancel = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 12, 12)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new DockPanel();
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        DockPanel.SetDock(description, Dock.Bottom);
        root.Children.Add(buttonPanel);
        root.Children.Add(description);
        root.Children.Add(listBox);

        var dialog = new Window
        {
            Title = "选择工程模板",
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Content = root
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;

        var result = dialog.ShowDialog() == true ? listBox.SelectedItem as ProjectTemplateInfo : null;
        return result;
    }

    public LoginRequest? ShowRoleLoginDialog(UserRole currentRole)
    {
        var roleCombo = new ComboBox
        {
            Margin = new Thickness(12, 12, 12, 8),
            MinWidth = 240,
            ItemsSource = Enum.GetValues<UserRole>()
        };
        roleCombo.SelectedItem = currentRole;

        var passwordBox = new PasswordBox
        {
            Margin = new Thickness(12, 0, 12, 12),
            MinWidth = 240
        };

        var hint = new TextBlock
        {
            Margin = new Thickness(12, 0, 12, 8),
            Text = "Operator 可空密码，Engineer=123456，Administrator=admin123",
            TextWrapping = TextWrapping.Wrap
        };

        var okButton = new Button
        {
            Content = "确定",
            MinWidth = 86,
            Margin = new Thickness(4)
        };
        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 86,
            Margin = new Thickness(4),
            IsCancel = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 12, 12)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new StackPanel();
        root.Children.Add(new TextBlock { Margin = new Thickness(12, 12, 12, 4), Text = "角色" });
        root.Children.Add(roleCombo);
        root.Children.Add(new TextBlock { Margin = new Thickness(12, 0, 12, 4), Text = "密码" });
        root.Children.Add(passwordBox);
        root.Children.Add(hint);
        root.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = "角色登录",
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Content = root
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;

        if (dialog.ShowDialog() != true || roleCombo.SelectedItem is not UserRole role)
        {
            return null;
        }

        return new LoginRequest
        {
            Role = role,
            Password = passwordBox.Password
        };
    }

    public void ShowPermissionDenied()
    {
        MessageBox.Show("当前权限不足", "权限提示", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
