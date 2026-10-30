using System;
using System.Windows;

namespace OODD.Charts.Demo.Wpf
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application application = new Application();
            application.Run(new MainWindow());
        }
    }
}
