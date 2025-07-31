using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class GeometryUtils
    {
        /// <summary>Проверка, находится ли точка внутри 2D-полилинии.</summary>
        public static bool IsPointInside(this Polyline poly, Point3d pt)
        {
            // Алгоритм "ray casting" по Y
            var point2d = new Point2d(pt.X, pt.Y);
            int crossings = 0;
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                Point2d a = poly.GetPoint2dAt(i);
                Point2d b = poly.GetPoint2dAt((i + 1) % poly.NumberOfVertices);

                if (((a.Y <= point2d.Y && point2d.Y < b.Y) || (b.Y <= point2d.Y && point2d.Y < a.Y)) &&
                    (point2d.X < (b.X - a.X) * (point2d.Y - a.Y) / (b.Y - a.Y + 1e-9) + a.X))
                {
                    crossings++;
                }
            }
            return (crossings % 2) == 1;
        }

        /// <summary>Проверка пересечения блока с другими объектами по габаритам.</summary>
        public static bool IntersectsOther(BlockTableRecord ms, BlockReference br, Transaction tr)
        {
            if (!br.Bounds.HasValue) return false;
            Extents3d bb = br.Bounds.Value;

            foreach (ObjectId id in ms)
            {
                if (id == br.ObjectId) continue;
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || !ent.Bounds.HasValue) continue;

                Extents3d eb = ent.Bounds.Value;
                if (bb.MinPoint.X <= eb.MaxPoint.X &&
                    bb.MaxPoint.X >= eb.MinPoint.X &&
                    bb.MinPoint.Y <= eb.MaxPoint.Y &&
                    bb.MaxPoint.Y >= eb.MinPoint.Y)
                {
                    return true;
                }
            }
            return false;
        }

        public static Point3d Center(this Extents3d ext)
        {
            return new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                (ext.MinPoint.Y + ext.MaxPoint.Y) / 2,
                (ext.MinPoint.Z + ext.MaxPoint.Z) / 2
            );
        }
    }
}
