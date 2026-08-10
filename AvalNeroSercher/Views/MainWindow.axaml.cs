using Avalonia.Controls;
using AvalNeroSercher.ViewModels;
using System;

namespace AvalNeroSercher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Важно: очистка ресурсов
            if (DataContext is MainViewModel vm)
                vm.Cleanup();
            base.OnClosed(e);
        }
    }
}