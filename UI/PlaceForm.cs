using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class PlaceForm : Form
    {
        private DataGridView blockGrid;
        private NumericUpDown offsetUpDown;
        private CheckBox clearOldCheckBox;
        private Button placeButton;

        public PlaceForm()
        {
            Text = "Настройки размещения оборудования";
            Width = 450; Height = 300;

            Controls.Add(new Label { Text = "Блоки для размещения:", Top = 10, Left = 10, Width = 200 });

            blockGrid = new DataGridView
            {
                Top = 30,
                Left = 10,
                Width = 410,
                Height = 150,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                ColumnCount = 2,
                RowHeadersVisible = false
            };
            blockGrid.Columns[0].Name = "Имя блока";
            blockGrid.Columns[1].Name = "Количество (0 = максимум)";
            Controls.Add(blockGrid);

            Controls.Add(new Label { Text = "Отступ (мм):", Top = 190, Left = 10, Width = 100 });
            offsetUpDown = new NumericUpDown
            {
                Top = 190,
                Left = 120,
                Width = 100,
                Minimum = 0,
                Maximum = 10000,
                Value = 500
            };
            Controls.Add(offsetUpDown);

            clearOldCheckBox = new CheckBox { Text = "Очистить старые", Top = 220, Left = 120, Width = 150 };
            Controls.Add(clearOldCheckBox);

            placeButton = new Button { Text = "Разместить", Top = 240, Left = 120, Width = 100 };
            placeButton.Click += (s, e) =>
            {
                var blockList = new List<(string name, int count)>();

                foreach (DataGridViewRow row in blockGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    string blockName = row.Cells[0]?.Value?.ToString();
                    if (string.IsNullOrWhiteSpace(blockName)) continue;

                    int count = 0;
                    if (row.Cells[1]?.Value != null)
                        int.TryParse(row.Cells[1].Value.ToString(), out count);

                    blockList.Add((blockName, count));
                }

                double offset = (double)offsetUpDown.Value / 1000.0;
                bool clearOld = clearOldCheckBox.Checked;

                Close();
                Logic.Placer.Place(blockList, offset, clearOld);
            };
            Controls.Add(placeButton);
        }
    }
}
