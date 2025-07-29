using System;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class PlaceForm : Form
    {
        private TextBox blockNameTextBox;
        private NumericUpDown offsetUpDown;
        private CheckBox clearOldCheckBox;
        private Button placeButton;

        public PlaceForm()
        {
            this.Text = "Настройки расстановки";
            this.Width = 300;
            this.Height = 180;

            Label blockLabel = new Label() { Text = "Имя блока:", Top = 10, Left = 10, Width = 100 };
            blockNameTextBox = new TextBox() { Top = 10, Left = 120, Width = 150 };

            Label offsetLabel = new Label() { Text = "Отступ (мм):", Top = 40, Left = 10, Width = 100 };
            offsetUpDown = new NumericUpDown() { Top = 40, Left = 120, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };

            clearOldCheckBox = new CheckBox() { Text = "Очистить старые", Top = 70, Left = 120, Width = 150 };

            placeButton = new Button() { Text = "Разместить", Top = 100, Left = 120, Width = 100 };
            placeButton.Click += (s, e) =>
            {
                string blockName = blockNameTextBox.Text;
                double offset = (double)offsetUpDown.Value / 1000.0;
                bool clearOld = clearOldCheckBox.Checked;
                this.Close();
                Logic.Placer.Place(blockName, offset, clearOld);
            };

            this.Controls.Add(blockLabel);
            this.Controls.Add(blockNameTextBox);
            this.Controls.Add(offsetLabel);
            this.Controls.Add(offsetUpDown);
            this.Controls.Add(clearOldCheckBox);
            this.Controls.Add(placeButton);
        }
    }
}
