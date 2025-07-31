using System;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class AddBlockDialog : Form
    {
        public string BlockName { get; private set; }
        public double Offset { get; private set; }
        public int Count { get; private set; }

        TextBox nameBox;
        NumericUpDown offsetUpDown, countUpDown;

        public AddBlockDialog()
        {
            Text = "Добавить блок";
            Width = 300;
            Height = 200;

            Controls.Add(new Label { Text = "Имя блока:", Top = 10, Left = 10 });
            nameBox = new TextBox { Top = 10, Left = 100, Width = 150 };
            Controls.Add(nameBox);

            Controls.Add(new Label { Text = "Отступ (мм):", Top = 40, Left = 10 });
            offsetUpDown = new NumericUpDown { Top = 40, Left = 100, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };
            Controls.Add(offsetUpDown);

            Controls.Add(new Label { Text = "Кол-во (-1 = ∞):", Top = 70, Left = 10 });
            countUpDown = new NumericUpDown { Top = 70, Left = 130, Width = 70, Minimum = -1, Maximum = 1000, Value = -1 };
            Controls.Add(countUpDown);

            var okButton = new Button { Text = "OK", Top = 110, Left = 100, Width = 80 };
            okButton.Click += (s, e) =>
            {
                BlockName = nameBox.Text.Trim();
                Offset = (double)offsetUpDown.Value;
                Count = (int)countUpDown.Value;

                if (string.IsNullOrEmpty(BlockName))
                {
                    MessageBox.Show("Имя блока не может быть пустым.");
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okButton);
        }
    }
}
