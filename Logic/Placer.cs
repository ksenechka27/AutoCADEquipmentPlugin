using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;

namespace AutoCADEquipmentPlugin.Logic
{
    public class Placer
    {
        public void PlaceEquipmentAlongWalls(Polyline boundary, List<(string blockName, double offset, int count)> blocksToPlace)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                List<Extents3d> obstacles = GetObstacles(ms, tr, boundary.ObjectId);

                // Пример: обходим все сегменты границы
                for (int i = 0; i < boundary.NumberOfVertices - 1; i++)
                {
                    Point3d pt1 = boundary.GetPoint3dAt(i);
                    Point3d pt2 = boundary.GetPoint3dAt(i + 1);
                    Vector3d dir = pt2 - pt1;

                    double length = dir.Length;
                    dir = dir.GetNormal();

                    double currentPos = 0;

                    foreach (var (blockName, offset, count) in blocksToPlace)
                    {
                        int placed = 0;

                        while (currentPos + offset < length && (count == 0 || placed < count))
                        {
                            Point3d pos = pt1 + dir.MultiplyBy(currentPos);

                            if (TryPlaceBlock(pos, dir, blockName, 1.0, ms, tr, obstacles, out BlockReference br))
                            {
                                placed++;
                                currentPos += offset;
                            }
                            else
                            {
                                currentPos += offset / 2.0; // шаг меньше, если не удалось разместить
                            }
                        }
                    }
                }

                tr.Commit();
            }
        }

        private List<Extents3d> GetObstacles(BlockTableRecord ms, Transaction tr, ObjectId boundaryId)
        {
            List<Extents3d> obstacles = new List<Extents3d>();

            foreach (ObjectId id in ms)
            {
                if (id == boundaryId) continue;

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || !ent.Visible || !ent.Bounds.HasValue) continue;
                if (ent is BlockReference br && br.Name.StartsWith("Оборудование")) continue;

                obstacles.Add(ent.Bounds.Value);
            }

            return obstacles;
        }

        private bool TryPlaceBlock(Point3d position, Vector3d direction, string blockName, double scale, BlockTableRecord ms, Transaction tr, List<Extents3d> obstacles, out BlockReference brPlaced)
        {
            brPlaced = null;

            ObjectId blockId = GetBlockIdByName(blockName, tr);
            if (blockId == ObjectId.Null) return false;

            BlockReference br = new BlockReference(position, blockId);
            br.ScaleFactors = new Scale3d(scale);
            br.Rotation = direction.AngleOnPlane(Vector3d.ZAxis);

            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);

            if (Geometry.GeometryUtils.IntersectsOther(ms, br, tr) || IntersectsObstacles(br, obstacles))
            {
                br.Erase();
                return false;
            }

            brPlaced = br;
            return true;
        }

        private bool IntersectsObstacles(BlockReference br, List<Extents3d> obstacles)
        {
            if (!br.Bounds.HasValue) return false;

            Extents3d brBounds = br.Bounds.Value;

            foreach (var obs in obstacles)
            {
                bool intersects =
                    brBounds.MinPoint.X <= obs.MaxPoint.X &&
                    brBounds.MaxPoint.X >= obs.MinPoint.X &&
                    brBounds.MinPoint.Y <= obs.MaxPoint.Y &&
                    brBounds.MaxPoint.Y >= obs.MinPoint.Y;

                if (intersects)
                    return true;
            }

            return false;
        }

        private ObjectId GetBlockIdByName(string blockName, Transaction tr)
        {
            BlockTable bt = (BlockTable)tr.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId, OpenMode.ForRead);
            return bt.Has(blockName) ? bt[blockName] : ObjectId.Null;
        }
    }
}
