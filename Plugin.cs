using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AutoCADEquipmentPlugin.Geometry;

[assembly: CommandClass(typeof(AutoCADEquipmentPlugin.Plugin))]

namespace AutoCADEquipmentPlugin
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nПлагин загружен. Команда: EQP.");
        }
        public void Terminate() { }

        [CommandMethod("EQP")]
        public void RunWithUI()
        {
            Application.ShowModalDialog(new UI.PlaceForm());
        }
    }
}
