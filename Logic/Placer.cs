using System;
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
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                List<Entity> existingBlocks = new List<Entity>();
                List<Entity> obstacles = GetObstacles(tr, btr);
                List<Point3d> wallPoints = GetWallPoints(boundary);

                foreach (ObjectId id in btr)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference br)
                        existingBlocks.Add(br);
                }

                double offset = 500.0;
                foreach (string blockName in blockNames)
                {
                    if (!bt.Has(blockName)) continue;

                    BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);

                    foreach (Point3d pt in wallPoints)
                    {
                        if (TryPlaceBlock(blockDef, pt, offset, existingBlocks, obstacles, btr, tr))
                        {
                            ed.WriteMessage($"\nБлок '{blockName}' размещён.");
                            break;
                        }
                    }
                }

                tr.Commit();
            }
        }

        private static List<Point3d> GetWallPoints(Polyline boundary)
        {
            List<Point3d> points = new List<Point3d>();
            for (int i = 0; i < boundary.NumberOfVertices; i++)
                points.Add(boundary.GetPoint3dAt(i));
            return points;
        }

        private static List<Entity> GetObstacles(Transaction tr, BlockTableRecord btr)
        {
            List<Entity> result = new List<Entity>();
            foreach (ObjectId id in btr)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && !(ent is Polyline) && !(ent is BlockReference))
                {
                    result.Add(ent);
                }
            }
            return result;
        }

        private static bool TryPlaceBlock(BlockTableRecord blockDef, Point3d position, double offset, List<Entity> existingBlocks, List<Entity> obstacles, BlockTableRecord btr, Transaction tr)
        {
            Point3d basePoint = new Point3d(position.X + offset, position.Y, 0);
            BlockReference br = new BlockReference(basePoint, blockDef.ObjectId);

            if (!IntersectsAny(br, existingBlocks) && !IntersectsAny(br, obstacles))
            {
                btr.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                existingBlocks.Add(br);
                return true;
            }

            br.Dispose();
            return false;
        }

        private static bool IntersectsAny(BlockReference br, List<Entity> entities)
        {
            if (!br.Bounds.HasValue)
                return false;

            Extents3d brBounds = br.Bounds.Value;

            foreach (var ent in entities)
            {
                if (!ent.Bounds.HasValue)
                    continue;

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
