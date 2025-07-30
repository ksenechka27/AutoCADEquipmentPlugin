using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using System;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class Utils
    {
        public static bool IsPointInside(Polyline poly, Point3d point)
        {
            return poly.GetDistAtPoint(poly.GetClosestPointTo(point, false)) >= 0;
        }

        public static bool IntersectsOtherObjects(BlockTableRecord modelSpace, BlockReference br, Transaction tr)
        {
            if (!br.Bounds.HasValue) return false;
            Extents3d brExt = br.Bounds.Value;

            foreach (ObjectId entId in modelSpace)
            {
                if (entId == br.ObjectId) continue;

                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null || ent is BlockReference == false || !ent.Bounds.HasValue) continue;

                Extents3d entExt = ent.Bounds.Value;

                if (IsOverlap(brExt, entExt))
                    return true;
            }

            return false;
        }

        private static bool IsOverlap(Extents3d a, Extents3d b)
        {
            return !(a.MaxPoint.X < b.MinPoint.X || a.MinPoint.X > b.MaxPoint.X ||
                     a.MaxPoint.Y < b.MinPoint.Y || a.MinPoint.Y > b.MaxPoint.Y);
        }
    }
}
