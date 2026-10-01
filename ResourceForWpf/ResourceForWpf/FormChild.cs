using System;
using System.Windows.Forms;

namespace ResourceForWpf
{
    public partial class FormChild : Form
    {
        public FormChild()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Formダイアログのボタンがクリックされました");
        }
    }
}
