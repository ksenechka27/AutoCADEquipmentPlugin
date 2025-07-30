using System;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class PlaceForm : Form
    {
        TextBox blockNameTextBox;
        NumericUpDown offsetUpDown;
        CheckBox clearOldCheckBox;
        Button placeButton;

        public PlaceForm()
        {
            Text = "Настройки размещения";
            Width = 300; Height = 180;

            Controls.Add(new Label { Text = "Имя блока:", Top = 10, Left = 10, Width = 100 });
            blockNameTextBox = new TextBox { Top = 10, Left = 120, Width = 150 };
            Controls.Add(blockNameTextBox);

            Controls.Add(new Label { Text = "Отступ (мм):", Top = 40, Left = 10, Width = 100 });
            offsetUpDown = new NumericUpDown { Top = 40, Left = 120, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };
            Controls.Add(offsetUpDown);

            clearOldCheckBox = new CheckBox { Text = "Очистить старые", Top = 70, Left = 120, Width = 150 };
            Controls.Add(clearOldCheckBox);

            placeButton = new Button { Text = "Разместить", Top = 100, Left = 120, Width = 100 };
            placeButton.Click += (s, e) =>
            {
                string blockName = blockNameTextBox.Text;
                double offset = (double)offsetUpDown.Value / 1000.0;
                bool clearOld = clearOldCheckBox.Checked;
                Close();
                Logic.Placer.Place(blockName, offset, clearOld);
            };
            Controls.Add(placeButton);
        }
    }
}
