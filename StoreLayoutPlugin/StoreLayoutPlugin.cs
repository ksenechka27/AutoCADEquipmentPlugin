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
        // Убираем смещение внутрь, теперь позиция блока на линии
        private const double OffsetInside = 0.0; 

        [CommandMethod("PlaceEquipmentAlongPerimeterInsideFixed")]
        public void PlaceEquipmentAlongPerimeterInsideFixed()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                ed.WriteMessage("\nВыберите замкнутую полилинию — периметр торгового зала (по часовой стрелке):");
                Polyline perimeter = PromptForClosedPolyline(ed, "\nВыберите периметр торгового зала:");
                if (perimeter == null) return;

                ed.WriteMessage("\nВыберите прямоугольник (замкнутую полилинию) с оборудованием:");
                Polyline equipmentRect = PromptForClosedPolyline(ed, "\nВыберите прямоугольник с оборудованием:");
                if (equipmentRect == null) return;

                List<BlockReference> equipmentBlocks = GetBlocksInsidePolyline(db, equipmentRect, ed);
                ed.WriteMessage($"\nНайдено блоков оборудования внутри прямоугольника: {equipmentBlocks.Count}");
                if (equipmentBlocks.Count == 0) return;

                List<Tuple<Point3d, Point3d>> segments = GetPerimeterSegments(perimeter);
                ed.WriteMessage($"\nПериметр разбит на {segments.Count} сегментов.");

                Point3d startPt = PromptForPointOnPolyline(ed, perimeter, "\nВыберите точку старта размещения:");
                if (startPt == Point3d.Origin) return;

                Point3d endPt = PromptForPointOnPolyline(ed, perimeter, "\nВыберите точку окончания размещения:");
                if (endPt == Point3d.Origin) return;

                int startSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, startPt);
                int endSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, endPt);

                if (startSegmentIndex == -1 || endSegmentIndex == -1)
                {
                    ed.WriteMessage("\nТочки старта или финиша не лежат на периметре.");
                    return;
                }

                List<int> segmentOrder = GetSegmentOrder(startSegmentIndex, endSegmentIndex, segments.Count);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int blockIndex = 0;

                    foreach (int segIdx in segmentOrder)
                    {
                        if (blockIndex >= equipmentBlocks.Count)
                            break;

                        var segment = segments[segIdx];
                        Point3d segStart = segment.Item1;
                        Point3d segEnd = segment.Item2;

                        // ВАЖНО: если это стартовый сегмент, позиция начала равна точке старта (без смещения!)
                        Point3d placeStart = (segIdx == startSegmentIndex) ? startPt : segStart;
                        // Аналогично для конечного сегмента
                        Point3d placeEnd = (segIdx == endSegmentIndex) ? endPt : segEnd;

                        Vector3d segVector = placeEnd - placeStart;
                        double segLength = segVector.Length;
                        Vector3d segDirection = segVector.GetNormal();

                        // Получаем нормаль, но она теперь нужна только если бы смещали внутрь; тут осталась для возможных проверок
                        Vector3d insideNormal = GetInsideNormal(perimeter, segIdx);

                        double currentOffset = 0.0;

                        while (blockIndex < equipmentBlocks.Count && currentOffset <= segLength)
                        {
                            var blk = equipmentBlocks[blockIndex];
                            Extents3d extents = blk.GeometricExtents;

                            double blockLength = extents.MaxPoint.X - extents.MinPoint.X; // длинная сторона - X
                            double blockWidth = extents.MaxPoint.Y - extents.MinPoint.Y;

                            // Если блок не помещается на оставшемся отрезке - выходим из цикла по сегменту
                            if (currentOffset + blockLength > segLength)
                                break;

                            // Позиция блока на линии периметра - длинная сторона блока ориентирована вдоль сегмента
                            Point3d positionOnSegment = placeStart + segDirection.MultiplyBy(currentOffset + blockLength / 2);

                            // Убираем сдвиг внутрь, позиция ровно по линии
                            Point3d blockPosition = positionOnSegment; 

                            // Поворот: длинная сторона вдоль направления сегмента
                            double rotationAngle = Math.Atan2(segDirection.Y, segDirection.X);

                            // Создаём блок в нужном месте с поворотом
                            CreateBlockReference(tr, ms, blk.Name, blockPosition, rotationAngle);

                            ed.WriteMessage($"\nРазмещён блок '{blk.Name}' с поворотом {rotationAngle * 180.0 / Math.PI:F1}° на позиции {blockPosition}");

                            currentOffset += blockLength + GapBetweenBlocks;
                            blockIndex++;
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage($"\nРазмещено блоков: {blockIndex} из {equipmentBlocks.Count}");
                    if (blockIndex < equipmentBlocks.Count)
                        ed.WriteMessage($"\nНе размещено блоков: {equipmentBlocks.Count - blockIndex}");
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

        // Вычисление нормали внутрь полигона для возможного использования (оставляем на будущее)
        private Vector3d GetInsideNormal(Polyline perimeter, int segIdx)
        {
            int n = perimeter.NumberOfVertices;

            Point2d p0 = perimeter.GetPoint2dAt(segIdx);
            Point2d p1 = perimeter.GetPoint2dAt((segIdx + 1) % n);

            Vector2d edgeVector = p1 - p0;
            Vector2d normal = edgeVector.GetNormal();

            Vector2d leftNormal = new Vector2d(-normal.Y, normal.X);

            Point2d testPoint = p0 + leftNormal * 0.1;

            bool isInside = IsPointInPolyline(perimeter, testPoint);
            if (isInside)
            {
                return new Vector3d(leftNormal.X, leftNormal.Y, 0);
            }
            else
            {
                Vector2d rightNormal = new Vector2d(normal.Y, -normal.X);
                return new Vector3d(rightNormal.X, rightNormal.Y, 0);
            }
        }

        // --- Вспомогательные методы из вашего исходного кода (без изменений) ---

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
