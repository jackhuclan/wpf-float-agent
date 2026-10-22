using System;
using System.Windows;
using System.Windows.Input;
using Floaticon.ViewModels;

namespace Floaticon.Views;

/// <summary>
/// 模型配置对话框：三项必填，保存到 config.json。
/// </summary>
public partial class ConfigWindow : Window
{
    private readonly ConfigViewModel _viewModel;

    public ConfigWindow(ConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Saved += OnSaved;
        Closed += (_, _) => _viewModel.Saved -= OnSaved;
    }

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
