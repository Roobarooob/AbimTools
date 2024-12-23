﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace AbimToolsMine
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class BatchTools : IExternalCommand
    {
        public static ExternalCommandData CommandData { get; set; }

        public static CollisionsWin window = null;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CommandData = commandData;
            if (window == null)
            {
                window = new CollisionsWin();
                window.ShowDialog();
            }
            else
            {
                window.Activate();
            }
            return Result.Succeeded;
        }
    }
    
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Collisions : IExternalCommand
    {
        string xmlFilePath { get; set; }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            String discipline = BatchFunctions.GetDisciplineFromFileName(doc.Title);
            string username = app.Username;

            FamilySymbol familySymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)  // Категория "Обобщенные модели"
                .WhereElementIsElementType()                   // Выбираем только типоразмеры (ElementType)
                .FirstOrDefault(e => (e as ElementType).FamilyName.Equals("ПРО_Коллизия") &&
                                        e.Name == "Основная") as FamilySymbol;

            BatchFunctions.workset = BatchFunctions.GetOrCreateWorkset(doc, "*Коллизии");

            try
            {
                xmlFilePath = doc.ProjectInformation.LookupParameter("ПРО_Путь к XML коллизий").AsString();
            }
            catch { TaskDialog.Show("Ошибка!", "нет параметра «ПРО_Путь к XML коллизий»"); }
            try
            {
                if (familySymbol != null)
                {
                    int count = BatchFunctions.AnalyzeAndPlace(doc, username, familySymbol, discipline, xmlFilePath);
                    // Выполните синхронизацию с Revit Server             
                }
                else 
                {
                    TaskDialog.Show("Ошибка", "Не загружено семейство ПРО_Коллизия");
                }
            }
            catch
            {
                TaskDialog.Show("Ошибка", "Что-то не получлось возможно не заполнен параметр «ПРО_Путь к XML коллизий»");
            }
            return Result.Succeeded;
        }
    }
    public class BatchFunctions
    {
        public static Workset workset { get; set; }
        //Удалить связи из файла
        public static void BatchLinkRemove(ExternalCommandData commandData, List<string> filePaths)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            var true_list = new StringBuilder();
            var false_list = new StringBuilder();

            Opt opt = new Opt();
            Application app = uiapp.Application;
            foreach (string filePath in filePaths)
            {
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                if (File.Exists(filePath) || modelPath.ServerPath)
                {                 
                    // Настройте параметры открытия документа с закрытыми рабочими наборами
                    OpenOptions openOptions = new OpenOptions();
                    openOptions.DetachFromCentralOption = DetachFromCentralOption.ClearTransmittedSaveAsNewCentral;
                    WorksetConfiguration worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                    openOptions.SetOpenWorksetsConfiguration(worksetConfig);
                    openOptions.AllowOpeningLocalByWrongUser = true;

                    // Откройте документ
                    Document doc = app.OpenDocumentFile(modelPath, openOptions);
                    using (Transaction tx = new Transaction(doc))
                    {
                        try
                        {

                            tx.Start("Удаление связей из файла");
                            LinkRemove(doc);
                            tx.Commit();
                            // Сохраните и закройте документ
                            true_list.AppendLine(doc.Title);
                            doc.Save();
                            try
                            {
                                doc.Close(false);
                            }
                            catch
                            {
                                continue;
                            }

                        }
                        catch
                        {
                            //закройте документ
                            false_list.AppendLine(doc.Title);
                            doc.Close(false);

                        }
                    }
                }
            }
            System.Windows.MessageBox.Show($"Связи удалены \nУспешно обработаны:\n{true_list}\nНе обработаны:\n{false_list}");
        }

        public static void DownloadFamily(ExternalCommandData commandData, List<string> filePaths, string FamilyPath)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            var true_list = new StringBuilder();
            var false_list = new StringBuilder();

            Opt opt = new Opt();
            Application app = uiapp.Application;
            foreach (string filePath in filePaths)
            {
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                if (File.Exists(filePath)||modelPath.ServerPath)
                {
                    // Настройте параметры открытия документа с закрытыми рабочими наборами
                    OpenOptions openOptions = new OpenOptions();
                    WorksetConfiguration worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                    openOptions.SetOpenWorksetsConfiguration(worksetConfig);
                    openOptions.AllowOpeningLocalByWrongUser = true;

                    // Откройте документ
                    Document doc = app.OpenDocumentFile(modelPath, openOptions);
                    using (Transaction tx = new Transaction(doc))
                    {
                        try
                        {

                            tx.Start("Загрузить семейство");
                            doc.LoadFamily(FamilyPath, opt, out Family family);
                            tx.Commit();
                            // Выполните синхронизацию с Revit Server
                            SyncWithRevitServer(doc);
                            // Сохраните и закройте документ
                            true_list.AppendLine(doc.Title);
                            doc.Save();
                            try
                            {
                                doc.Close(false);
                            }
                            catch
                            {
                                continue;
                            }

                        }
                        catch
                        {
                            //закройте документ
                            false_list.AppendLine(doc.Title);
                            doc.Close(false);

                        }
                    }
                }
            }
            System.Windows.MessageBox.Show($"Загрузка семейства !\nУспешно обработаны:\n{true_list}\nНе загружено:\n{false_list}");
        }
        //Загрузка коллизий
        public static void PlaceCollisions(ExternalCommandData commandData, List<string> filePaths, string xmlFilePath)
        {
            // Определите список файлов для обработки
            if (xmlFilePath != "")
            {
                UIApplication uiapp = commandData.Application;
                Application app = uiapp.Application;
                string username = app.Username;

                //списки в сообщения о завершении
                var true_list = new StringBuilder();
                var false_list = new StringBuilder();
                // Перебирайте каждый файл
                foreach (string filePath in filePaths)
                {
                    ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                    if (File.Exists(filePath) || modelPath.ServerPath)
                    {                      
                        // Настройте параметры открытия документа с закрытыми рабочими наборами
                        OpenOptions openOptions = new OpenOptions();
                        WorksetConfiguration worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                        openOptions.SetOpenWorksetsConfiguration(worksetConfig);
                        openOptions.AllowOpeningLocalByWrongUser = true;

                        // Откройте документ
                        Document doc = app.OpenDocumentFile(modelPath, openOptions);
                        String discipline = GetDisciplineFromFileName(doc.Title);

                        FamilySymbol familySymbol = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_GenericModel)  // Категория "Обобщенные модели"
                            .WhereElementIsElementType()                   // Выбираем только типоразмеры (ElementType)
                            .FirstOrDefault(e => (e as ElementType).FamilyName.Equals("ПРО_Коллизия") &&
                                                    e.Name == "Основная") as FamilySymbol;

                        workset = GetOrCreateWorkset(doc, "*Коллизии");

                        try
                        {
                            if (familySymbol != null)
                            {
                                int count = AnalyzeAndPlace(doc, username, familySymbol, discipline, xmlFilePath);
                                // Выполните синхронизацию с Revit Server
                                SyncWithRevitServer(doc);
                                // Сохраните и закройте документ
                                if (count >= 0)
                                {
                                    true_list.AppendLine(doc.Title + $" - {count}");
                                }
                                else
                                {
                                    false_list.AppendLine(doc.Title);
                                }
                                doc.Save();
                                try
                                {
                                    doc.Close(false);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                false_list.AppendLine(doc.Title + " - Нет семейства ПРО_Коллизия");
                            }
                        }
                        catch
                        {
                            //закройте документ
                            false_list.AppendLine(doc.Title);
                            doc.Close(false);

                        }
                    }
                }

                System.Windows.MessageBox.Show($"Процесс завершен!\nУспешно обработаны:\n{true_list}\nНе обработаны:\n{false_list}");
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Добавьте XML файл", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private static void LinkRemove(Document doc)
        {

            // Берем все линки в проекте
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkType));

            // Собираем все RevitLinkType элементы
            List<ElementId> linkIds = collector
                .Cast<RevitLinkType>()
                .Select(linkType => linkType.Id)
                .ToList();

            // Проверяем, есть ли ссылки
            if (linkIds.Count > 0)
            {
                doc.Delete(linkIds);

            }
        }
        public static void SyncWithRevitServer(Document doc)
        {
            // Логика синхронизации
            if (doc.IsWorkshared)
            {
                TransactWithCentralOptions transactOptions = new TransactWithCentralOptions();
                SynchronizeWithCentralOptions syncOptions = new SynchronizeWithCentralOptions();
                RelinquishOptions relinquishOptions = new RelinquishOptions(true)
                {
                    UserWorksets = true,
                    FamilyWorksets = true,
                    ViewWorksets = true,
                    CheckedOutElements = true,
                    StandardWorksets = true,
                };
                syncOptions.SetRelinquishOptions(relinquishOptions);
                doc.SynchronizeWithCentral(transactOptions, syncOptions);
            }
        }

        // Метод для получения или создания рабочего набора "*Коллизии"
        public static Workset GetOrCreateWorkset(Document doc, string worksetName)
        {
            // Поиск существующего рабочего набора
            Workset existingWorkset = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(ws => ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

            // Если рабочий набор существует, возвращаем его
            if (existingWorkset != null)
            {
                return existingWorkset;
            }

            // Создание нового рабочего набора
            using (Transaction trans = new Transaction(doc, "Создать рабочий набор"))
            {
                WorksetId worksetId = null;
                trans.Start();
                try
                {
                    worksetId = Workset.Create(doc, worksetName).Id;
                    trans.Commit();

                    // Возвращаем новый рабочий набор
                    return doc.GetWorksetTable().GetWorkset(worksetId);
                }
                catch
                {
                    //в файле нет рабочих наборов
                    trans.Commit();
                    return null;

                }
            }
        }
        public static void ChangeElementWorkset(Element element, Workset workset)
        {

            WorksetId worksetId = workset.Id;
            Parameter worksetParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            worksetParam.Set(worksetId.IntegerValue);

        }
        //Анализировать xml и расставить коллизии
        public static int AnalyzeAndPlace(Document doc, string username, FamilySymbol familySymbol, string discipline, string xmlFilePath)
        {
            var xmlFileDate = File.GetLastWriteTime(xmlFilePath);

            XDocument xmlDoc = XDocument.Load(xmlFilePath);
            int count = 0;
            var clashResults = (from clashtest in xmlDoc.Descendants("clashtest")
                                let clashtestName = clashtest.Attribute("name").Value
                                from clashresult in clashtest.Descendants("clashresult")
                                let clashResultName = clashresult.Attribute("name").Value
                                let status = clashresult.Attribute("status").Value
                                let pos3f = clashresult.Descendants("pos3f").FirstOrDefault()
                                let posX = pos3f.Attribute("x").Value
                                let posY = pos3f.Attribute("y").Value
                                let posZ = pos3f.Attribute("z").Value
                                let clashObject = clashresult.Descendants("clashobject").FirstOrDefault()
                                let fileName = clashObject?.Descendants("pathlink").FirstOrDefault()?
                                                    .Elements("node").Skip(2).FirstOrDefault()?.Value
                                where !string.IsNullOrEmpty(clashtestName) && !string.IsNullOrEmpty(clashResultName)
                                        && pos3f != null && clashObject != null // Пропускаем элементы с отсутствующими данными
                                select new ClashResult
                                {
                                    ClashTestName = clashtestName,
                                    ClashResultName = clashResultName,

                                    PosX = posX,
                                    PosY = posY,
                                    PosZ = posZ,
                                    Status = status,
                                    FileName = fileName.Replace(".nwc", "")
                                }).ToList();
            //Создать элементы
            Autodesk.Revit.DB.Transform transform = doc.ActiveProjectLocation.GetTotalTransform();
            if (DelBefore())
            {
                using (Transaction trans = new Transaction(doc, "Размещение шариков"))
                {

                    trans.Start();
                    count = PlaceClashResults();
                    trans.Commit();
                }
                DelAfter();
                return count;
            }
            else
            {
                return -1;
            }
            //Расстановка элементов
            int PlaceClashResults()
            {

                var filteredClashResults = clashResults
                            .Where(cr => (cr.Status == "active" || cr.Status == "new") && cr.FileName == doc.Title.Replace("_" + username, ""))
                            .ToList();
                filteredClashResults = RemoveDuplicateClashResults(filteredClashResults);
                foreach (var clashResult in filteredClashResults)
                {
                    if (!double.TryParse(clashResult.PosX, NumberStyles.Float, CultureInfo.InvariantCulture, out double posX) ||
                                    !double.TryParse(clashResult.PosY, NumberStyles.Float, CultureInfo.InvariantCulture, out double posY) ||
                                    !double.TryParse(clashResult.PosZ, NumberStyles.Float, CultureInfo.InvariantCulture, out double posZ))
                    {
                        // Если парсинг не удался, пропускаем этот элемент
                        continue;
                    }


                    XYZ point = new XYZ(posX, posY, posZ) * 1000 / 304.8;
                    XYZ trans_point = transform.OfPoint(point);
                    Level nearestLevel = GetNearestLowerLevel(trans_point.Z);
                    XYZ fin_point = new XYZ(trans_point.X, trans_point.Y, (trans_point.Z - nearestLevel.Elevation));

                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                        doc.Regenerate();
                    }

                    FamilyInstance instance = doc.Create.
                        NewFamilyInstance(fin_point, familySymbol, nearestLevel, nearestLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    instance.LookupParameter("ПРО_Марка").Set(clashResult.ClashResultName);
                    instance.LookupParameter("ПРО_Обозначение").Set(clashResult.ClashTestName);
                    instance.LookupParameter("ПРО_Дата").Set(xmlFileDate.ToString("dd/MM/yyyy"));
                    if (workset != null)
                    {
                        ChangeElementWorkset(instance, workset);
                    }
                }
                return filteredClashResults.Count();

            }
            /*bool MatchesDiscipline(string clashTestName)
            {
                if (string.IsNullOrEmpty(discipline)) return false;
                return clashTestName.StartsWith(discipline);
            }*/


            Level GetNearestLowerLevel(double z)
            {
                // Получить все уровни в документе
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                var levels = collector.OfClass(typeof(Level)).Cast<Level>().ToList();

                // Найти ближайший нижний уровень
                Level nearestLowerLevel = levels
                    .Where(level => level.Elevation <= z)
                    .OrderByDescending(level => level.Elevation)
                    .FirstOrDefault();

                if (nearestLowerLevel != null)
                {
                    return nearestLowerLevel;
                }

                // Если ближайший нижний уровень не найден, найти ближайший верхний уровень
                Level nearestUpperLevel = levels
                    .Where(level => level.Elevation > z)
                    .OrderBy(level => level.Elevation)
                    .FirstOrDefault();

                return nearestUpperLevel;
            }

            bool DelBefore()
            {

                ElementId newId = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)  // Категория "Обобщенные модели"
                .WhereElementIsElementType()                   // Выбираем только типоразмеры (ElementType)
                .FirstOrDefault(e => (e as ElementType).FamilyName.Equals("ПРО_Коллизия") &&
                                        e.Name == "Смежная").Id;

                var instances = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType()
                    .Where(e =>
                    {
                        var familyInstance = e as FamilyInstance;
                        return familyInstance != null && familyInstance.Symbol.FamilyName.Equals("ПРО_Коллизия");
                    })
                    .ToList();

                var blacklist = new List<ElementId>(); //Черный список лишних типов
                if (CheckIfAnyInstanceIsLocked(instances))
                {
                    using (Transaction trans = new Transaction(doc, "Удаление предыдущих"))
                    {
                        trans.Start();
                        foreach (FamilyInstance instance in instances)
                        {
                            string prim = instance.LookupParameter("ПРО_Примечание").AsString();
                            if (instance.Name != "Смежная")
                            {
                                if (instance.Name != "Основная")
                                {
                                    blacklist.Add(instance.GetTypeId());
                                    if (prim != "" && prim != null)
                                    {
                                        instance.ChangeTypeId(newId);
                                    }
                                    else
                                    {
                                        doc.Delete(instance.Id);
                                    }
                                }
                                else
                                {
                                    if (prim != "" && prim != null)
                                    {
                                        instance.ChangeTypeId(newId);
                                    }
                                    else
                                    {
                                        doc.Delete(instance.Id);
                                    }
                                }
                            }
                        }

                        //удаление лишних типов
                        if (blacklist.Count > 0)
                        {
                            foreach (ElementId elementId in blacklist.Distinct().ToList())
                            {
                                doc.Delete(elementId);
                            }

                        }
                        trans.Commit();
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            bool CheckIfAnyInstanceIsLocked(List<Element> elements)
            {
                string currentUserName = doc.Application.Username;

                // Получаем все элементы по их идентификаторам


                foreach (var element in elements)
                {
                    string editedByParam = element.get_Parameter(BuiltInParameter.EDITED_BY).AsString();
                    if (editedByParam == null || editedByParam == "" || editedByParam == currentUserName)
                    {
                        continue; // Элемент свободен или редактируется текущим пользователем, продолжаем проверку
                    }
                    else
                    {
                        return false; // Найден элемент, редактируемый другим пользователем
                    }
                }

                return true; // Ни один элемент не блокируется другими пользователями
            }
            void DelAfter()
            {
                // Получаем все элементы семейства "ПРО_Коллизия" тип "А"
                var typeAElements = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType()
                    .Where(e =>
                    {
                        var familyInstance = e as FamilyInstance;
                        return familyInstance != null && familyInstance.Symbol.FamilyName.Equals("ПРО_Коллизия");
                    })
                    .Where(e => e.Name == "Смежная")
                    .ToList();

                // Получаем все элементы семейства "ПРО_Коллизия" тип "Б"
                var typeBElements = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType()
                    .Where(e =>
                    {
                        var familyInstance = e as FamilyInstance;
                        return familyInstance != null && familyInstance.Symbol.FamilyName.Equals("ПРО_Коллизия");
                    })
                    .Where(e => e.Name == "Основная")
                    .ToList();

                using (Transaction trans = new Transaction(doc, "Фильтрация существующих"))
                {
                    trans.Start();

                    // Проверяем каждый элемент типа А
                    foreach (var typeA in typeAElements)
                    {
                        XYZ coordA = ((LocationPoint)typeA.Location).Point;

                        // Проверяем, есть ли элемент типа Б с такими же координатами
                        var matchingTypeB = typeBElements.FirstOrDefault(typeB =>
                        {
                            XYZ coordB = ((LocationPoint)typeB.Location).Point;
                            return coordA.IsAlmostEqualTo(coordB);
                        });

                        if (matchingTypeB != null)
                        {
                            // Получаем параметры "ПРО_Текст" для обоих элементов
                            Parameter paramTextA = typeA.LookupParameter("ПРО_Марка");
                            Parameter paramTextB = matchingTypeB.LookupParameter("ПРО_Марка");
                            if (paramTextA != null && paramTextB != null)
                            {
                                // Копируем значение из типа Б в тип А
                                string textB = paramTextB.AsString();
                                paramTextA.Set(textB);
                            }
                            // Если элемент типа Б найден, удаляем элемент типа Б
                            doc.Delete(matchingTypeB.Id);
                            typeBElements.Remove(matchingTypeB); // Убираем удаленный элемент из списка
                        }
                        else
                        {
                            // Если элемента типа Б нет, удаляем элемент типа А
                            doc.Delete(typeA.Id);
                        }
                    }
                    trans.Commit();
                }
            }

            List<ClashResult> RemoveDuplicateClashResults(List<ClashResult> list)
            {
                var uniquePoints = new HashSet<string>();
                var uniqueClashResults = new List<ClashResult>();

                foreach (var clashResult in list)
                {
                    string pointKey = $"{clashResult.PosX},{clashResult.PosY},{clashResult.PosZ}";

                    if (!uniquePoints.Contains(pointKey))
                    {
                        uniquePoints.Add(pointKey);
                        uniqueClashResults.Add(clashResult);
                    }
                }

                return uniqueClashResults;
            }
        }

        public static string GetDisciplineFromFileName(string fileName)
        {
            if (fileName.Contains("АР")) return "0";
            if (fileName.Contains("КР")) return "1";
            if (fileName.Contains("ОВ1")) return "2";
            if (fileName.Contains("ОВ2")) return "3";
            if (fileName.Contains("ВК")) return "4";
            if (fileName.Contains("ЭОМ")) return "5";
            if (fileName.Contains("СС")) return "6";
            if (fileName.Contains("ТМ")) return "7";
            if (fileName.Contains("ТХ")) return "8";
            return null;
        }
    }
        //загрузить семейства в модель
        
    public static class XYZExtensions
    {
        public static bool IsAlmostEqualTo(this XYZ point1, XYZ point2, double tolerance)
        {
            return Math.Abs(point1.X - point2.X) <= tolerance &&
                   Math.Abs(point1.Y - point2.Y) <= tolerance &&
                   Math.Abs(point1.Z - point2.Z) <= tolerance;
        }
    }
    public class Opt : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
    public class ClashResult
    {
        public string ClashTestName { get; set; }
        public string ClashResultName { get; set; }
        public string PosX { get; set; }
        public string PosY { get; set; }
        public string PosZ { get; set; }
        public string Status { get; set; }
        public string FileName { get; set; } // Добавляем поле для имени файла первого элемента коллизии

    }

}
    
