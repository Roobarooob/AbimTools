#region Namespaces
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media.Imaging;


#endregion

namespace AbimTools
{
    public class App : IExternalApplication
    {
        public static PushButton pref_button { get; set; }
        public static PushButton selector_button { get; set; }
        public static PushButton bath_button { get; set; }
        public static PushButton col_button { get; set; }
        public static PushButton workset_button { get; set; }
        public static PushButton lookUp_button { get; set; }
        public RibbonPanel RibbonPanel(UIControlledApplication a, string tab, string ribbonPanelText)
        {
            // Empty ribbon panel 
            RibbonPanel ribbonPanel = null;
            // Try to create ribbon tab. 
            try
            {
                a.CreateRibbonTab(tab);
            }
            catch { }
            // Try to create ribbon panel.
            try
            {
                RibbonPanel panel = a.CreateRibbonPanel(tab, ribbonPanelText);
            }
            catch { }
            // Search existing tab for your panel.
            List<RibbonPanel> panels = a.GetRibbonPanels(tab);
            foreach (RibbonPanel p in panels)
            {
                if (p.Name == ribbonPanelText)
                {
                    ribbonPanel = p;
                }
            }
            //return panel 
            return ribbonPanel;
        }
        private void OnButtonCreate(UIControlledApplication application)
        {

            string assemblyPath = typeof(App).Assembly.Location;
            var pan1 = RibbonPanel(application, "����-���", "�����");

            // Create push buttons
            PushButton CreateBtn(RibbonPanel panel, string name, string text, string command, string image_uri)
            {
                PushButtonData buttondata = new PushButtonData(name, text, assemblyPath, command);
                BitmapImage pb1Image = new BitmapImage(new Uri(image_uri));
                buttondata.LargeImage = pb1Image;
                PushButton button = panel.AddItem(buttondata) as PushButton;
                return button;
            }


            /// PushButtonData buttondata1 = new PushButtonData("Opening_Tools", "����������� ���������\n� ������", assemblyPath, "Opening_Tools.Command");
            /// BitmapImage pb1Image1 = new BitmapImage(new Uri("pack://application:,,,/Opening_Tools;component/Resources/PlaceIcon.png"));
            /// buttondata1.LargeImage = pb1Image1;
            
            //About
            pref_button = CreateBtn(pan1, "About", "� ���������", "AbimTools.Version", "pack://application:,,,/AbimToolsMine;component/Resources/About.png");
            pref_button.ToolTip = "����� ���������� � ������";
            pref_button.AvailabilityClassName = "AbimTools.CommandAvailability";
            //Pref_button.LongDescription = "";

            //����� � ��������
            bath_button = CreateBtn(pan1, "BatchTools", "��������\n���������", "AbimTools.BatchTools", "pack://application:,,,/AbimToolsMine;component/Resources/BatchTools32.png");
            bath_button.ToolTip = "����������� ������������������ ���������������� ��������� ���������� ������ Revit";
            bath_button.LongDescription = "�������������� �������� ��������, ����������� �������� �� XML, �������� ������ �� �������";
            BitmapImage bath_Image = new BitmapImage(new Uri("pack://application:,,,/AbimToolsMine;component/Resources/BatchTools16.png"));
            bath_button.Image = bath_Image;
            bath_button.AvailabilityClassName = "AbimTools.CommandAvailability";
            
            //��������
            col_button = CreateBtn(pan1, "CollisionTools", "���������\n��������", "AbimTools.Collisions", "pack://application:,,,/AbimToolsMine;component/Resources/Collisions32.png");
            col_button.ToolTip = "�������� �������� �� XML � ������� ��������\n" +
                "���� � XML ������ ���� �������� � ��������� ���_���� XML ��������";
            BitmapImage col_Image = new BitmapImage(new Uri("pack://application:,,,/AbimToolsMine;component/Resources/Collisions16.png"));
            col_button.Image = col_Image;

            //������
            selector_button = CreateBtn(pan1, "FastFilter", "������������", "AbimTools.FastFilter", "pack://application:,,,/AbimToolsMine;component/Resources/FastFilter32.png");
            selector_button.ToolTip = "���������� ��������� ���������, ����� �������";
            BitmapImage sb_Image = new BitmapImage(new Uri("pack://application:,,,/AbimToolsMine;component/Resources/FastFilter16.png"));
            selector_button.Image = sb_Image;

            //������� ������
            workset_button = CreateBtn(pan1, "SetWorksetForLinks", "�����������\n" +
                "������� �������", "AbimTools.LinksWokset", "pack://application:,,,/AbimToolsMine;component/Resources/WSName32.png");
            workset_button.ToolTip = "�������� ������� ������� ��� ������, ���������� ������ ������� �������";
            BitmapImage ws_Image = new BitmapImage(new Uri("pack://application:,,,/AbimToolsMine;component/Resources/WSName16.png"));
            workset_button.Image = ws_Image;

            //������� ������
            lookUp_button = CreateBtn(pan1, "GetLookupTable", "������� ������\n������", "AbimTools.GetLookUpTable", "pack://application:,,,/AbimToolsMine;component/Resources/LPTableExport32.png");
            lookUp_button.ToolTip = "������� ������ ������ �� �������";
            BitmapImage lookup_Image = new BitmapImage(new Uri("pack://application:,,,/AbimToolsMine;component/Resources/LPTableExport16.png"));
            lookUp_button.Image = ws_Image;
        }
        public Result OnStartup(UIControlledApplication application)
        {
           OnButtonCreate(application);
            application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            return Result.Succeeded;
        }

        public Result CloseApp(UIControlledApplication application)
        {
            var pan1 = RibbonPanel(application, "����-���", "�����");
            pan1.Visible = false;
            application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            return Result.Succeeded;
        }

        private void OnDocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            Document doc = e.Document;

            // ���������, ��� �������� �� �������� ��������
            if (!doc.IsFamilyDocument)
            {
                PinLink(doc);
            }
        }
        private void PinLink(Document doc)
        {
            // �������� ��� �����
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(RevitLinkInstance));
            collector.UnionWith(new FilteredElementCollector(doc).OfClass(typeof(Grid)));
            collector.UnionWith(new FilteredElementCollector(doc).OfClass(typeof(Level)));
            var linkInstances = collector.Cast<Element>();

            using (Transaction trans = new Transaction(doc, "Pin"))
            {
                trans.Start();

                foreach (var linkInstance in linkInstances)
                {
                    // ���������� ������ �����
                    linkInstance.Pinned = true;
                }
                trans.Commit();
            }
        }
    }
    public class CommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // ������� ������ ��������
            return true;
        }
    }

}