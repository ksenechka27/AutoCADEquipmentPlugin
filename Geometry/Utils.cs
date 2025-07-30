using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AutoCADEquipmentPlugin.Geometry
{
    public static class GeometryUtils
    {
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
