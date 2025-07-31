using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class BlockEntry
    {
        public string BlockName { get; set; }
        public double Offset { get; set; } // в метрах
        public int Count { get; set; }     // количество блоков
    }

    public class PlaceMultiForm : Form
    {
        private TextBox blockNameTextBox;
        private NumericUpDown offsetUpDown;
        private NumericUpDown countUpDown;
        private Button addButton;
        private Button placeButton;
        private ListBox blockListBox;

        private List<BlockEntry> blockEntries = new List<BlockEntry>();

        public PlaceMultiForm()
        {
            Text = "Выбор блоков";
            Width = 400;
            Height = 300;

            Controls.Add(new Label { Text = "Имя блока:", Top = 10, Left = 10, Width = 100 });
            blockNameTextBox = new TextBox { Top = 10, Left = 120, Width = 150 };
            Controls.Add(blockNameTextBox);

            Controls.Add(new Label { Text = "Отступ (мм):", Top = 40, Left = 10, Width = 100 });
            offsetUpDown = new NumericUpDown { Top = 40, Left = 120, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };
            Controls.Add(offsetUpDown);

            Controls.Add(new Label { Text = "Количество:", Top = 70, Left = 10, Width = 100 });
            countUpDown = new NumericUpDown { Top = 70, Left = 120, Width = 100, Minimum = 1, Maximum = 100 };
            Controls.Add(countUpDown);

            addButton = new Button { Text = "Добавить", Top = 100, Left = 120, Width = 100 };
            addButton.Click += AddBlock;
            Controls.Add(addButton);

            blockListBox = new ListBox { Top = 130, Left = 10, Width = 360, Height = 80 };
            Controls.Add(blockListBox);

            placeButton = new Button { Text = "Разместить всё", Top = 220, Left = 120, Width = 120 };
            placeButton.Click += (s, e) =>
            {
                Close();
                Logic.Placer.PlaceBlocks(blockEntries); // вызываем новую функцию
            };
            Controls.Add(placeButton);
        }

        private void AddBlock(object sender, EventArgs e)
        {
            var name = blockNameTextBox.Text.Trim();
            var offset = (double)offsetUpDown.Value / 1000.0; // мм → м
            var count = (int)countUpDown.Value;

            if (string.IsNullOrEmpty(name)) return;

            blockEntries.Add(new BlockEntry { BlockName = name, Offset = offset, Count = count });
            blockListBox.Items.Add($"{name} — отступ: {offset * 1000}мм, кол-во: {count}");

            blockNameTextBox.Clear();
        }
    }
}
