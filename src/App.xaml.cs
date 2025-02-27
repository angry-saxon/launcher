using System;
using System.Windows;

namespace CanaryLauncherUpdate
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
