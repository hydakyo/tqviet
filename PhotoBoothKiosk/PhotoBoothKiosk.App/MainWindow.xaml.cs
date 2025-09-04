using System;
using System.Windows;
using System.Windows.Input;
using PhotoBoothKiosk.App.ViewModels;

namespace PhotoBoothKiosk.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();

            // Áp kiosk config
            if (App.Config.Kiosk.FullScreen)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode  = ResizeMode.NoResize;
            }

            if (App.Config.Kiosk.TopMost)
            {
                Topmost = true;
                Activate();
            }
        }

        // Bắt phím Ctrl+Shift+Q để thoát app
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftShift) && e.Key == Key.Q)
            {
                Application.Current.Shutdown();
            }
        }

        // Giữ focus kiosk
        protected override void OnDeactivated(EventArgs e)
        {
            if (App.Config.Kiosk.TopMost)
            {
                Topmost = false;
                Topmost = true;
                Activate();
                Focus();
            }
            base.OnDeactivated(e);
        }
    }
}
