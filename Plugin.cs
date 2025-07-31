using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using AutoCADEquipmentPlugin.UI;

[assembly: CommandClass(typeof(AutoCADEquipmentPlugin.Plugin))]

namespace AutoCADEquipmentPlugin
{
    public class Plugin
    {
        [CommandMethod("PlaceWithUI")]
        public void PlaceWithUI()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            // Показываем форму настроек
            var form = new PlaceForm();
            Application.ShowModalDialog(form);
        }
    }
}
