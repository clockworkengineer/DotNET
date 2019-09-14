using System;
using Gtk;

namespace ContactsDB
{
    /// <summary>
    /// Main class for a simple contacts database program.
    /// </summary>
    class MainClass
    {
        public static void Main(string[] args)
        {
            Application.Init();
            MainWindow win = new MainWindow();
            win.Show();
            Application.Run();
        }
    }
}
