using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AutoCADEquipmentPlugin.Geometry;
using System;
using System.Linq;

namespace AutoCADEquipmentPlugin.Logic
{
    public static class Placer
    {
        public static void Place(string blockName, double offset, bool clearOld)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nВыберите границу торгового зала (замкнутая полилиния):");
                peo.SetRejectMessage("\nТолько полилинии.");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                ObjectId polyId = per.ObjectId;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline poly = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                    if (!poly.Closed)
                    {
                        ed.WriteMessage("\nПолилиния не замкнута.");
                        return;
                    }

                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (!bt.Has(blockName))
                    {
                        ed.WriteMessage($"\nБлок \"{blockName}\" не найден.");
                        return;
                    }

                    BlockTableRecord brDef = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                    if (brDef.Name == blockName)

                    if (clearOld)
                    {
                        foreach (ObjectId entId in ms)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                            if (ent is BlockReference br && br.Name == blockName)
                            {
                                br.Erase();
                            }
                        }
                    }

                    double totalLength = 0;
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        Point3d pt1 = poly.GetPoint3dAt(i);
                        Point3d pt2 = poly.GetPoint3dAt((i + 1) % poly.NumberOfVertices);
                        Vector3d edge = pt2 - pt1;
                        totalLength += edge.Length;
                    }

                    BlockTableRecord brDef = tr.GetObject(bt[blockName], OpenMode.ForRead) as BlockTableRecord;
                    Extents3d ext = brDef.GeometricExtents;
                    double blockLength = (ext.MaxPoint - ext.MinPoint).X;

                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        Point3d pt1 = poly.GetPoint3dAt(i);
                        Point3d pt2 = poly.GetPoint3dAt((i + 1) % poly.NumberOfVertices);
                        Vector3d dir = pt2 - pt1;
                        Vector3d perp = dir.GetPerpendicularVector().GetNormal() * offset;
                        Vector3d unit = dir.GetNormal();

                        double segmentLength = dir.Length;
                        int count = (int)(segmentLength / (blockLength + offset));

                        for (int j = 0; j < count; j++)
                        {
                            Point3d pos = pt1 + unit * (j * (blockLength + offset)) + perp;
                            double angle = Math.Atan2(unit.Y, unit.X);

                            BlockReference br = new BlockReference(pos, brDef.ObjectId) { Rotation = angle };

                            if (!Utils.IsPointInside(poly, pos)) continue;
                            if (Utils.IntersectsOtherObjects(ms, br, tr)) continue;

                            ms.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                        }

                        // угловой блок
                        Point3d corner = pt2 + perp;
                        if (Utils.IsPointInside(poly, corner))
                        {
                            BlockReference cornerBr = new BlockReference(corner, brDef.ObjectId)
                            {
                                Rotation = Math.Atan2(unit.Y, unit.X)
                            };

                            if (!Utils.IntersectsOtherObjects(ms, cornerBr, tr))
                            {
                                ms.AppendEntity(cornerBr);
                                tr.AddNewlyCreatedDBObject(cornerBr, true);
                            }
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage("\nОборудование размещено.");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nОшибка: " + ex.Message);
            }
        }
    }
}
