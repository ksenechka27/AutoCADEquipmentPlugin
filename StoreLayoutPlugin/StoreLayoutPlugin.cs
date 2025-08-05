using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(StoreLayoutPlugin.StoreLayout))]

namespace StoreLayoutPlugin
{
    public class StoreLayout
    {
        private List<Polyline> forbiddenZones = new List<Polyline>();

        [CommandMethod("PlaceEquipmentOptimized")]
        public void PlaceEquipmentOptimized()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // 1. Выбор основной зоны размещения (верхний слой)
                var zonePline = PromptForClosedPolyline(ed, "\nВыберите полилинию - контур зоны размещения оборудования: ");
                if (zonePline == null) return;

                ed.WriteMessage($"\nПлощадь зоны размещения: {zonePline.Area:F2} кв.м.");

                // 2. Выбор точек старта и финиша внутри зоны
                Point3d startPt = PromptForPointInsidePolyline(ed, zonePline, "\nУкажите точку старта размещения оборудования: ");
                if (startPt == Point3d.Origin) return;

                Point3d endPt = PromptForPointInsidePolyline(ed, zonePline, "\nУкажите точку финиша размещения оборудования: ");
                if (endPt == Point3d.Origin) return;

                ed.WriteMessage($"\nСтарт: {startPt}, Финиш: {endPt}");

                // 3. Выбор запретных зон
                forbiddenZones = new List<Polyline>();
                while(true)
                {
                    PromptKeywordOptions pko = new PromptKeywordOptions("\nДобавить запретную зону? [Да/Нет]: ","Да Нет");
                    var pkr = ed.GetKeywords(pko);
                    if (pkr.Status != PromptStatus.OK || pkr.StringResult == "Нет")
                        break;

                    var forbidZone = PromptForClosedPolyline(ed, "\nВыберите полилинию - запретная зона: ");
                    if(forbidZone != null)
                    {
                        forbiddenZones.Add(forbidZone);
                        ed.WriteMessage($"\nЗапретная зона добавлена (площадь: {forbidZone.Area:F2} кв.м.).");
                    }
                }

                // 4. Поиск оборудования вне зоны размещения (замкнутой полилинии)
                var equipmentsOutside = GetBlocksOutsidePolyline(db, zonePline, ed);

                if (equipmentsOutside.Count == 0)
                {
                    ed.WriteMessage("\nОборудование вне зоны размещения не найдено. Выход.");
                    return;
                }
                ed.WriteMessage($"\nНайдено {equipmentsOutside.Count} единиц оборудования вне зоны.");

                // 5. Генерация последовательности рядов (разбиение зоны на параллельные линии)
                var rows = GenerateRows(zonePline, 1.5); // 1.5м — условная ширина ряда для размещения

                // 6. Размещение оборудования вдоль рядов жадным алгоритмом с ориентацией вдоль стены
                using(Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    DrawHelper.DrawCircle(btr, tr, startPt, 0.3);
                    DrawHelper.DrawCircle(btr, tr, endPt, 0.3);

                    List<BoundingBox2d> placedBoxes = new List<BoundingBox2d>();

                    int eqIndex = 0;
                    foreach(var row in rows)
                    {
                        var availableSegments = GetAvailableSegments(row, zonePline, forbiddenZones);
                        foreach(var segment in availableSegments)
                        {
                            double segmentLength = segment.Item1.GetDistanceTo(segment.Item2);
                            double currentOffset = 0;

                            while(eqIndex < equipmentsOutside.Count && currentOffset < segmentLength)
                            {
                                var block = equipmentsOutside[eqIndex];
                                var extents = block.GeometricExtents;
                                double length = extents.MaxPoint.X - extents.MinPoint.X;
                                double width = extents.MaxPoint.Y - extents.MinPoint.Y;

                                if(currentOffset + length > segmentLength)
                                    break; // не помещается на текущем сегменте

                                // Позиция размещения вдоль ряда
                                var dirVec = (segment.Item2 - segment.Item1).GetNormal();
                                Point2d placePos2d = segment.Item1 + dirVec.MultiplyBy(currentOffset + length / 2) + row.Normal.MultiplyBy(width / 2);

                                // Проверка попадания внутрь зоны и не в запрещенной зоне
                                if(!IsPointInPolyline(zonePline, placePos2d) || forbiddenZones.Any(fz => IsPointInPolyline(fz, placePos2d)))
                                {
                                    currentOffset += length + 0.1; // небольшой сдвиг
                                    continue;
                                }

                                // Создаем bounding box и проверяем пересечения
                                BoundingBox2d newBox = CreateBoundingBox(placePos2d, length, width, dirVec);
                                bool collision = placedBoxes.Any(box => box.Intersects(newBox));
                                if(collision)
                                {
                                    currentOffset += 0.2; // сдвигаемся и пробуем снова
                                    continue;
                                }

                                // Расчёт ориентации блока — длинная сторона вдоль стены
                                double blockRotation = CalculateBlockRotation(block, dirVec);

                                // Размещаем блок с поворотом
                                block.UpgradeOpen();
                                block.Position = new Autodesk.AutoCAD.Geometry.Point3d(placePos2d.X, placePos2d.Y, block.Position.Z);
                                block.Rotation = blockRotation;

                                placedBoxes.Add(newBox);
                                ed.WriteMessage($"\nРазмещено: {block.Name} на {block.Position} с поворотом {blockRotation * 180/Math.PI:F1}°");

                                currentOffset += length + 0.2;
                                eqIndex++;

                                if(eqIndex >= equipmentsOutside.Count)
                                    break;
                            }

                            if(eqIndex >= equipmentsOutside.Count)
                                break;
                        }
                        if(eqIndex >= equipmentsOutside.Count)
                            break;
                    }

                    if(eqIndex < equipmentsOutside.Count)
                        ed.WriteMessage($"\nВнимание: не удалось разместить {equipmentsOutside.Count - eqIndex} единиц оборудования.");

                    tr.Commit();
                }
            }
            catch(Exception ex)
            {
                ed.WriteMessage("\nОшибка: " + ex.Message);
            }
        }

        private double CalculateBlockRotation(BlockReference block, Vector2d wallDirection)
        {
            var extents = block.GeometricExtents;
            double length = extents.MaxPoint.X - extents.MinPoint.X;
            double width = extents.MaxPoint.Y - extents.MinPoint.Y;

            bool lengthIsLonger = length >= width;
            double wallAngle = Math.Atan2(wallDirection.Y, wallDirection.X);

            return lengthIsLonger ? wallAngle : wallAngle + Math.PI / 2;
        }

        private List<Polyline> forbiddenZones = new List<Polyline>();

        private Polyline PromptForClosedPolyline(Editor ed, string message)
        {
            var peo = new PromptEntityOptions(message);
            peo.SetRejectMessage("\nВыберите замкнутую полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = ed.Document.Database.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null || !pl.Closed)
                {
                    ed.WriteMessage("\nПолилиния должна быть замкнутой.");
                    return null;
                }
                tr.Commit();
                return pl;
            }
        }

        private Point3d PromptForPointInsidePolyline(Editor ed, Polyline pl, string message)
        {
            var ppo = new PromptPointOptions(message);
            while (true)
            {
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) return Point3d.Origin;
                if (IsPointInPolyline(pl, new Point2d(ppr.Value.X, ppr.Value.Y)))
                    return ppr.Value;
                ed.WriteMessage("\nТочка должна находиться внутри выбранного контура.");
            }
        }

        private bool IsPointInPolyline(Polyline pline, Point2d point)
        {
            int crossings = 0;
            int numVerts = pline.NumberOfVertices;

            for (int i = 0; i < numVerts; i++)
            {
                Point2d v1 = pline.GetPoint2dAt(i);
                Point2d v2 = pline.GetPoint2dAt((i + 1) % numVerts);

                if (((v1.Y > point.Y) != (v2.Y > point.Y)))
                {
                    double atX = v1.X + (point.Y - v1.Y) * (v2.X - v1.X) / (v2.Y - v1.Y);
                    if (atX > point.X)
                        crossings++;
                }
            }
            return (crossings % 2) == 1;
        }

        private List<BlockReference> GetBlocksOutsidePolyline(Database db, Polyline polyline, Editor ed)
        {
            List<BlockReference> result = new List<BlockReference>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference blkRef)
                    {
                        Point3d pos = blkRef.Position;
                        if (!IsPointInPolyline(polyline, new Point2d(pos.X, pos.Y)))
                        {
                            result.Add(blkRef);
                        }
                    }
                }
                tr.Commit();
            }
            return result;
        }

        private List<Row> GenerateRows(Polyline zone, double rowWidth)
        {
            var points = zone.GetPoints2d();
            if(points.Count < 3) return new List<Row>();

            Vector2d baseEdge = (points[1] - points[0]).GetNormal();
            Vector2d normal = new Vector2d(-baseEdge.Y, baseEdge.X);

            var projections = points.Select(p => p.DotProduct(normal)).ToList();
            double minProj = projections.Min();
            double maxProj = projections.Max();

            List<Row> rows = new List<Row>();
            for(double offset = minProj; offset <= maxProj; offset += rowWidth)
            {
                Point2d start = points[0] + normal.MultiplyBy(offset - projections[0]);
                Point2d end = points[1] + normal.MultiplyBy(offset - projections[1]);
                rows.Add(new Row() { Start = start, End = end, Normal = normal });
            }
            return rows;
        }

        private List<Tuple<Point2d, Point2d>> GetAvailableSegments(Row row, Polyline zone, List<Polyline> forbZones)
        {
            return new List<Tuple<Point2d, Point2d>> { Tuple.Create(row.Start, row.End) };
        }

        private int FindNearestVertexIndex(List<Point2d> points, Point2d target)
        {
            int bestIndex = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                double dist = points[i].GetDistanceTo(target);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private BoundingBox2d CreateBoundingBox(Point2d basePos, double length, double width, Vector2d direction)
        {
            Vector2d perp = new Vector2d(-direction.Y, direction.X);
            Point2d p1 = basePos;
            Point2d p2 = basePos + direction.MultiplyBy(length);
            Point2d p3 = p2 + perp.MultiplyBy(width);
            Point2d p4 = basePos + perp.MultiplyBy(width);

            double minX = new[] { p1.X, p2.X, p3.X, p4.X }.Min();
            double maxX = new[] { p1.X, p2.X, p3.X, p4.X }.Max();
            double minY = new[] { p1.Y, p2.Y, p3.Y, p4.Y }.Min();
            double maxY = new[] { p1.Y, p2.Y, p3.Y, p4.Y }.Max();

            return new BoundingBox2d(new Point2d(minX, minY), new Point2d(maxX, maxY));
        }

        private class Row
        {
            public Point2d Start { get; set; }
            public Point2d End { get; set; }
            public Vector2d Normal { get; set; }
        }
    }

    static class DrawHelper
    {
        public static void DrawCircle(BlockTableRecord btr, Transaction tr, Point3d center, double radius)
        {
            Circle circle = new Circle(center, Vector3d.ZAxis, radius);
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
        }

        public static List<Point2d> GetPoints2d(this Polyline pline)
        {
            List<Point2d> pts = new List<Point2d>();
            for (int i = 0; i < pline.NumberOfVertices; i++)
                pts.Add(pline.GetPoint2dAt(i));
            return pts;
        }
    }
}