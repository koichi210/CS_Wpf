using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;

namespace TabControl
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //tabItem1.TabPages.Add(myTabPage
            //tabItem1.AddHandler(
            //tabControl1
            //tabControl1.

            //var tabPage = new System.Windows.Forms.TabPage("タブページ名");
            //tabControl1.Controls.Add(tabPage);        ←Controls　ってのが出ないぞ。。

            //System.Windows.Controls.TabControl.AddSelectedHandler(

            //string title = "TabPage " + (tabControl1.TabCount + 1).ToString();
            //TabPage myTabPage = new TabPage(title);
            //tabControl1.TabPages.Add(myTabPage);
        }

        private void tabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


        }
    }
}
