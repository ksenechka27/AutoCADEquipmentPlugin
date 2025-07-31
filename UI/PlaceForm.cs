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
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Выбор полилинии — это будет область расстановки
                PromptEntityOptions peo = new PromptEntityOptions("\nВыберите полилинию области расстановки оборудования: ");
                peo.SetRejectMessage("\nНужна именно полилиния.");
                peo.AddAllowedClass(typeof(Polyline), false);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                ObjectId polyId = per.ObjectId;

                // Выбор точки начала расстановки (вход)
                PromptPointResult pStart = ed.GetPoint("\nУкажите точку начала расстановки (вход): ");
                if (pStart.Status != PromptStatus.OK) return;

                // Выбор точки конца расстановки (выход)
                PromptPointResult pEnd = ed.GetPoint("\nУкажите точку конца расстановки (выход): ");
                if (pEnd.Status != PromptStatus.OK) return;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline boundary = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                    if (boundary == null) return;

                    // Получение всех вершин полилинии как маршрута вдоль стен
                    List<Point2d> wallPath = new List<Point2d>();
                    for (int i = 0; i < boundary.NumberOfVertices; i++)
                        wallPath.Add(boundary.GetPoint2dAt(i));

                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (!bt.Has(blockName))
                    {
                        ed.WriteMessage($"\nБлок с именем \"{blockName}\" не найден.");
                        return;
                    }

                    if (clearOld)
                        ClearExistingBlocks(tr, db, blockName);

                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    BlockTableRecord blockDef = tr.GetObject(bt[blockName], OpenMode.ForRead) as BlockTableRecord;

                    double blockLength = GetBlockBoundingLength(blockDef);
                    double blockWidth = GetBlockBoundingWidth(blockDef);

                    // Проходим по отрезкам маршрута вдоль границ
                    Point2d currentPos = FindNearestPointOnPath(wallPath, pStart.Value.Convert2d());

                    while (true)
                    {
                        Vector2d direction = GetNextDirection(wallPath, currentPos, pEnd.Value.Convert2d());
                        if (direction.IsZeroLength()) break;

                        // Позиция для установки блока
                        Point3d insertPt = new Point3d(currentPos.X, currentPos.Y, 0);

                        // Проверка, не выйдет ли блок за границы
                        if (!IsBlockInsideBoundary(boundary, insertPt, direction, blockLength, blockWidth)) break;

                        // Вставка блока
                        BlockReference br = new BlockReference(insertPt, blockDef.ObjectId);
                        br.Rotation = direction.Angle;
                        br.ScaleFactors = new Scale3d(1);

                        if (!IsIntersecting(tr, db, br))
                        {
                            modelSpace.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                        }
                        else
                        {
                            br.Dispose();
                            break; // пересечение — прерываем
                        }

                        // Продвигаемся по направлению на длину блока + отступ
                        currentPos = new Point2d(
                            currentPos.X + direction.X * (blockLength + offset),
                            currentPos.Y + direction.Y * (blockLength + offset)
                        );
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nОшибка: " + ex.Message);
            }
        }

        // Удаление старых блоков
        private static void ClearExistingBlocks(Transaction tr, Database db, string blockName)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (ObjectId entId in modelSpace)
            {
                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent is BlockReference br && br.Name == blockName)
                {
                    br.UpgradeOpen();
                    br.Erase();
                }
            }
        }

        private static double GetBlockBoundingLength(BlockTableRecord blockDef)
        {
            Extents3d? ext = blockDef.Bounds;
            return ext.HasValue ? ext.Value.MaxPoint.X - ext.Value.MinPoint.X : 1.0;
        }

        private static double GetBlockBoundingWidth(BlockTableRecord blockDef)
        {
            Extents3d? ext = blockDef.Bounds;
            return ext.HasValue ? ext.Value.MaxPoint.Y - ext.Value.MinPoint.Y : 1.0;
        }

        private static bool IsBlockInsideBoundary(Polyline boundary, Point3d pt, Vector2d dir, double len, double wid)
        {
            Point3d[] corners = new Point3d[4];
            Vector3d right = dir.GetPerpendicularVector().GetNormal().ToVector3d() * wid;
            Vector3d forward = dir.ToVector3d().GetNormal() * len;

            corners[0] = pt;
            corners[1] = pt + forward;
            corners[2] = pt + forward + right;
            corners[3] = pt + right;

            return corners.All(c => boundary.IsPointInside(c.Convert2d(), Tolerance.Global));
        }

        private static Vector2d GetNextDirection(List<Point2d> path, Point2d current, Point2d end)
        {
            foreach (var pt in path)
            {
                if (pt.DistanceTo(current) < 1e-4) continue;
                Vector2d dir = pt - current;
                if (!dir.IsZeroLength()) return dir.GetNormal();
            }
            return new Vector2d(0, 0);
        }

        private static Point2d FindNearestPointOnPath(List<Point2d> path, Point2d refPoint)
        {
            return path.OrderBy(p => p.GetDistanceTo(refPoint)).First();
        }

        private static bool IsIntersecting(Transaction tr, Database db, BlockReference br)
        {
            // Можно сделать точную проверку пересечений здесь
            return false;
        }
    }

    static class Extensions
    {
        public static Point2d Convert2d(this Point3d pt) => new Point2d(pt.X, pt.Y);

        public static bool IsPointInside(this Polyline pl, Point2d pt, Tolerance tol)
        {
            return pl.IsInside(pt, tol.EqualPoint, false);
        }

        public static Vector3d ToVector3d(this Vector2d v) => new Vector3d(v.X, v.Y, 0);
    }
}
