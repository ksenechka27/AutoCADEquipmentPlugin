using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AutoCADEquipmentPlugin.Geometry;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        // Главный метод размещения оборудования
        public static void PlaceEquipmentAlongWalls(BlockTableRecord ms, Transaction tr, Polyline boundary, List<(ObjectId blockId, double offset, int count)> blocks)
        {
            var obstacles = GetObstacles(ms, tr, boundary);

            foreach (var (blockId, offset, count) in blocks)
            {
                int placed = 0;

                foreach (var segment in GetWallSegments(boundary))
                {
                    if (placed >= count)
                        break;

                    placed += PlaceBlockAlongPolyline(ms, tr, blockId, segment, offset, count - placed, obstacles);
                }
            }
        }

        // Получение всех отрезков стен (линейных участков) из полилинии
        private static List<Line> GetWallSegments(Polyline poly)
        {
            var segments = new List<Line>();

            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                Point3d p1 = poly.GetPoint3dAt(i);
                Point3d p2 = poly.GetPoint3dAt((i + 1) % poly.NumberOfVertices);
                segments.Add(new Line(p1, p2));
            }

            return segments;
        }

        // Размещение вдоль конкретного отрезка
        private static int PlaceBlockAlongPolyline(BlockTableRecord ms, Transaction tr, ObjectId blockId, Line segment, double offset, int maxCount, List<Extents3d> obstacles)
        {
            int placed = 0;

            BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
            if (blockDef == null) return 0;

            Extents3d defBounds = blockDef.Bounds ?? new Extents3d(Point3d.Origin, new Point3d(1, 1, 0));
            double width = defBounds.MaxPoint.X - defBounds.MinPoint.X;

            Vector3d direction = (segment.EndPoint - segment.StartPoint).GetNormal();
            double availableLength = segment.Length;
            Point3d current = segment.StartPoint;

            for (int i = 0; i < maxCount && availableLength > width; i++)
            {
                Point3d insertPoint = current + direction * (offset + i * (width + offset));

                if (TryPlaceBlock(ms, tr, blockId, insertPoint, direction, obstacles, out BlockReference br))
                {
                    ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                    placed++;
                }
                else
                {
                    // Обойти или повернуть — можно доработать ещё
                    continue;
                }

                availableLength -= (width + offset);
            }

            return placed;
        }

        // Попытка разместить блок без пересечений
        private static bool TryPlaceBlock(BlockTableRecord ms, Transaction tr, ObjectId blockId, Point3d position, Vector3d direction, List<Extents3d> obstacles, out BlockReference result)
        {
            result = new BlockReference(position, blockId);
            result.Rotation = direction.Angle;

            tr.AddNewlyCreatedDBObject(result, true);

            bool intersects = GeometryUtils.IntersectsOther(ms, result, tr) || IntersectsObstacles(result, obstacles);

            if (intersects)
            {
                result.Erase();
                result = null;
                return false;
            }

            return true;
        }

        // Проверка пересечений с препятствиями
        private static bool IntersectsObstacles(BlockReference br, List<Extents3d> obstacles)
        {
            if (!br.Bounds.HasValue) return false;

            var brBounds = br.Bounds.Value;

            foreach (var obs in obstacles)
            {
                bool intersect =
                    brBounds.MinPoint.X <= obs.MaxPoint.X &&
                    brBounds.MaxPoint.X >= obs.MinPoint.X &&
                    brBounds.MinPoint.Y <= obs.MaxPoint.Y &&
                    brBounds.MaxPoint.Y >= obs.MinPoint.Y;

                if (intersect)
                    return true;
            }

            return false;
        }

        // 🔍 Поиск препятствий внутри области
        private static List<Extents3d> GetObstacles(BlockTableRecord ms, Transaction tr, Polyline boundary)
        {
            var result = new List<Extents3d>();

            foreach (ObjectId id in ms)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || !ent.Bounds.HasValue) continue;

                // Пропускаем блоки оборудования
                if (ent is BlockReference) continue;

                var bounds = ent.Bounds.Value;

                // Центр объекта должен быть внутри границы
                var center = new Point3d(
                    (bounds.MinPoint.X + bounds.MaxPoint.X) / 2,
                    (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2,
                    0);

                if (GeometryUtils.IsPointInside(boundary, center))
                {
                    result.Add(bounds);
                }
            }

            return result;
        }
    }
}
