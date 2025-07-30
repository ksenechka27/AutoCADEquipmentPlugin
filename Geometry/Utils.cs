using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class Utils
    {
        public static bool IsPointInside(Polyline poly, Point3d p)
        {
            return poly.GetArea() > 0 && poly.IsPointInside(p, new Tolerance(1e-6, 1e-4), true);
        }

        public static bool IntersectsOther(BlockTableRecord ms, BlockReference br, Transaction tr)
        {
            var bb = br.Bounds;
            if (!bb.HasValue) return false;

            foreach (var oid in ms)
            {
                if (oid == br.ObjectId) continue;
                var ent = tr.GetObject(oid, OpenMode.ForRead) as Entity;
                if (ent is BlockReference obr)
                {
                    var ob = obr.Bounds;
                    if (ob.HasValue &&
                        bb.Value.MinPoint.X < ob.Value.MaxPoint.X &&
                        bb.Value.MaxPoint.X > ob.Value.MinPoint.X &&
                        bb.Value.MinPoint.Y < ob.Value.MaxPoint.Y &&
                        bb.Value.MaxPoint.Y > ob.Value.MinPoint.Y)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
