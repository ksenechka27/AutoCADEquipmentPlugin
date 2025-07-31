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
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions polyOpts = new PromptEntityOptions("Выберите полилинию для размещения: ");
            polyOpts.SetRejectMessage("Только полилинии!");
            polyOpts.AddAllowedClass(typeof(Polyline), false);

            PromptEntityResult polyRes = ed.GetEntity(polyOpts);
            if (polyRes.Status != PromptStatus.OK) return;

            PromptPointResult entryRes = ed.GetPoint("Укажите точку входа: ");
            if (entryRes.Status != PromptStatus.OK) return;
            Point3d entryPoint = entryRes.Value;

            PromptPointResult exitRes = ed.GetPoint("Укажите точку выхода: ");
            if (exitRes.Status != PromptStatus.OK) return;
            Point3d exitPoint = exitRes.Value;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (!bt.Has(blockName))
                {
                    ed.WriteMessage("\nБлок не найден: " + blockName);
                    return;
                }

                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                Polyline boundary = tr.GetObject(polyRes.ObjectId, OpenMode.ForRead) as Polyline;

                // Очистка старых блоков
                if (clearOld)
                {
                    foreach (ObjectId id in modelSpace)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is BlockReference br && br.Name == blockName)
                        {
                            br.UpgradeOpen();
                            br.Erase();
                        }
                    }
                }

                // Сбор препятствий
                List<Extents3d> obstacles = new List<Extents3d>();
                foreach (ObjectId id in modelSpace)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference br && br.Name == blockName) continue;
                    if (ent.Bounds.HasValue)
                    {
                        obstacles.Add(ent.Bounds.Value);
                    }
                }

                // Расставляем блоки вдоль полилинии
                PlaceBlocksAlongPolyline(modelSpace, tr, bt[blockName], boundary, offset, entryPoint, exitPoint, obstacles);
                tr.Commit();
            }
        }

        private static void PlaceBlocksAlongPolyline(BlockTableRecord space, Transaction tr, ObjectId blockId, Polyline pline, double offset, Point3d entry, Point3d exit, List<Extents3d> obstacles)
        {
            double totalLength = pline.Length;
            double placedLength = 0;
            double step = offset;
            int segmentCount = pline.NumberOfVertices - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                LineSegment3d segment = pline.GetLineSegmentAt(i);
                Vector3d direction = segment.Direction.GetNormal();
                double length = segment.Length;
                Point3d current = segment.StartPoint;

                while ((current - segment.EndPoint).Length > offset)
                {
                    Point3d insertionPoint = current + (direction * offset / 2);
                    if (CanPlaceHere(insertionPoint, blockId, tr, obstacles))
                    {
                        BlockReference br = new BlockReference(insertionPoint, blockId);
                        br.Rotation = Math.Atan2(direction.Y, direction.X);
                        space.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);
                    }
                    current += direction * offset;
                }
            }
        }

        private static bool CanPlaceHere(Point3d point, ObjectId blockId, Transaction tr, List<Extents3d> obstacles)
        {
            BlockTableRecord blockDef = tr.GetObject(blockId, OpenMode.ForRead) as BlockTableRecord;
            Extents3d blockExtents = blockDef.Bounds ?? new Extents3d(point, point);
            Point3d min = point + (blockExtents.MinPoint - blockDef.Origin.GetPoint3d());
            Point3d max = point + (blockExtents.MaxPoint - blockDef.Origin.GetPoint3d());
            Extents3d placementExtents = new Extents3d(min, max);

            foreach (var obs in obstacles)
            {
                if (Intersects(placementExtents, obs))
                    return false;
            }
            return true;
        }

        private static bool Intersects(Extents3d a, Extents3d b)
        {
            return !(a.MaxPoint.X < b.MinPoint.X || a.MinPoint.X > b.MaxPoint.X ||
                     a.MaxPoint.Y < b.MinPoint.Y || a.MinPoint.Y > b.MaxPoint.Y);
        }
    }
}
