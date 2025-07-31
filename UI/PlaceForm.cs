using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AutoCADEquipmentPlugin.UI
{
    public class BlockInfo
    {
        public string Name { get; set; }
        public double Offset { get; set; }
        public int Count { get; set; }  // -1 означает "до заполнения"
    }

    public class PlaceForm : Form
    {
        private ListView blockListView;
        private Button addButton, removeButton, placeButton;
        private CheckBox clearOldCheckBox;

        public PlaceForm()
        {
            Text = "Настройки размещения оборудования";
            Width = 500;
            Height = 400;

            blockListView = new ListView
            {
                Top = 10,
                Left = 10,
                Width = 460,
                Height = 250,
                View = View.Details,
                FullRowSelect = true
            };
            blockListView.Columns.Add("Имя блока", 180);
            blockListView.Columns.Add("Отступ (мм)", 100);
            blockListView.Columns.Add("Кол-во", 80);
            Controls.Add(blockListView);

            addButton = new Button { Text = "Добавить", Top = 270, Left = 10, Width = 100 };
            removeButton = new Button { Text = "Удалить", Top = 270, Left = 120, Width = 100 };
            placeButton = new Button { Text = "Разместить", Top = 320, Left = 360, Width = 100 };
            clearOldCheckBox = new CheckBox { Text = "Очистить старые", Top = 320, Left = 10, Width = 200 };

            Controls.Add(addButton);
            Controls.Add(removeButton);
            Controls.Add(placeButton);
            Controls.Add(clearOldCheckBox);

            addButton.Click += (s, e) =>
            {
                var dlg = new AddBlockDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var item = new ListViewItem(new[] {
                        dlg.BlockName,
                        dlg.Offset.ToString(),
                        dlg.Count == -1 ? "∞" : dlg.Count.ToString()
                    });
                    blockListView.Items.Add(item);
                }
            };

            removeButton.Click += (s, e) =>
            {
                foreach (ListViewItem item in blockListView.SelectedItems)
                    blockListView.Items.Remove(item);
            };

            placeButton.Click += (s, e) =>
            {
                var blocks = new List<BlockInfo>();
                foreach (ListViewItem item in blockListView.Items)
                {
                    string name = item.SubItems[0].Text;
                    double offset = double.Parse(item.SubItems[1].Text) / 1000.0;
                    int count = item.SubItems[2].Text == "∞" ? -1 : int.Parse(item.SubItems[2].Text);
                    blocks.Add(new BlockInfo { Name = name, Offset = offset, Count = count });
                }

                bool clearOld = clearOldCheckBox.Checked;
                Close();

                // Передаём в Placer
                Logic.Placer.Place(blocks, clearOld);
            };
        }
    }
}
