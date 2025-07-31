using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Plugin.UI;
using System;

namespace Plugin.Logic
{
    public class Placer
    {
        [CommandMethod("eqp")]
        public void PlaceWithUI()
        {
            var form = new PlaceForm();
            Application.ShowModalDialog(form);

            if (!form.DialogResult.HasValue || !form.DialogResult.Value)
                return;

            var blockName = form.SelectedBlockName;
            var offset = form.Offset;
            var clearOld = form.ClearOld;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 👉 Выбор точки входа
            PromptPointResult entryRes = ed.GetPoint("\nУкажите точку входа:");
            if (entryRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nТочка входа не выбрана.");
                return;
            }
            Point3d entryPoint = entryRes.Value;

            // 👉 Выбор точки выхода
            PromptPointResult exitRes = ed.GetPoint("\nУкажите точку выхода:");
            if (exitRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nТочка выхода не выбрана.");
                return;
            }
            Point3d exitPoint = exitRes.Value;

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (!bt.Has(blockName))
                {
                    ed.WriteMessage($"\nБлок \"{blockName}\" не найден.");
                    return;
                }

                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                if (clearOld)
                {
                    foreach (ObjectId id in modelSpace)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent is BlockReference br && br.Name == blockName)
                        {
                            br.Erase();
                        }
                    }
                }

                // 🔧 Здесь нужно будет реализовать алгоритм размещения вдоль границ, с учетом входа/выхода
                // Пока просто вставим блок в точку входа
                BlockReference newBr = new BlockReference(entryPoint, bt[blockName]);
                modelSpace.AppendEntity(newBr);
                tr.AddNewlyCreatedDBObject(newBr, true);

                tr.Commit();
            }
        }
    }
}
