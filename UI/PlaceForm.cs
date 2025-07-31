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
        public PlaceForm() => InitializeComponent();

        private void btnOk_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var peo = new PromptEntityOptions("\nВыберите границу помещения (закрытая полилиния):");
            peo.SetRejectMessage("Нужно выбрать замкнутую полилинию.");
            peo.AddAllowedClass(typeof(Polyline), false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            Polyline boundary;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                boundary = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!boundary.Closed) { ed.WriteMessage("\nПолилиния должна быть замкнутой."); return; }
                tr.Commit();
            }

            var ppr1 = ed.GetPoint("\nУкажите точку входа:"); if (ppr1.Status != PromptStatus.OK) return;
            var ppr2 = ed.GetPoint("\nУкажите точку выхода:"); if (ppr2.Status != PromptStatus.OK) return;

            string blockName = comboBoxBlockName.Text;
            if (string.IsNullOrWhiteSpace(blockName)) { MessageBox.Show("Выберите блок."); return; }

            PlaceBlocks(new List<string> { blockName }, boundary, ppr1.Value, ppr2.Value);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
