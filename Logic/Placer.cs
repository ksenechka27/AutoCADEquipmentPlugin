using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AutoCADEquipmentPlugin.Utils;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void Place(string blockName, double offset, bool clearOld)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // Получаем границу помещения
            Polyline boundary = GeometryUtils.SelectPolyline("Выберите границу помещения");
            if (boundary == null)
            {
                ed.WriteMessage("\nОтмена: не выбрана граница.");
                return;
            }

            // Получаем точку входа
            Point3d? entry = GeometryUtils.GetPoint("Укажите точку входа");
            if (entry == null) return;

            // Получаем точку выхода
            Point3d? exit = GeometryUtils.GetPoint("Укажите точку выхода");
            if (exit == null) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Очищаем старые объекты, если нужно
                if (clearOld)
                    BlockUtils.ClearOldBlocks(db, tr, blockName);

                // Сбор препятствий
                List<Extents3d> obstacles = GeometryUtils.CollectObstacles(db, tr, boundary);

                // Загружаем блок
                if (!BlockUtils.EnsureBlockExists(blockName, db, tr))
                {
                    ed.WriteMessage("\nБлок не найден: " + blockName);
                    return;
                }

                // Размещение
                PlaceAlongPolyline(db, tr, boundary, blockName, offset, (Point3d)entry, (Point3d)exit, obstacles);

                tr.Commit();
            }
        }

        private static void PlaceAlongPolyline(Database db, Transaction tr, Polyline boundary, string blockName, double offset, Point3d entry, Point3d exit, List<Extents3d> obstacles)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            for (int i = 0; i < boundary.NumberOfVertices - 1; i++)
            {
                Point3d start = boundary.GetPoint3dAt(i);
                Point3d end = boundary.GetPoint3dAt(i + 1);
                Vector3d direction = (end - start).GetNormal();
                Vector3d normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);

                double length = start.DistanceTo(end);
                double current = 0;
                while (current + offset < length)
                {
                    Point3d pos = start + direction.MultiplyBy(current);
                    if (!TryPlaceBlock(db, tr, btr, blockName, pos, direction, offset, obstacles))
                    {
                        // попытка развернуть блок на 90 градусов
                        if (!TryPlaceBlock(db, tr, btr, blockName, pos, normal, offset, obstacles))
                        {
                            // пропускаем, если не удалось разместить
                            current += offset;
                            continue;
                        }
                    }
                    current += offset;
                }
            }
        }

        private static bool TryPlaceBlock(Database db, Transaction tr, BlockTableRecord btr, string blockName, Point3d position, Vector3d orientation, double offset, List<Extents3d> obstacles)
        {
            Matrix3d transform = Matrix3d.Displacement(position - Point3d.Origin) *
                                 Matrix3d.Rotation(orientation.AngleOnPlane(new Plane(Point3d.Origin, Vector3d.ZAxis)), Vector3d.ZAxis, Point3d.Origin);

            using (BlockReference br = new BlockReference(Point3d.Origin, BlockUtils.GetBlockId(db, tr, blockName)))
            {
                br.TransformBy(transform);
                br.ScaleFactors = new Scale3d(1);
                br.Layer = "0";

                Extents3d extents = br.GeometricExtents;
                foreach (var obs in obstacles)
                {
                    if (GeometryUtils.Intersects(extents, obs))
                        return false;
                }

                btr.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                return true;
            }
        }
    }
}
