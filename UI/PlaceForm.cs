using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;
using System.Linq;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Utils
    {
        // Получить габариты блока
        public static Extents3d? GetBlockExtents(BlockReference br)
        {
            try
            {
                var ext = br.GeometricExtents;
                return ext;
            }
            catch
            {
                return null;
            }
        }

        // Преобразовать полилинию в список отрезков
        public static List<LineSegment2d> ExtractSegments(Polyline pline)
        {
            var segments = new List<LineSegment2d>();
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                int next = (i + 1) % pline.NumberOfVertices;
                var p1 = pline.GetPoint2dAt(i);
                var p2 = pline.GetPoint2dAt(next);
                segments.Add(new LineSegment2d(p1, p2));
            }
            return segments;
        }

        // Проверка: точка находится внутри замкнутой полилинии
        public static bool IsPointInside(Polyline pline, Point2d pt)
        {
            return pline.IsPointInside(pt, Tolerance.Global, true);
        }

        // Проверка: будет ли вставка блока пересекаться с другими объектами
        public static bool HasIntersections(Transaction tr, BlockReference newBlock, BlockTableRecord btr, Polyline boundary)
        {
            var db = HostApplicationServices.WorkingDatabase;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            // Получаем предполагаемый экстент вставляемого блока
            var ext = GetBlockExtents(newBlock);
            if (ext == null) return true;

            // Получаем центр блока для быстрой проверки
            var center = (ext.Value.MinPoint + ext.Value.MaxPoint) / 2.0;
            if (!IsPointInside(boundary, new Point2d(center.X, center.Y)))
                return true;

            // Пройтись по всем объектам и проверить пересечение
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent is BlockReference existingBr)
                {
                    if (existingBr.GeometricExtents.IntersectWith(ext.Value) != null)
                        return true;
                }
                else if (ent != null && ent.Bounds != null)
                {
                    if (ent.Bounds.Value.IntersectWith(ext.Value) != null)
                        return true;
                }
            }

            return false;
        }

        // Угол между двумя точками (в радианах)
        public static double GetAngle(Point2d from, Point2d to)
        {
            return (to - from).Angle;
        }

        // Создаёт нормализованный вектор длины length по сегменту
        public static Vector2d GetDirection(LineSegment2d seg)
        {
            return (seg.EndPoint - seg.StartPoint).GetNormal();
        }
    }
}
