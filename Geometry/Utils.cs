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
            return poly.Contains(point);
        }

        public static bool IntersectsOtherObjects(BlockTableRecord modelSpace, BlockReference br, Transaction tr)
        {
            Extents3d? ext = br.Bounds;
            if (ext == null) return false;

            foreach (ObjectId entId in modelSpace)
            {
                if (entId == br.ObjectId) continue;
                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null || ent is BlockReference == false) continue;

                try
                {
                    if (ent.GeometricExtents.IntersectWith(ext.Value) != null)
                        return true;
                }
                catch { continue; }
            }

            return false;
        }
    }
}
