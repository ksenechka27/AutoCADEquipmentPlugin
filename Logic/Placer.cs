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

        private void btnOk_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 1. Запрос замкнутой полилинии
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите замкнутую полилинию помещения:");
            peo.SetRejectMessage("Нужно выбрать полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            ObjectId polyId = per.ObjectId;
            Polyline boundary = null;

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                boundary = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                if (boundary == null || !boundary.Closed)
                {
                    ed.WriteMessage("\nПолилиния должна быть замкнутой.");
                    return;
                }
                tr.Commit();
            }

            // 2. Запрос точки входа
            PromptPointResult pprEntry = ed.GetPoint("\nУкажите точку входа:");
            if (pprEntry.Status != PromptStatus.OK) return;
            Point3d entry = pprEntry.Value;

            // 3. Запрос точки выхода
            PromptPointResult pprExit = ed.GetPoint("\nУкажите точку выхода:");
            if (pprExit.Status != PromptStatus.OK) return;
            Point3d exit = pprExit.Value;

            // 4. Получение имени блока
            string blockName = comboBoxBlockName.Text; // Из UI
            if (string.IsNullOrWhiteSpace(blockName))
            {
                MessageBox.Show("Выберите имя блока.");
                return;
            }

            // 5. Вызов размещения
            List<string> blocks = new List<string> { blockName };
            Placer.PlaceBlocks(blocks, boundary, entry, exit);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
