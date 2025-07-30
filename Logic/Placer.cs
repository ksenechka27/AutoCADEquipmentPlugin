using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AutoCADEquipmentPlugin.Geometry;
using System.Linq;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void Place(string blockName, double offset, bool clearOld)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var peo = new PromptEntityOptions("\nВыберите границу (замкнутая полилиния):");
            peo.SetRejectMessage("Только полилинии.");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var poly = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!poly.Closed) { ed.WriteMessage("Полилиния не замкнута."); return; }

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (!bt.Has(blockName)) { ed.WriteMessage($"Блок «{blockName}» не найден."); return; }
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                if (clearOld)
                {
                    foreach (var entId in ms.Cast<ObjectId>())
                    {
                        var ent = tr.GetObject(entId, OpenMode.ForWrite) as BlockReference;
                        if (ent?.Name == blockName) ent.Erase();
                    }
                }

                var brDef = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                if (!brDef.Bounds.HasValue) { ed.WriteMessage("Блок без геометрии."); return; }
                var ext = brDef.Bounds.Value;
                double blockLength = (ext.MaxPoint - ext.MinPoint).X;

                for (int i = 0; i < poly.NumberOfVertices; i++)
                {
                    var p1 = poly.GetPoint3dAt(i);
                    var p2 = poly.GetPoint3dAt((i + 1) % poly.NumberOfVertices);
                    var edge = p2 - p1;
                    var dir = edge.GetNormal();
                    var perp = dir.GetPerpendicularVector().GetNormal() * offset;

                    int count = (int)(edge.Length / (blockLength + offset));
                    for (int j = 0; j < count; j++)
                    {
                        var pos = p1 + dir * (j * (blockLength + offset)) + perp;
                        if (!GeometryUtils.IsPointInside(poly, pos)) continue;

                        var br = new BlockReference(pos, brDef.ObjectId)
                        {
                            Rotation = System.Math.Atan2(dir.Y, dir.X)
                        };
                        if (GeometryUtils.IntersectsOther(ms, br, tr)) continue;

                        ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);
                    }

                    // угловой блок
                    var corner = p2 + perp;
                    if (GeometryUtils.IsPointInside(poly, corner))
                    {
                        var cb = new BlockReference(corner, brDef.ObjectId)
                        {
                            Rotation = System.Math.Atan2(dir.Y, dir.X)
                        };
                        if (!GeometryUtils.IntersectsOther(ms, cb, tr))
                        {
                            ms.AppendEntity(cb);
                            tr.AddNewlyCreatedDBObject(cb, true);
                        }
                    }
                }

                tr.Commit();
                ed.WriteMessage("\nОборудование размещено.");
            }
        }
    }
}
