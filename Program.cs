// Program.cs
using System;
using System.Windows.Forms;

namespace Moto.Editor
{
    /// <summary>
    /// Point d'entrée de MOTO Editor.
    /// L'éditeur est volontairement léger : WinForms natif, sans dépendance NuGet externe.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.MainWindow());
        }
    }
}
