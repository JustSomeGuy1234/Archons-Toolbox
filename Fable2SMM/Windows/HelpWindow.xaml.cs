using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace ArchonsToolbox.HelpSystem
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            HelpManager.UpdateHelpItems();
            InitializeComponent();
        }

        public void EnumerateButton_Click(object sender, RoutedEventArgs e)
        {
            HelpManager.UpdateHelpItems();
        }
    }
}
