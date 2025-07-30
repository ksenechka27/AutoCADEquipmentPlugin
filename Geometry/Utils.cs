using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace AutoCADEquipmentPlugin
{
    public static class GeometryUtils
    {
        /// <summary>
        /// Проверяет, находится ли точка внутри замкнутого полигона.
        /// </summary>
        public static bool IsPointInside(Point3d point, Polyline polyline)
        {
            if (polyline == null || !polyline.Closed)
                return false;

            int numIntersections = 0;
            Point3d p1, p2;

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                p1 = polyline.GetPoint3dAt(i);
                p2 = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);

                if (IsEdgeCrossingHorizontalRay(point, p1, p2))
                    numIntersections++;
            }

            return numIntersections % 2 == 1;
        }

        /// <summary>
        /// Проверка, пересекает ли отрезок (p1-p2) горизонтальный луч вправо от точки point.
        /// </summary>
        private static bool IsEdgeCrossingHorizontalRay(Point3d point, Point3d p1, Point3d p2)
        {
            if (p1.Y > p2.Y)
            {
                var temp = p1;
                p1 = p2;
                p2 = temp;
            }

            // Быстрая фильтрация по вертикали
            if (point.Y <= p1.Y || point.Y > p2.Y)
                return false;

            // Проверка пересечения по X
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            if (dy == 0)
                return false; // горизонтальный сегмент — не пересекает

            double intersectionX = p1.X + (point.Y - p1.Y) * dx / dy;

            return intersectionX > point.X;
        }
    }
}
