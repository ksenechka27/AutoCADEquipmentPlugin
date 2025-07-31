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
            Editor ed = doc.Editor;
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Очистка старых блоков
                if (clearOld)
                {
                    Utils.ClearPreviousBlocks(blockName, tr);
                }

                // Выбор полилинии области расстановки
                PromptEntityOptions peo = new PromptEntityOptions("\nВыберите внутреннюю полилинию зоны расстановки:");
                peo.SetRejectMessage("Только полилиния.");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;
                Polyline zonePolyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (!zonePolyline.Closed)
                {
                    ed.WriteMessage("\nПолилиния должна быть замкнута.");
                    return;
                }

                // Ввод начальной и конечной точки пользователем
                PromptPointResult startRes = ed.GetPoint("\nУкажите начальную точку размещения:");
                if (startRes.Status != PromptStatus.OK) return;
                Point3d startPoint = startRes.Value;

                PromptPointResult endRes = ed.GetPoint("\nУкажите конечную точку размещения:");
                if (endRes.Status != PromptStatus.OK) return;
                Point3d endPoint = endRes.Value;

                // Получение определения блока
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (!bt.Has(blockName))
                {
                    ed.WriteMessage("\nБлок с именем " + blockName + " не найден.");
                    return;
                }
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);

                // Границы блока
                Extents3d? blockExt = Utils.GetBlockExtents(btr);
                if (blockExt == null)
                {
                    ed.WriteMessage("\nНевозможно определить габариты блока.");
                    return;
                }

                Vector3d totalDirection = endPoint - startPoint;

                List<SegmentInfo> segments = Utils.ExtractSegments(zonePolyline);
                Point3d currentPos = startPoint;
                Vector3d moveDir = totalDirection.GetNormal();

                foreach (var seg in segments)
                {
                    Vector3d segDir = (seg.End - seg.Start).GetNormal();
                    double segmentLength = seg.Start.DistanceTo(seg.End);
                    Point3d blockPos = seg.Start;

                    // Ориентируем блок вдоль сегмента
                    double angle = segDir.AngleOnPlane(new Plane(Point3d.Origin, Vector3d.ZAxis));
                    double step = blockExt.Value.MaxPoint.X - blockExt.Value.MinPoint.X + offset;

                    while (blockPos.DistanceTo(seg.End) >= step)
                    {
                        // Проверка попадания внутрь зоны
                        if (!Utils.IsPointInside(zonePolyline, blockPos))
                        {
                            blockPos = blockPos + segDir.MultiplyBy(step);
                            continue;
                        }

                        // Проверка на пересечения с объектами
                        if (Utils.HasIntersections(blockPos, angle, btr, tr))
                        {
                            blockPos = blockPos + segDir.MultiplyBy(step);
                            continue;
                        }

                        // Вставка блока
                        BlockReference br = new BlockReference(blockPos, btr.ObjectId);
                        br.Rotation = angle;
                        BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        modelSpace.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);

                        blockPos = blockPos + segDir.MultiplyBy(step);
                    }
                }

                tr.Commit();
            }
        }

        private struct SegmentInfo
        {
            public Point3d Start;
            public Point3d End;
        }
    }
}
