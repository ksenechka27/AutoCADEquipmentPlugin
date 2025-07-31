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
        public static void PlaceEquipmentAlongWalls(Polyline roomPolyline, Point3d entry, Point3d exit, List<(string blockName, int count, double offset)> blocksToPlace)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                List<Entity> obstacles = GetObstacles(ms, tr);

                foreach (var (blockName, count, offset) in blocksToPlace)
                {
                    if (!bt.Has(blockName))
                    {
                        ed.WriteMessage($"\nБлок {blockName} не найден.");
                        continue;
                    }

                    BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                    int placedCount = 0;

                    foreach (Segment segment in GetSegments(roomPolyline))
                    {
                        if (placedCount >= count)
                            break;

                        Point3d currentPoint = segment.StartPoint;
                        Vector3d direction = (segment.EndPoint - segment.StartPoint).GetNormal();

                        while (placedCount < count)
                        {
                            Matrix3d transform = Matrix3d.Displacement(currentPoint - Point3d.Origin);
                            BlockReference br = new BlockReference(Point3d.Origin, blockDef.ObjectId);
                            br.TransformBy(transform);

                            if (GeometryUtils.IsPointInside(roomPolyline, currentPoint) &&
                                !GeometryUtils.IntersectsOther(ms, br, tr) &&
                                !IntersectsObstacles(br, obstacles))
                            {
                                ms.AppendEntity(br);
                                tr.AddNewlyCreatedDBObject(br, true);
                                placedCount++;
                                currentPoint += direction.MultiplyBy(offset);
                            }
                            else
                            {
                                // сдвинемся дальше по сегменту на offset
                                currentPoint += direction.MultiplyBy(offset);
                            }

                            if ((currentPoint - segment.EndPoint).Length < offset)
                                break;
                        }
                    }
                }

                tr.Commit();
            }
        }

        private static bool IntersectsObstacles(BlockReference br, List<Entity> obstacles)
        {
            if (!br.Bounds.HasValue) return false;

            Extents3d brBounds = br.Bounds.Value;

            foreach (var obstacle in obstacles)
            {
                if (!obstacle.Bounds.HasValue) continue;

                Extents3d obsBounds = obstacle.Bounds.Value;

                bool intersects =
                    brBounds.MinPoint.X <= obsBounds.MaxPoint.X &&
                    brBounds.MaxPoint.X >= obsBounds.MinPoint.X &&
                    brBounds.MinPoint.Y <= obsBounds.MaxPoint.Y &&
                    brBounds.MaxPoint.Y >= obsBounds.MinPoint.Y;

                if (intersects)
                    return true;
            }

            return false;
        }

        private static List<Entity> GetObstacles(BlockTableRecord ms, Transaction tr)
        {
            List<Entity> obstacles = new List<Entity>();

            foreach (ObjectId id in ms)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                if (ent is BlockReference || ent is Polyline || ent is Solid || ent is Hatch)
                {
                    if (!ent.Bounds.HasValue) continue;
                    obstacles.Add(ent);
                }
            }

            return obstacles;
        }

        private struct Segment
        {
            public Point3d StartPoint;
            public Point3d EndPoint;
        }

        private static List<Segment> GetSegments(Polyline poly)
        {
            List<Segment> segments = new List<Segment>();

            int count = poly.NumberOfVertices;
            for (int i = 0; i < count; i++)
            {
                Point3d start = poly.GetPoint3dAt(i);
                Point3d end = poly.GetPoint3dAt((i + 1) % count);
                segments.Add(new Segment { StartPoint = start, EndPoint = end });
            }

            return segments;
        }
    }
}
