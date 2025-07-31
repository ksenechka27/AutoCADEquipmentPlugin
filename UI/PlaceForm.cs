using System;
using System.Windows.Forms;
using AutoCADEquipmentPlugin.Logic;

namespace AutoCADEquipmentPlugin.UI
{
    public partial class PlaceForm : Form
    {
        public string BlockName => txtBlockName.Text;
        public double Offset => (double)numOffset.Value;

        public PlaceForm()
        {
            InitializeComponent();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BlockName))
            {
                MessageBox.Show("Введите имя блока.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
