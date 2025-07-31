using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AutoCADEquipmentPlugin.Geometry;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        /// <summary>
        /// Основной метод размещения блоков по стенам внутри замкнутой области.
        /// </summary>
        public static void PlaceBlocks(ObjectId polylineId, string blockName, double offset)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Polyline boundary = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                if (boundary == null || !boundary.Closed)
                {
                    ed.WriteMessage("\nОшибка: полилиния не замкнута или не существует.");
                    return;
                }

                List<Entity> obstacles = GetObstacles(ms, boundary, tr);

                Place(blockName, boundary, offset, ms, tr, obstacles);
                tr.Commit();
            }
        }

        /// <summary>
        /// Метод размещения блоков вдоль стен с учётом препятствий.
        /// </summary>
        private static void Place(string blockName, Polyline boundary, double offset, BlockTableRecord ms, Transaction tr, List<Entity> obstacles)
        {
            BlockTableRecord blockDef = tr.GetObject(ms.Database.BlockTableId, OpenMode.ForRead) as BlockTableRecord;

            if (!blockDef.Has(blockName)) return;
            ObjectId blockId = blockDef[blockName];

            for (int i = 0; i < boundary.NumberOfVertices - 1; i++)
            {
                Point3d start = boundary.GetPoint3dAt(i);
                Point3d end = boundary.GetPoint3dAt((i + 1) % boundary.NumberOfVertices);

                Vector3d direction = end - start;
                double length = direction.Length;
                Vector3d unit = direction.GetNormal();

                double step = 2.0; // фиксированный шаг (можно заменить на размер блока)
                for (double d = offset; d < length - offset; d += step)
                {
                    Point3d pos = start + unit.MultiplyBy(d);
                    if (!GeometryUtils.IsPointInside(boundary, pos)) continue;

                    BlockReference br = new BlockReference(pos, blockId);
                    ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);

                    if (GeometryUtils.IntersectsOther(ms, br, tr))
                    {
                        br.Erase();
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Получение препятствий внутри области (всё кроме оборудования).
        /// </summary>
        private static List<Entity> GetObstacles(BlockTableRecord ms, Polyline boundary, Transaction tr)
        {
            var result = new List<Entity>();

            foreach (ObjectId id in ms)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent is BlockReference) continue;

                if (ent.Bounds.HasValue && GeometryUtils.IsPointInside(boundary, ent.Bounds.Value.Center()))
                {
                    result.Add(ent);
                }
            }

            return result;
        }
    }

    public static class Extents3dExtensions
    {
        public static Point3d Center(this Extents3d ext)
        {
            return new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                (ext.MinPoint.Y + ext.MaxPoint.Y) / 2,
                (ext.MinPoint.Z + ext.MaxPoint.Z) / 2);
        }
    }
}
