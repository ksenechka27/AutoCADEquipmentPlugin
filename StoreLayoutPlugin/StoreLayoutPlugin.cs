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

        [CommandMethod("PlaceEquipmentAlongPerimeterFull")]
        public void PlaceEquipmentAlongPerimeterFull()
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

                // Формируем последовательность всех сегментов периметра
                List<int> segmentOrder = new List<int>();
                for (int i = 0; i < segments.Count; i++)
                    segmentOrder.Add(i);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int blockIndex = 0;

                    // **Объявляем здесь переменную, чтобы не получить ошибку "не существует в текущем контексте"**
                    List<Extents3d> placedBlockExtents = new List<Extents3d>();

                    foreach (int segIdx in segmentOrder)
                    {
                        if (blockIndex >= equipmentBlocks.Count)
                            break;

                        var segment = segments[segIdx];
                        Point3d segStart = segment.Item1;
                        Point3d segEnd = segment.Item2;

                        Vector3d segVector = segEnd - segStart;
                        double segLength = segVector.Length;
                        Vector3d segDirection = segVector.GetNormal();
                        Vector3d segNormal = new Vector3d(-segDirection.Y, segDirection.X, 0);

                        double currentOffset = 0.0;

                        while (blockIndex < equipmentBlocks.Count && currentOffset <= segLength)
                        {
                            var blk = equipmentBlocks[blockIndex];
                            Extents3d extents = blk.GeometricExtents;

                            double sizeX = extents.MaxPoint.X - extents.MinPoint.X;
                            double sizeY = extents.MaxPoint.Y - extents.MinPoint.Y;

                            Vector2d basePointOffset = GetBlockBasePointOffset(blk, extents);

                            bool placed = false;

                            for (int orientationTry = 0; orientationTry < 2 && !placed; orientationTry++)
                            {
                                bool isLongSideX = (orientationTry == 0) ? (sizeX >= sizeY) : (sizeX < sizeY);
                                double blockLength = isLongSideX ? sizeX : sizeY;

                                if (currentOffset + blockLength > segLength)
                                {
                                    break;
                                }

                                Point3d centerPos = segStart + segDirection.MultiplyBy(currentOffset + blockLength / 2);

                                Point3d correctedPosition = centerPos
                                    - segDirection.MultiplyBy(basePointOffset.X)
                                    - segNormal.MultiplyBy(basePointOffset.Y);

                                double rotationAngle = Math.Atan2(segDirection.Y, segDirection.X);
                                if (!isLongSideX)
                                {
                                    rotationAngle += Math.PI / 2.0;
                                }

                                Extents3d rotatedExtents = GetRotatedExtents(extents, correctedPosition, rotationAngle, basePointOffset);

                                if (!IsExtentsInsidePolyline(perimeter, rotatedExtents))
                                    continue;

                                bool intersects = false;
                                foreach (var placedExt in placedBlockExtents)
                                {
                                    if (DoExtentsIntersect(placedExt, rotatedExtents))
                                    {
                                        intersects = true;
                                        break;
                                    }
                                }
                                if (intersects)
                                    continue;

                                CreateBlockReference(tr, ms, blk.Name, correctedPosition, rotationAngle);
                                placedBlockExtents.Add(rotatedExtents);

                                ed.WriteMessage($"\nРазмещён блок '{blk.Name}' с поворотом {rotationAngle * 180.0 / Math.PI:F1}° на позиции {correctedPosition}");

                                currentOffset += blockLength + GapBetweenBlocks;
                                blockIndex++;
                                placed = true;
                            }

                            if (!placed)
                                break; // Не удалось разместить блок на этом сегменте - выходим к следующему сегменту
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage($"\nРазмещено блоков: {placedBlockExtents.Count} из {equipmentBlocks.Count}");
                    if (placedBlockExtents.Count < equipmentBlocks.Count)
                        ed.WriteMessage($"\nНе размещено блоков: {equipmentBlocks.Count - placedBlockExtents.Count}");
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

        private Vector2d GetBlockBasePointOffset(BlockReference blk, Extents3d extents)
        {
            Point3d basePointWorld = blk.Position;
            double centerX = (extents.MinPoint.X + extents.MaxPoint.X) / 2.0;
            double centerY = (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0;

            double offsetX = basePointWorld.X - centerX;
            double offsetY = basePointWorld.Y - centerY;

            return new Vector2d(offsetX, offsetY);
        }

        private Extents3d GetRotatedExtents(Extents3d extents, Point3d position, double rotation, Vector2d basePointOffset)
        {
            Point3d min = extents.MinPoint;
            Point3d max = extents.MaxPoint;

            Point3d[] corners = new Point3d[4];
            corners[0] = new Point3d(min.X, min.Y, 0);
            corners[1] = new Point3d(max.X, min.Y, 0);
            corners[2] = new Point3d(max.X, max.Y, 0);
            corners[3] = new Point3d(min.X, max.Y, 0);

            double centerX = (min.X + max.X) / 2.0;
            double centerY = (min.Y + max.Y) / 2.0;

            var rotatedPoints = new List<Point3d>();

            foreach (var pt in corners)
            {
                double dx = pt.X - centerX - basePointOffset.X;
                double dy = pt.Y - centerY - basePointOffset.Y;

                double rotatedX = dx * Math.Cos(rotation) - dy * Math.Sin(rotation);
                double rotatedY = dx * Math.Sin(rotation) + dy * Math.Cos(rotation);

                rotatedPoints.Add(new Point3d(rotatedX + position.X, rotatedY + position.Y, 0));
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var pt in rotatedPoints)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            return new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
        }

        private bool IsExtentsInsidePolyline(Polyline polyline, Extents3d extents)
        {
            Point2d[] pointsToCheck = new Point2d[]
            {
                new Point2d(extents.MinPoint.X, extents.MinPoint.Y),
                new Point2d(extents.MinPoint.X, extents.MaxPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MinPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y)
            };

            foreach (var pt in pointsToCheck)
            {
                if (!IsPointInPolyline(polyline, pt))
                    return false;
            }
            return true;
        }

        private bool DoExtentsIntersect(Extents3d ext1, Extents3d ext2)
        {
            return !(ext1.MaxPoint.X < ext2.MinPoint.X ||
                     ext1.MinPoint.X > ext2.MaxPoint.X ||
                     ext1.MaxPoint.Y < ext2.MinPoint.Y ||
                     ext1.MinPoint.Y > ext2.MaxPoint.Y);
        }

        private Polyline PromptForClosedPolyline(Editor ed, string prompt)
        {
            var peo = new PromptEntityOptions(prompt);
            peo.SetRejectMessage("\nВыберите замкнутую полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);

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
