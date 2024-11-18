﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbimTools
{
    [Transaction(TransactionMode.Manual)]
    public class Version : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            TaskDialog.Show("Информация", "Версия приложения - 3.3.1\n\n" +

                "Загруженные плагины:\n" +
                "АвтоПиннер\n" +
                "БыстроФильтр\n" +
                "Пакетная обработка\n"+
                "Загрузка коллизий\n" +
                "Инструменты рабочих наборов\n\n" +
            "created by Anton Rubinshtein\n");
            return Result.Succeeded;
        }
    }

}