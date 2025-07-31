using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class GeometryUtils
    {
        /// <summary>
        /// Проверка, находится ли точка внутри полилинии.
        /// </summary>
        public static bool IsPointInside(Polyline poly, Point3d pt)
        {
            var cs = poly.GetPlane().GetCoordinateSystem();
            var pt2d = pt.Convert2d(cs);
            return poly.IsPointInside(pt2d, Tolerance.Global, false);
        }

        /// <summary>
        /// Проверка пересечения блока с другими объектами (по габаритам).
        /// </summary>
        public static bool IntersectsOther(BlockTableRecord ms, BlockReference br, Transaction tr)
        {
            if (!br.Bounds.HasValue) return false;

            Extents3d brBounds = br.Bounds.Value;

            foreach (ObjectId id in ms)
            {
                if (id == br.ObjectId) continue;

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || !ent.Bounds.HasValue) continue;

                Extents3d entBounds = ent.Bounds.Value;

                bool intersects =
                    brBounds.MinPoint.X <= entBounds.MaxPoint.X &&
                    brBounds.MaxPoint.X >= entBounds.MinPoint.X &&
                    brBounds.MinPoint.Y <= entBounds.MaxPoint.Y &&
                    brBounds.MaxPoint.Y >= entBounds.MinPoint.Y;

                if (intersects)
                    return true;
            }

            return false;
        }
    }
}
