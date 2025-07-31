using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoCADEquipmentPlugin.UI
{
    public class PlaceForm : Form
    {
        TextBox blockNameTextBox;
        NumericUpDown offsetUpDown;
        CheckBox clearOldCheckBox;
        Button selectPointsButton;
        Button placeButton;

        Point3d? entryPoint = null;
        Point3d? exitPoint = null;

        public PlaceForm()
        {
            Text = "Настройки размещения";
            Width = 300; Height = 220;

            Controls.Add(new Label { Text = "Имя блока:", Top = 10, Left = 10, Width = 100 });
            blockNameTextBox = new TextBox { Top = 10, Left = 120, Width = 150 };
            Controls.Add(blockNameTextBox);

            Controls.Add(new Label { Text = "Отступ (мм):", Top = 40, Left = 10, Width = 100 });
            offsetUpDown = new NumericUpDown { Top = 40, Left = 120, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };
            Controls.Add(offsetUpDown);

            clearOldCheckBox = new CheckBox { Text = "Очистить старые", Top = 70, Left = 120, Width = 150 };
            Controls.Add(clearOldCheckBox);

            selectPointsButton = new Button { Text = "Выбрать вход/выход", Top = 100, Left = 120, Width = 150 };
            selectPointsButton.Click += (s, e) =>
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                PromptPointOptions ppo1 = new PromptPointOptions("\nУкажите точку входа:");
                var res1 = ed.GetPoint(ppo1);
                if (res1.Status != PromptStatus.OK) return;
                entryPoint = res1.Value;

                PromptPointOptions ppo2 = new PromptPointOptions("\nУкажите точку выхода:");
                var res2 = ed.GetPoint(ppo2);
                if (res2.Status != PromptStatus.OK) return;
                exitPoint = res2.Value;

                ed.WriteMessage($"\nВход: {entryPoint.Value}, Выход: {exitPoint.Value}");
            };
            Controls.Add(selectPointsButton);

            placeButton = new Button { Text = "Разместить", Top = 140, Left = 120, Width = 100 };
            placeButton.Click += (s, e) =>
            {
                string blockName = blockNameTextBox.Text;
                double offset = (double)offsetUpDown.Value / 1000.0;
                bool clearOld = clearOldCheckBox.Checked;

                if (entryPoint == null || exitPoint == null)
                {
                    MessageBox.Show("Необходимо выбрать вход и выход!");
                    return;
                }

                Close();
                Logic.Placer.Place(blockName, offset, clearOld, entryPoint.Value, exitPoint.Value);
            };
            Controls.Add(placeButton);
        }
    }
}
