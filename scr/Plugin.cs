pusing System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

[assembly: CommandClass(typeof(AutoCADEquipmentPlugin.Plugin))]

namespace AutoCADEquipmentPlugin
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAutoCADEquipmentPlugin загружен. Используйте команду PlaceWithUI.");
        }

        public void Terminate() { }

        [CommandMethod("eqp")]
        public void PlaceWithUI()
        {
            Application.ShowModalDialog(new PlaceForm());
        }
    }

    public class PlaceForm : Form
    {
        private TextBox blockNameTextBox;
        private NumericUpDown offsetUpDown;
        private Button placeButton;

        public PlaceForm()
        {
            this.Text = "Параметры расстановки";
            this.Width = 300;
            this.Height = 150;

            Label blockLabel = new Label() { Text = "Имя блока:", Top = 10, Left = 10, Width = 100 };
            blockNameTextBox = new TextBox() { Top = 10, Left = 120, Width = 150 };

            Label offsetLabel = new Label() { Text = "Отступ (мм):", Top = 40, Left = 10, Width = 100 };
            offsetUpDown = new NumericUpDown() { Top = 40, Left = 120, Width = 100, Minimum = 0, Maximum = 10000, Value = 500 };

            placeButton = new Button() { Text = "Разместить", Top = 80, Left = 120, Width = 100 };
            placeButton.Click += (s, e) =>
            {
                string blockName = blockNameTextBox.Text;
                double offset = (double)offsetUpDown.Value / 1000.0;
                this.Close();
                PluginHelper.PlaceEquipment(blockName, offset);
            };

            this.Controls.Add(blockLabel);
            this.Controls.Add(blockNameTextBox);
            this.Controls.Add(offsetLabel);
            this.Controls.Add(offsetUpDown);
            this.Controls.Add(placeButton);
        }
    }

    public static class PluginHelper
    {
        public static void PlaceEquipment(string blockName, double offset)
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
                        ed.WriteMessage($"\nБлок \"{blockName}\" не найден в чертеже.");
                        return;
                    }

                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    BlockTableRecord blockDef = tr.GetObject(bt[blockName], OpenMode.ForRead) as BlockTableRecord;
                    Extents3d ext = blockDef.GeometricExtents;
                    double blockLength = (ext.MaxPoint - ext.MinPoint).X;

                    int numSegments = poly.NumberOfVertices;
                    for (int i = 0; i < numSegments; i++)
                    {
                        Point3d pt1 = poly.GetPoint3dAt(i);
                        Point3d pt2 = poly.GetPoint3dAt((i + 1) % numSegments);

                        Vector3d edge = pt2 - pt1;
                        Vector3d perp = edge.GetPerpendicularVector().GetNormal() * offset;
                        Vector3d dir = edge.GetNormal();

                        double length = edge.Length;
                        int count = (int)(length / (blockLength + offset));

                        for (int j = 0; j < count; j++)
                        {
                            Point3d pos = pt1 + (dir * j * (blockLength + offset)) + perp;
                            double angle = Math.Atan2(dir.Y, dir.X);

                            // Проверка — внутри ли точка
                            if (!poly.IsPointInside(pos, Tolerance.Global, true))
                                continue;

                            BlockReference br = new BlockReference(pos, bt[blockName])
                            {
                                Rotation = angle
                            };

                            // Проверка пересечения с другими объектами
                            br.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, pos));
                            br.ScaleFactors = new Scale3d(1); // Убедимся, что масштаб 1:1

                            modelSpace.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);

                            // Проверка пересечения после добавления
                            bool intersects = false;
                            foreach (ObjectId entId in modelSpace)
                            {
                                if (entId == br.ObjectId) continue;
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent != null && br.GeometricExtents.IntersectWith(ent.GeometricExtents).HasValue)
                                {
                                    intersects = true;
                                    break;
                                }
                            }

                            if (intersects)
                            {
                                br.Erase(); // Удалить если пересекается
                            }
                        }

                        // "Умная" вставка в углу
                        Point3d corner = pt2 + perp;
                        if (poly.IsPointInside(corner, Tolerance.Global, true))
                        {
                            BlockReference cornerBr = new BlockReference(corner, bt[blockName])
                            {
                                Rotation = Math.Atan2(dir.Y, dir.X),
                                ScaleFactors = new Scale3d(1)
                            };

                            modelSpace.AppendEntity(cornerBr);
                            tr.AddNewlyCreatedDBObject(cornerBr, true);
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\nОборудование успешно размещено.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nОшибка: " + ex.Message);
            }
        }
    }

    public static class PolylineExtensions
    {
        // Быстрая проверка: точка внутри полилинии
        public static bool IsPointInside(this Polyline poly, Point3d point, Tolerance tolerance, bool useEvenOdd)
        {
            return poly.Contains(point);
        }
    }
}
