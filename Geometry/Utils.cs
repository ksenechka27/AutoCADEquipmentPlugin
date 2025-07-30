using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class GeometryUtils
    {
        public static bool IsPointInside(this Polyline pline, Point2d pt, Tolerance tolerance)
        {
            if (!pline.Closed) return false;

            int windingNumber = 0;
            int numVerts = pline.NumberOfVertices;

            for (int i = 0; i < numVerts; i++)
            {
                Point2d p1 = pline.GetPoint2dAt(i);
                Point2d p2 = pline.GetPoint2dAt((i + 1) % numVerts);

                if (p1.Y <= pt.Y)
                {
                    if (p2.Y > pt.Y && IsLeft(p1, p2, pt) > 0)
                        windingNumber++;
                }
                else
                {
                    if (p2.Y <= pt.Y && IsLeft(p1, p2, pt) < 0)
                        windingNumber--;
                }
            }

            return windingNumber != 0;
        }

        private static double IsLeft(Point2d p0, Point2d p1, Point2d p2)
        {
            return (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
        }
    }
}
