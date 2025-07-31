// Placer.cs — с поддержкой обхода внутренних препятствий и проверки пересечений
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void Place(string blockName, double offset, bool clearOld)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Выбор полилинии помещения
                PromptEntityOptions peo = new PromptEntityOptions("Выберите полилинию помещения: ");
                peo.SetRejectMessage("Нужна полилиния.");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;
                Polyline boundary = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;

                // Точка входа
                PromptPointResult pprStart = ed.GetPoint("Укажите точку входа: ");
                if (pprStart.Status != PromptStatus.OK) return;
                Point3d entry = pprStart.Value;

                // Точка выхода
                PromptPointResult pprEnd = ed.GetPoint("Укажите точку выхода: ");
                if (pprEnd.Status != PromptStatus.OK) return;
                Point3d exit = pprEnd.Value;

                // Поиск препятствий
                List<Extents3d> obstacles = CollectObstacles(tr, db, boundary.ObjectId);

                // Очистка старых блоков
                if (clearOld)
                    DeleteOldBlocks(tr, db, blockName);

                // Размещение вдоль стен
                PlaceBlockAlongPolyline(tr, db, blockName, boundary, entry, offset, obstacles);

                tr.Commit();
            }
        }

        private static List<Extents3d> CollectObstacles(Transaction tr, Database db, ObjectId boundaryId)
        {
            List<Extents3d> result = new();
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (id == boundaryId) continue;
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent is BlockReference br && br.Name.StartsWith("EQP_")) continue;
                try
                {
                    var ext = ent.GeometricExtents;
                    result.Add(ext);
                }
                catch { }
            }
            return result;
        }

        private static void DeleteOldBlocks(Transaction tr, Database db, string blockName)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            foreach (ObjectId id in ms)
            {
                if (id.ObjectClass.Name != "AcDbBlockReference") continue;
                BlockReference br = tr.GetObject(id, OpenMode.ForWrite) as BlockReference;
                if (br != null && br.Name == blockName)
                    br.Erase();
            }
        }

        private static void PlaceBlockAlongPolyline(Transaction tr, Database db, string blockName, Polyline poly, Point3d entry, double offset, List<Extents3d> obstacles)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            double totalLen = poly.Length;
            double current = 0;

            while (current < totalLen)
            {
                Point3d pos = poly.GetPointAtDist(current);
                Vector3d normal = poly.GetFirstDerivative(pos).GetNormal();
                double angle = normal.AngleOnPlane(Vector3d.ZAxis);

                // Попытка размещения
                if (TryPlaceBlock(pos, angle, db, ms, blockName, tr, obstacles))
                    current += offset;
                else
                    current += offset / 2.0; // шаг уменьшаем при препятствиях
            }
        }

        private static bool TryPlaceBlock(Point3d pos, double angle, Database db, BlockTableRecord ms, string blockName, Transaction tr, List<Extents3d> obstacles)
        {
            BlockReference br = new BlockReference(pos, db.BlockTableId[blockName]);
            br.Rotation = angle;
            br.ScaleFactors = new Scale3d(1);
            br.Layer = "0";

            // Проверка пересечений
            Extents3d? ext = TryGetExtents(br);
            if (ext.HasValue)
            {
                foreach (var obs in obstacles)
                {
                    if (IsIntersecting(ext.Value, obs))
                        return false;
                }
            }

            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            return true;
        }

        private static Extents3d? TryGetExtents(Entity ent)
        {
            try { return ent.GeometricExtents; } catch { return null; }
        }

        private static bool IsIntersecting(Extents3d a, Extents3d b)
        {
            return a.MinPoint.X < b.MaxPoint.X && a.MaxPoint.X > b.MinPoint.X &&
                   a.MinPoint.Y < b.MaxPoint.Y && a.MaxPoint.Y > b.MinPoint.Y;
        }
    }
}
