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
    public partial class PlaceMultiForm : Form
    {
        private List<string> blockList = new List<string>();

        private void btnPlace_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var per = ed.GetEntity(new PromptEntityOptions("\nВыберите границу помещения:") { AddAllowedClass(typeof(Polyline), false) });
            if (per.Status != PromptStatus.OK) return;

            Polyline boundary;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                boundary = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!boundary.Closed) { ed.WriteMessage("\nПолилиния должна быть замкнутой."); return; }
                tr.Commit();
            }

            var entryPr = ed.GetPoint("\nУкажите точку входа:");
            if (entryPr.Status != PromptStatus.OK) return;
            var exitPr = ed.GetPoint("\nУкажите точку выхода:");
            if (exitPr.Status != PromptStatus.OK) return;

            // blockList заполняется из UI ранее
            PlaceBlocks(blockList, boundary, entryPr.Value, exitPr.Value);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
