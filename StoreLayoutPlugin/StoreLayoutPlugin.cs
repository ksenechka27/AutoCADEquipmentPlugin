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
        [CommandMethod("PlaceEquipmentOptimized")]
        public void PlaceEquipmentOptimized()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // 1. Выбор зоны размещения
                ed.WriteMessage("\nВыберите зону размещения оборудования.");
                Polyline placementZone = PromptForClosedPolyline(ed, "\nВыберите полилинию - контур зоны размещения оборудования: ");
                if (placementZone == null) return;
                ed.WriteMessage($"\nЗона размещения выбрана. Площадь: {placementZone.Area:F2} кв.м.");

                // 2. Точка старта размещения
                ed.WriteMessage("\nВыберите точку старта размещения оборудования.");
                Point3d startPt = PromptForPointInsidePolyline(ed, placementZone, "\nУкажите точку старта размещения оборудования: ");
                if (startPt == Point3d.Origin) return;

                // 3. Точка финиша размещения
                ed.WriteMessage("\nВыберите точку финиша размещения оборудования.");
                Point3d endPt = PromptForPointInsidePolyline(ed, placementZone, "\nУкажите точку финиша размещения оборудования: ");
                if (endPt == Point3d.Origin) return;
                ed.WriteMessage($"\nСтарт: {startPt}, Финиш: {endPt}");

                // 4. Выбор зоны с оборудованием
                ed.WriteMessage("\nВыберите зону с оборудованием (полилиния).");
                Polyline equipmentZone = PromptForClosedPolyline(ed, "\nВыберите полилинию - контур зоны с оборудованием: ");
                if (equipmentZone == null) return;
                ed.WriteMessage($"\nЗона с оборудованием выбрана. Площадь: {equipmentZone.Area:F2} кв.м.");

                // 5. Поиск блоков оборудования (стелажи) внутри этой зоны
                List<BlockReference> equipmentBlocks = GetBlocksInsidePolyline(db, equipmentZone, ed);
                ed.WriteMessage($"\nНайдено блоков оборудования (стелажей) внутри зоны с оборудованием: {equipmentBlocks.Count}");
                if (equipmentBlocks.Count == 0)
                {
                    ed.WriteMessage("\nВ зоне с оборудованием блоки не найдены. Выход.");
                    return;
                }

                // 6. Запуск размещения блоков
                ed.WriteMessage("\nНачинается размещение оборудования...");

                PlaceBlocksAlongRows(ed, db, placementZone, startPt, endPt, equipmentBlocks);

                ed.WriteMessage("\nРазмещение завершено.");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nAutoCAD Runtime ошибка: " + ex.Message);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nОбщая ошибка: " + ex.Message);
            }
        }

        private void PlaceBlocksAlongRows(Editor ed, Database db, Polyline placementZone, Point3d startPt, Point3d endPt, List<BlockReference> blocksToPlace)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // Нарисовать точки старта и финиша для визуализации
                DrawHelper.DrawCircle(btr, tr, startPt, 0.3);
                DrawHelper.DrawCircle(btr, tr, endPt, 0.3);

                List<BoundingBox2d> placedBoxes = new List<BoundingBox2d>();

                // Генерация рядов между стартом и финишем
                List<Row> rows = GenerateRowsBetweenPoints(startPt, endPt, 1.5);

                int eqIndex = 0;
                foreach (Row row in rows)
                {
                    var availableSegments = new List<Tuple<Point2d, Point2d>> { Tuple.Create(row.Start, row.End) };

                    foreach (var segment in availableSegments)
                    {
                        double segmentLength = segment.Item1.GetDistanceTo(segment.Item2);
                        double currentOffset = 0;

                        while (eqIndex < blocksToPlace.Count && currentOffset < segmentLength)
                        {
                            BlockReference block = blocksToPlace[eqIndex];

                            // Получаем объект с нужным режимом открытия для редактирования
                            BlockReference blockForWrite = tr.GetObject(block.ObjectId, OpenMode.ForWrite) as BlockReference;

                            Extents3d extents = blockForWrite.GeometricExtents;
                            double length = extents.MaxPoint.X - extents.MinPoint.X;
                            double width = extents.MaxPoint.Y - extents.MinPoint.Y;

                            if (currentOffset + length > segmentLength)
                                break;

                            Vector2d dirVec = (segment.Item2 - segment.Item1).GetNormal();
                            Point2d placePos2d = segment.Item1 + dirVec.MultiplyBy(currentOffset + length / 2) + row.Normal.MultiplyBy(width / 2);

                            if (!IsPointInPolyline(placementZone, placePos2d))
                            {
                                currentOffset += length + 0.1;
                                continue;
                            }

                            BoundingBox2d newBox = CreateBoundingBox(placePos2d, length, width, dirVec);
                            bool collision = placedBoxes.Any(box => box.Intersects(newBox));
                            if (collision)
                            {
                                currentOffset += 0.2;
                                continue;
                            }

                            double blockRotation = CalculateBlockRotation(blockForWrite, dirVec);

                            // Теперь объект открыт для записи, безопасно изменяем
                            blockForWrite.Position = new Point3d(placePos2d.X, placePos2d.Y, blockForWrite.Position.Z);
                            blockForWrite.Rotation = blockRotation;

                            placedBoxes.Add(newBox);
                            ed.WriteMessage($"\nРазмещено: {blockForWrite.Name} на {blockForWrite.Position} с поворотом {blockRotation * 180 / Math.PI:F1}°");

                            currentOffset += length + 0.2;
                            eqIndex++;

                            if (eqIndex >= blocksToPlace.Count)
                                break;
                        }
                        if (eqIndex >= blocksToPlace.Count)
                            break;
                    }
                    if (eqIndex >= blocksToPlace.Count)
                        break;
                }

                if (eqIndex < blocksToPlace.Count)
                    ed.WriteMessage($"\nВнимание: не удалось разместить {blocksToPlace.Count - eqIndex} единиц оборудования.");

                tr.Commit();
            }
        }

        private List<BlockReference> GetBlocksInsidePolyline(Database db, Polyline polyline, Editor ed)
        {
            List<BlockReference> result = new List<BlockReference>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                foreach (ObjectId id in ms)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference blkRef)
                    {
                        Point3d pos = blkRef.Position;
                        string blkName = blkRef.Name.ToLowerInvariant();
                        ed.WriteMessage($"\nНайден блок: {blkName} на позиции {pos}");

                        if (IsPointInPolyline(polyline, new Point2d(pos.X, pos.Y)) &&
                            blkName.Contains("стеллаж")) // фильтр по имени "стеллаж"
                        {
                            result.Add(blkRef);
                        }
                    }
                }
                tr.Commit();
            }
            ed.WriteMessage($"\nВсего блоков с фильтром 'стеллаж': {result.Count}");
            return result;
        }

        private List<Row> GenerateRowsBetweenPoints(Point3d startPt, Point3d endPt, double rowWidth)
        {
            Vector2d baseVec = new Vector2d(endPt.X - startPt.X, endPt.Y - startPt.Y).GetNormal();
            Vector2d normal = new Vector2d(-baseVec.Y, baseVec.X);

            double distance = startPt.DistanceTo(endPt);
            int rowCount = (int)(distance / rowWidth) + 1;

            List<Row> rows = new List<Row>();
            for (int i = 0; i < rowCount; i++)
            {
                Point2d rowStart = new Point2d(startPt.X, startPt.Y) + normal.MultiplyBy(i * rowWidth);
                Point2d rowEnd = new Point2d(endPt.X, endPt.Y) + normal.MultiplyBy(i * rowWidth);
                rows.Add(new Row() { Start = rowStart, End = rowEnd, Normal = normal });
            }
            return rows;
        }

        private double CalculateBlockRotation(BlockReference block, Vector2d direction)
        {
            Extents3d extents = block.GeometricExtents;
            double length = extents.MaxPoint.X - extents.MinPoint.X;
            double width = extents.MaxPoint.Y - extents.MinPoint.Y;

            bool lengthLonger = length >= width;
            double angle = Math.Atan2(direction.Y, direction.X);

            return lengthLonger ? angle : angle + Math.PI / 2;
        }

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
                    ed.WriteMessage("\nОшибка: выбранный объект не является замкнутой полилинией.");
                    return null;
                }
                tr.Commit();
                return pl;
            }
        }

        private Point3d PromptForPointInsidePolyline(Editor ed, Polyline polyline, string message)
        {
            var ppo = new PromptPointOptions(message);
            while (true)
            {
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) return Point3d.Origin;
                if (IsPointInPolyline(polyline, new Point2d(ppr.Value.X, ppr.Value.Y)))
                    return ppr.Value;
                ed.WriteMessage("\nТочка должна находиться внутри выбранного контура.");
            }
        }

        private bool IsPointInPolyline(Polyline polyline, Point2d point)
        {
            int crossings = 0;
            int numVerts = polyline.NumberOfVertices;

            for (int i = 0; i < numVerts; i++)
            {
                Point2d v1 = polyline.GetPoint2dAt(i);
                Point2d v2 = polyline.GetPoint2dAt((i + 1) % numVerts);

                if (((v1.Y > point.Y) != (v2.Y > point.Y)))
                {
                    double atX = v1.X + (point.Y - v1.Y) * (v2.X - v1.X) / (v2.Y - v1.Y);
                    if (atX > point.X)
                        crossings++;
                }
            }
            return (crossings % 2) == 1;
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
    }

    public class Row
    {
        public Point2d Start { get; set; }
        public Point2d End { get; set; }
        public Vector2d Normal { get; set; }
    }

    public class BoundingBox2d
    {
        public Point2d MinPoint { get; }
        public Point2d MaxPoint { get; }

        public BoundingBox2d(Point2d minPoint, Point2d maxPoint)
        {
            MinPoint = minPoint;
            MaxPoint = maxPoint;
        }

        public bool Intersects(BoundingBox2d other)
        {
            return !(other.MinPoint.X > MaxPoint.X || other.MaxPoint.X < MinPoint.X ||
                     other.MinPoint.Y > MaxPoint.Y || other.MaxPoint.Y < MinPoint.Y);
        }
    }

    public static class DrawHelper
    {
        public static void DrawCircle(BlockTableRecord btr, Transaction tr, Point3d center, double radius)
        {
            Circle circle = new Circle(center, Vector3d.ZAxis, radius);
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
        }
    }
}
