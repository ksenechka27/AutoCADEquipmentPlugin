using System;
using System.Collections.Generic;
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
        private const double PointTolerance = 0.2;
        private const double GapBetweenBlocks = 0.1;
        private const double AngleThresholdDegrees = 15.0;

        [CommandMethod("PlaceEquipmentAlongPerimeter")]
        public void PlaceEquipmentAlongPerimeter()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                ed.WriteMessage("\nВыберите замкнутую полилинию — периметр торгового зала:");
                Polyline perimeter = PromptForClosedPolyline(ed, "\nВыберите полилинию — периметр торгового зала:");
                if (perimeter == null) return;

                ed.WriteMessage("\nВыберите прямоугольник (замкнутую полилинию) с оборудованием:");
                Polyline equipmentRect = PromptForClosedPolyline(ed, "\nВыберите прямоугольник с оборудованием:");
                if (equipmentRect == null) return;

                List<BlockReference> equipmentBlocks = GetBlocksInsidePolyline(db, equipmentRect, ed);
                ed.WriteMessage($"\nНайдено блоков оборудования внутри прямоугольника: {equipmentBlocks.Count}");
                if (equipmentBlocks.Count == 0)
                {
                    ed.WriteMessage("\nВ выбранном прямоугольнике оборудование не найдено. Выход.");
                    return;
                }

                // Разбиваем периметр на сегменты — ребра полигона между последовательными вершинами
                List<Tuple<Point3d, Point3d>> segments = GetPerimeterSegments(perimeter);
                ed.WriteMessage($"\nПериметр разбит на {segments.Count} сегментов (рёбер).");

                Point3d startPt = PromptForPointOnPolyline(ed, perimeter, "\nВыберите точку старта размещения оборудования:");
                if (startPt == Point3d.Origin) return;

                Point3d endPt = PromptForPointOnPolyline(ed, perimeter, "\nВыберите точку окончания размещения оборудования:");
                if (endPt == Point3d.Origin) return;

                int startSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, startPt);
                int endSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, endPt);

                if (startSegmentIndex == -1 || endSegmentIndex == -1)
                {
                    ed.WriteMessage("\nТочки старта или финиша не лежат на периметре.");
                    return;
                }

                ed.WriteMessage($"\nСтартовый сегмент: {startSegmentIndex}, конечный сегмент: {endSegmentIndex}");

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    List<int> segmentOrder = GetSegmentOrder(startSegmentIndex, endSegmentIndex, segments.Count);
                    int blockIndex = 0;
                    bool placementDone = false;

                    foreach (int segIdx in segmentOrder)
                    {
                        if (placementDone)
                            break;

                        var segment = segments[segIdx];
                        Point3d segStart = segment.Item1;
                        Point3d segEnd = segment.Item2;

                        Point3d placeStart = (segIdx == startSegmentIndex) ? startPt : segStart;
                        Point3d placeEnd = (segIdx == endSegmentIndex) ? endPt : segEnd;

                        Vector3d segVector = placeEnd - placeStart;
                        double segLength = segVector.Length;
                        Vector3d segDirection = segVector.GetNormal();

                        double currentOffset = 0.0;

                        while (blockIndex < equipmentBlocks.Count && currentOffset < segLength)
                        {
                            var blk = equipmentBlocks[blockIndex];
                            Extents3d extents = blk.GeometricExtents;
                            double length = extents.MaxPoint.X - extents.MinPoint.X;
                            double width = extents.MaxPoint.Y - extents.MinPoint.Y;

                            double rotationAngle = Math.Atan2(segDirection.Y, segDirection.X);

                            if (currentOffset + length <= segLength)
                            {
                                Point3d position = placeStart + segDirection.MultiplyBy(currentOffset + length / 2);
                                CreateBlockReference(tr, ms, blk.Name, position, rotationAngle);
                                ed.WriteMessage($"\nРазмещён блок '{blk.Name}' с поворотом {rotationAngle * 180.0 / Math.PI:F1}° на позиции {position}");
                                currentOffset += length + GapBetweenBlocks;
                                blockIndex++;
                            }
                            else if (currentOffset + width <= segLength)
                            {
                                double altRotation = rotationAngle + Math.PI / 2;
                                Point3d position = placeStart + segDirection.MultiplyBy(currentOffset + width / 2);
                                CreateBlockReference(tr, ms, blk.Name, position, altRotation);
                                ed.WriteMessage($"\nРазмещён блок '{blk.Name}' с альтернативным поворотом {altRotation * 180.0 / Math.PI:F1}° на позиции {position}");
                                currentOffset += width + GapBetweenBlocks;
                                blockIndex++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage($"\nРазмещено блоков: {blockIndex} из {equipmentBlocks.Count}");
                    if (blockIndex < equipmentBlocks.Count)
                    {
                        ed.WriteMessage($"\nНе размещено блоков: {equipmentBlocks.Count - blockIndex}");
                    }
                }
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

        // Разбитие периметра на все рёбра — последовательные вершины
        private List<Tuple<Point3d, Point3d>> GetPerimeterSegments(Polyline pl)
        {
            var segments = new List<Tuple<Point3d, Point3d>>();
            int n = pl.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point3d start = pl.GetPoint3dAt(i);
                Point3d end = pl.GetPoint3dAt((i + 1) % n);
                segments.Add(Tuple.Create(start, end));
            }
            return segments;
        }

        // Находит индекс сегмента, которому принадлежит точка, по параметру ломаной
        private int FindSegmentIndexByPointOnPolyline(Polyline pl, Point3d pt)
        {
            try
            {
                double param = pl.GetParameterAtPoint(pt);
                int index = (int)Math.Floor(param);
                if (index >= 0 && index < pl.NumberOfVertices)
                    return index;
            }
            catch { }
            return -1;
        }

        // Формирование порядка обхода сегментов (по возрастанию индексов с переходом на 0)
        private List<int> GetSegmentOrder(int startSeg, int endSeg, int segCount)
        {
            var order = new List<int> { startSeg };
            int current = startSeg;
            while (current != endSeg)
            {
                current = (current + 1) % segCount;
                order.Add(current);
            }
            return order;
        }

        private Polyline PromptForClosedPolyline(Editor ed, string prompt)
        {
            var peo = new PromptEntityOptions(prompt);
            peo.SetRejectMessage("\nВыберите замкнутую полилинию.");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: true);

            var res = ed.GetEntity(peo);
            if (res.Status != PromptStatus.OK)
                return null;

            using (var tr = ed.Document.Database.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null || !pl.Closed)
                {
                    ed.WriteMessage("\nОбъект не является замкнутой полилинией.");
                    return null;
                }
                tr.Commit();
                return pl;
            }
        }

        private Point3d PromptForPointOnPolyline(Editor ed, Polyline polyline, string prompt)
        {
            var ppo = new PromptPointOptions(prompt);

            while (true)
            {
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) return Point3d.Origin;

                if (IsPointCloseToPolyline(polyline, ppr.Value, PointTolerance))
                    return ppr.Value;

                ed.WriteMessage("\nТочка должна находиться на или очень близко к полилинии.");
            }
        }

        private bool IsPointCloseToPolyline(Polyline polyline, Point3d pt, double tol)
        {
            Point3d closest = polyline.GetClosestPointTo(pt, Vector3d.ZAxis, false);
            return closest.DistanceTo(pt) <= tol;
        }

        private List<BlockReference> GetBlocksInsidePolyline(Database db, Polyline polyline, Editor ed)
        {
            var blocks = new List<BlockReference>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference br)
                    {
                        Point3d pos = br.Position;
                        if (IsPointInPolyline(polyline, new Point2d(pos.X, pos.Y)))
                        {
                            blocks.Add(br);
                        }
                    }
                }

                tr.Commit();
            }
            return blocks;
        }

        // Проверка включения точки внутрь замкнутой 2D полилинии алгоритмом лучевого пересечения
        private bool IsPointInPolyline(Polyline polyline, Point2d point)
        {
            int crossings = 0;
            int n = polyline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point2d v1 = polyline.GetPoint2dAt(i);
                Point2d v2 = polyline.GetPoint2dAt((i + 1) % n);
                if (((v1.Y > point.Y) != (v2.Y > point.Y)))
                {
                    double atX = v1.X + (point.Y - v1.Y) * (v2.X - v1.X) / (v2.Y - v1.Y);
                    if (atX > point.X)
                        crossings++;
                }
            }
            return (crossings % 2) == 1;
        }

        private BlockReference CreateBlockReference(Transaction tr, BlockTableRecord modelSpace, string blockName, Point3d position, double rotation)
        {
            BlockTable bt = (BlockTable)tr.GetObject(modelSpace.Database.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(blockName))
                throw new Autodesk.AutoCAD.Runtime.Exception(
                    Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidInput,
                    $"Блок с именем {blockName} не найден.");

            ObjectId blockId = bt[blockName];
            BlockReference newBlock = new BlockReference(position, blockId)
            {
                Rotation = rotation
            };
            modelSpace.AppendEntity(newBlock);
            tr.AddNewlyCreatedDBObject(newBlock, true);

            return newBlock;
        }
    }
}
