using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AutoCADEquipmentPlugin.Logic;

namespace AutoCADEquipmentPlugin.UI
{
    public partial class PlaceForm : Form
    {
        public PlaceForm()
        {
            InitializeComponent();
        }

        private void btnPlace_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Выбор полилинии границы
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите полилинию (граница торгового зала): ");
            peo.SetRejectMessage("Нужно выбрать именно полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            Polyline boundary = null;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                boundary = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (boundary == null || !boundary.Closed)
                {
                    ed.WriteMessage("\nОшибка: выбрана незамкнутая или некорректная полилиния.");
                    return;
                }

                // Выбор входа
                PromptPointResult pprEntry = ed.GetPoint("\nУкажите точку входа: ");
                if (pprEntry.Status != PromptStatus.OK) return;
                Point3d entry = pprEntry.Value;

                // Выбор выхода
                PromptPointResult pprExit = ed.GetPoint("\nУкажите точку выхода: ");
                if (pprExit.Status != PromptStatus.OK) return;
                Point3d exit = pprExit.Value;

                // Получение имени блока из текстового поля
                string blockName = txtBlockName.Text.Trim();
                if (string.IsNullOrEmpty(blockName))
                {
                    MessageBox.Show("Введите имя блока.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                List<string> blocks = new List<string> { blockName };

                // Запуск размещения
                Placer.PlaceBlocks(blocks, boundary, entry, exit);

                tr.Commit();
            }

            // Закрыть форму после размещения
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
