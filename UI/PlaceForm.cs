using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
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
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            List<string> blockNames = new List<string>();
            string blockName = txtBlockName.Text.Trim();
            if (!string.IsNullOrEmpty(blockName))
                blockNames.Add(blockName);
            else
            {
                MessageBox.Show("Укажите имя блока.");
                return;
            }

            // Выбор полилинии
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите полилинию (границу помещения):");
            peo.SetRejectMessage("Это не полилиния.");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            Polyline boundary;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                boundary = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                tr.Commit();
            }

            // Выбор входа
            PromptPointResult ppr1 = ed.GetPoint("\nУкажите точку входа:");
            if (ppr1.Status != PromptStatus.OK) return;

            // Выбор выхода
            PromptPointResult ppr2 = ed.GetPoint("\nУкажите точку выхода:");
            if (ppr2.Status != PromptStatus.OK) return;

            // Расстановка
            Placer.PlaceBlocks(blockNames, boundary, ppr1.Value, ppr2.Value);
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
