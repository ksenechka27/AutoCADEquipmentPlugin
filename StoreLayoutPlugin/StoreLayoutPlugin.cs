using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

// Определяем алиасы для исключений чтобы избежать неоднозначности
using AcadException = Autodesk.AutoCAD.Runtime.Exception;
using SysException = System.Exception;

[assembly: CommandClass(typeof(AutoEquipPlacementPlugin.PlaceEquipmentCommands))]

namespace AutoEquipPlacementPlugin
{
    public class PlaceEquipmentCommands : IExtensionApplication
    {
        private const double MinGap = 0.1;         // Минимальный зазор между блоками и стеной
        private const double MaxShift = 0.2;       // Максимальное смещение вдоль сегмента (20 см)
        private const double ShiftStep = 0.01;     // Шаг смещения 1 см
        private const double GapBetweenBlocks = 0.1; // Зазор между блоками

        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAutoEquipPlacement Plugin loaded.\n");
        }

        public void Terminate()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAutoEquipPlacement Plugin terminated.\n");
        }

        [CommandMethod("PlaceEquipPerimeter")]
        public void PlaceEquipmentPerimeter()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                var peo = new PromptEntityOptions("\nВыберите замкнутую полилинию (периметр торгового зала по часовой стрелке): ");
                peo.SetRejectMessage("\nВыбран не полилиния.");
                peo.AddAllowedClass(typeof(Polyline), false);
                var resPerim = ed.GetEntity(peo);
                if (resPerim.Status != PromptStatus.OK) return;
                ObjectId perimeterId = resPerim.ObjectId;

                var rectOpt = new PromptEntityOptions("\nВыберите прямоугольник с оборудованием:");
                rectOpt.SetRejectMessage("\nВыбран не полилиния.");
                rectOpt.AddAllowedClass(typeof(Polyline), false);
                var resRect = ed.GetEntity(rectOpt);
                if (resRect.Status != PromptStatus.OK) return;
                ObjectId rectId = resRect.ObjectId;

                var pStartOpt = new PromptPointOptions("\nУкажите начальную точку обхода (должна лежать на периметре): ");
                var resStart = ed.GetPoint(pStartOpt);
                if (resStart.Status != PromptStatus.OK) return;
                Point3d startPt = resStart.Value;

                var pEndOpt = new PromptPointOptions("\nУкажите конечную точку обхода (должна лежать на периметре): ");
                var resEnd = ed.GetPoint(pEndOpt);
                if (resEnd.Status != PromptStatus.OK) return;
                Point3d endPt = resEnd.Value;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var perimeter = (Polyline)tr.GetObject(perimeterId, OpenMode.ForRead);
                    var rect = (Polyline)tr.GetObject(rectId, OpenMode.ForRead);

                    if (!perimeter.Closed)
                    {
                        ed.WriteMessage("\nПериметр должен быть замкнутой полилией.");
                        return;
                    }
                    if (!rect.Closed)
                    {
                        ed.WriteMessage("\nОбласть оборудования должна быть замкнутой полилией.");
                        return;
                    }

                    var segments = GetPerimeterSegments(perimeter);
                    ed.WriteMessage($"\nПериметр разбит на {segments.Count} сегментов.");

                    int startSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, startPt);
                    int endSegmentIndex = FindSegmentIndexByPointOnPolyline(perimeter, endPt);

                    if (startSegmentIndex == -1 || endSegmentIndex == -1)
                    {
                        ed.WriteMessage("\nНачальная или конечная точка не на периметре.");
                        return;
                    }

                    ed.WriteMessage($"\nСтартовый сегмент: {startSegmentIndex}, конечный сегмент: {endSegmentIndex}");

                    var segmentOrder = GetSegmentOrder(startSegmentIndex, endSegmentIndex, segments.Count);

                    var equipmentBlocks = GetBlocksInsidePolyline(db, rect, ed);
                    ed.WriteMessage($"\nНайдено блоков: {equipmentBlocks.Count}");
                    if (equipmentBlocks.Count == 0)
                    {
                        ed.WriteMessage("\nОборудование в выбранной области отсутствует.");
                        return;
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int blockIndex = 0;

                    foreach (int segIdx in segmentOrder)
                    {
                        if (blockIndex >= equipmentBlocks.Count) break;

                        var segment = segments[segIdx];
                        Point3d segStart = segment.Item1;
                        Point3d segEnd = segment.Item2;

                        Point3d placeStart = (segIdx == startSegmentIndex) ? startPt : segStart;
                        Point3d placeEnd = (segIdx == endSegmentIndex) ? endPt : segEnd;

                        Vector3d segVector = placeEnd - placeStart;
                        double segLength = segVector.Length;
                        Vector3d segDir = segVector.GetNormal();

                        double currentOffset = 0.0;

                        while (blockIndex < equipmentBlocks.Count && currentOffset < segLength)
                        {
                            var blk = equipmentBlocks[blockIndex];
                            Extents3d extents = blk.GeometricExtents;
                            double length = extents.MaxPoint.X - extents.MinPoint.X;

                            // Проверка, укладывается ли блок с учетом зазора GapBetweenBlocks
                            if (currentOffset + length + GapBetweenBlocks <= segLength)
                            {
                                Point3d position = placeStart + segDir * currentOffset;

                                // Вставляем блок с поворотом вдоль сегмента
                                CreateBlockReference(tr, ms, blk.Name, position, Math.Atan2(segDir.Y, segDir.X));

                                ed.WriteMessage($"\nРазмещён блок '{blk.Name}' с поворотом {Math.Atan2(segDir.Y, segDir.X) * 180 / Math.PI:F1}° на позиции {position}");

                                currentOffset += length + GapBetweenBlocks;
                                blockIndex++;
                            }
                            else
                            {
                                // Если блок не помещается, переходим к следующему сегменту
                                break;
                            }
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage($"\nРазмещено блоков: {blockIndex} из {equipmentBlocks.Count}");
                    if (blockIndex < equipmentBlocks.Count)
                        ed.WriteMessage($"\nНе размещено блоков: {equipmentBlocks.Count - blockIndex}");
                }
            }
            catch (AcadException ex)
            {
                ed.WriteMessage("\nAutoCAD Runtime ошибка: " + ex.Message);
            }
            catch (SysException ex)
            {
                ed.WriteMessage("\nОбщая ошибка: " + ex.Message);
            }
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
                            blocks.Add(br);
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
