using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;
using AutoCADEquipmentPlugin.Geometry;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void PlaceBlocks(List<string> blockNames, Polyline boundary, Point3d entryPoint, Point3d exitPoint)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                List<Entity> obstacles = GetObstacles(ms, tr);

                double offset = 300; // отступ между блоками, мм
                Point3d currentPos = entryPoint;

                foreach (var name in blockNames)
                {
                    if (!bt.Has(name)) continue;
                    BlockTableRecord brDef = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);

                    using (BlockReference br = new BlockReference(currentPos, brDef.ObjectId))
                    {
                        ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);

                        if (br.Bounds.HasValue)
                        {
                            Point3d center = br.Bounds.Value.Center();

                            if (!GeometryUtils.IsPointInside(boundary, center) || GeometryUtils.IntersectsOther(ms, br, tr))
                            {
                                br.Erase();
                                continue;
                            }

                            currentPos = new Point3d(currentPos.X + offset, currentPos.Y, currentPos.Z);
                        }
                    }
                }

                tr.Commit();
            }
        }

        private static List<Entity> GetObstacles(BlockTableRecord ms, Transaction tr)
        {
            List<Entity> obstacles = new List<Entity>();

            foreach (ObjectId id in ms)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent is BlockReference || ent is Polyline) continue;

                obstacles.Add(ent);
            }

            return obstacles;
        }
    }
}
