using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(AutoCADEquipmentPlugin.Plugin))]

namespace AutoCADEquipmentPlugin
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAutoCADEquipmentPlugin загружен. Используйте команду EQP.");
        }

        public void Terminate() { }

        [CommandMethod("eqp")]
        public void RunWithUI()
        {
            Application.ShowModalDialog(new UI.PlaceForm());
        }
    }
}
