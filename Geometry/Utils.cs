using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class GeometryUtils
    {
        // Проверка: точка внутри замкнутой полилинии
        public static bool IsPointInside(Polyline polyline, Point3d point)
        {
            var pts = new Point3dCollection();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                pts.Add(polyline.GetPoint3dAt(i));
            }

            return IsPointInside(pts, point);
        }

        // Вспомогательный метод: точка в многоугольнике (Ray Casting)
        public static bool IsPointInside(Point3dCollection polygon, Point3d point)
        {
            int crossings = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Point3d a = polygon[i];
                Point3d b = polygon[(i + 1) % polygon.Count];

                if (((a.Y <= point.Y) && (b.Y > point.Y)) || ((a.Y > point.Y) && (b.Y <= point.Y)))
                {
                    double vt = (double)(point.Y - a.Y) / (b.Y - a.Y);
                    double intersectX = a.X + vt * (b.X - a.X);
                    if (point.X < intersectX)
                        crossings++;
                }
            }
            return (crossings % 2) != 0;
        }

        // Проверка пересечений с другими объектами в модели
        public static bool IntersectsOther(BlockTableRecord ms, BlockReference br, Transaction tr)
        {
            if (br.Bounds == null) return false;

            Extents3d brBounds = br.Bounds.Value;

            foreach (ObjectId id in ms)
            {
                if (id == br.ObjectId) continue;

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.Bounds == null) continue;

                Extents3d entBounds = ent.Bounds.Value;

                // Простая проверка на пересечение границ
                if (brBounds.MinPoint.X <= entBounds.MaxPoint.X && brBounds.MaxPoint.X >= entBounds.MinPoint.X &&
                    brBounds.MinPoint.Y <= entBounds.MaxPoint.Y && brBounds.MaxPoint.Y >= entBounds.MinPoint.Y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
