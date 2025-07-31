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
            // Получаем имя блока из текстового поля
            string blockName = txtBlockName.Text.Trim();
            if (string.IsNullOrEmpty(blockName))
            {
                MessageBox.Show("Введите имя блока.");
                return;
            }

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                // Выбор полилинии-границы
                PromptEntityOptions peo = new PromptEntityOptions("\nВыберите границу (полилинию):");
                peo.SetRejectMessage("\nМожно выбрать только полилинию.");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                ObjectId polyId = per.ObjectId;
                Polyline boundary;
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    boundary = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                    tr.Commit();
                }

                // Выбор точки входа
                PromptPointResult pprEntry = ed.GetPoint("\nУкажите точку входа:");
                if (pprEntry.Status != PromptStatus.OK) return;
                Point3d entry = pprEntry.Value;

                // Выбор точки выхода
                PromptPointResult pprExit = ed.GetPoint("\nУкажите точку выхода:");
                if (pprExit.Status != PromptStatus.OK) return;
                Point3d exit = pprExit.Value;

                // Вызов размещения
                Placer.PlaceBlocks(new List<string> { blockName }, boundary, entry, exit);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nОшибка: {ex.Message}");
            }
            finally
            {
                this.Close();
            }
        }
    }
}
