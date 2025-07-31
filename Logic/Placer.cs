using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AutoCADEquipmentPlugin.Geometry;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void PlaceBlocks(List<string> blockNames, Polyline boundary, Point3d entry, Point3d exit)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                List<Entity> obstacles = CollectObstacles(ms, tr);
                List<Point3d> wallPoints = ExtractWallPoints(boundary);

                foreach (string blockName in blockNames)
                {
                    if (!bt.Has(blockName))
                        continue;

                    var blockDef = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);

                    foreach (Point3d pt in wallPoints)
                    {
                        if (TryPlace(pt, blockDef, ms, tr, obstacles))
                        {
                            ed.WriteMessage($"\nБлок '{blockName}' размещён.");
                            break;
                        }
                    }
                }

                tr.Commit();
            }
        }

        private static List<Entity> CollectObstacles(BlockTableRecord ms, Transaction tr)
        {
            var list = new List<Entity>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && !(ent is BlockReference) && !(ent is Polyline))
                    list.Add(ent);
            }
            return list;
        }

        private static List<Point3d> ExtractWallPoints(Polyline boundary)
        {
            var pts = new List<Point3d>();
            for (int i = 0; i < boundary.NumberOfVertices; i++)
            {
                pts.Add(boundary.GetPoint3dAt(i));
            }
            return pts;
        }

        private static bool TryPlace(Point3d pos, BlockTableRecord def, BlockTableRecord ms,
                                     Transaction tr, List<Entity> obstacles)
        {
            var br = new BlockReference(pos, def.ObjectId);
            if (!GeometryUtils.IntersectsOther(ms, br, tr))
            {
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                return true;
            }
            br.Dispose();
            return false;
        }
    }
}
