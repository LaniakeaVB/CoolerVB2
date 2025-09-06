using System;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;


namespace CoolerVB
{
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com, при изменении настроек перезагрузить игру")]
    [Serializable]
    [RestartRequired]
    public class ModSettings : SingletonOptions<ModSettings> // класс для ручной регулировки мода. Plib
    {
        private const string Exhaust_cooltype = "Мощность излишка охлаждения\", \"Количество отводимого тепла (кДж/с). Отрицательные значения охлаждают. Значения складываются"; // ID мода, должен быть уникальным
        private const string Self_heat_cooltype = "Мощность обычного режима охлаждения\", \"Количество отводимого тепла (кДж/с). Отрицательные значения охлаждают. Значения складываются"; // ID мода, должен быть уникальным

        [Option("Крупные значения могут привести с крашу игры", null, Format = "F0")]
        [JsonProperty]
        public string ReloadGame { get; set; }


        [Option("Мощность излишка охлаждения", Exhaust_cooltype, null, Format = "F0")]
        [Limit(-1550, 1100f)] // Исправил порядок: сначала минимум, потом максимум
        [JsonProperty]
        public float ExhaustKilowattsWhenActive { get; set; } // Значение по умолчанию

        [Option("Мощность обычного режима охлаждения", Self_heat_cooltype, null, Format = "F0")]
        [Limit(-1550, 1100f)] // Исправил порядок: сначала минимум, потом максимум
        [JsonProperty]
        public float SelfHeatKilowattsWhenActive { get; set; } // Значение по умолчанию


        [Option("Минимальная температура помещения", "Минимальная температура, до которой может охладить помещение 273 = 0°C", null, Format = "F0")]
        [Limit(-5f, 1700f)]
        [JsonProperty]
        public float MinCooledTemperature { get; set; }

        [Option("Температура до которой может нагреться лёд/вода/пар внутри установки 273 = 0°C", null, Format = "F0")]
        [Limit(0f, 1273.15f)]
        [JsonProperty]
        public float TargetTemperature { get; set; }

        [Option("Температура до которой здание ещё будет работать, 273 = 0°C", null, Format = "F0")]
        [Limit(0f, 1550f)]
        [JsonProperty]
        public float minimumOperatingTemperature { get; set; }

        public ModSettings()
        {
            ReloadGame = "Нажмите кнопку ПЕРЕЗАГРУЗИТЬ ИГРУ"; // По умолчанию
            ExhaustKilowattsWhenActive = -24f; //Мощность излишка охлаждения
            SelfHeatKilowattsWhenActive = -8f; //Мощность обычного режима охлаждения
            MinCooledTemperature = 273.15f; //Минимальная температура помещения
            TargetTemperature = 273.15f; //Температура до которой может нагреться лёд/вода/пар внутри установки
            minimumOperatingTemperature = 273.15f; //Температура до которой здание ещё будет работать
        }
    }

    public class CoolerVB : KMod.UserMod2 //такой класс нужен всегда в Harmony 2.0
    {
        public static ModSettings Settings; // Статическая ссылка на настройки

        public override void OnLoad(Harmony harmony) //такая хрень нужна всегда в Harmony 2.0
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false); //true/false запись в лог?

            // Регистрируем настройки
            new POptions().RegisterOptions(this, typeof(ModSettings)); //this теперь нужен всегда

            // Загружаем текущие настройки
            Settings = POptions.ReadSettings<ModSettings>() ?? new ModSettings();

            harmony.PatchAll(); //тоже обязательно в Harmony 2.0
        }
    }

    [HarmonyPatch(typeof(IceCooledFanConfig))] //указываем какой класс хотим поменять, какой файл. файл нашли через программу ILspy
    [HarmonyPatch("CreateBuildingDef")] //какой метод или функцию хотим изменить
    public class CreateBuildingDef_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref BuildingDef __result)
        {
                  
            
            __result.ExhaustKilowattsWhenActive = CoolerVB.Settings.ExhaustKilowattsWhenActive; //излишек тоже влияет на работу, может даже больше
            
            __result.SelfHeatKilowattsWhenActive = CoolerVB.Settings.SelfHeatKilowattsWhenActive; //обычный режим работы obj.SelfHeatKilowattsWhenActive = (0f - COOLING_RATE) * 0.25f; private float COOLING_RATE = 32f;
        }
    }

    [HarmonyPatch(typeof(IceCooledFanConfig))]
    [HarmonyPatch("ConfigureBuildingTemplate")]
    public class IceCooledFanConfig_ConfigureBuildingTemplate_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Используем значения из настроек
            IceCooledFan iceCooledFan = go.AddOrGet<IceCooledFan>();
            iceCooledFan.coolingRate = 35f; ////мощность выработки (траты) льда и перехода его в воду - пар, большие значения - появляется бочонок с водой/паром сразу и ничего не охлаждено
            //по умолчанию 35f
            iceCooledFan.minCooledTemperature = CoolerVB.Settings.MinCooledTemperature; //это строение может понизить температуру окружающей среды только до отметки ...
            iceCooledFan.targetTemperature = CoolerVB.Settings.TargetTemperature; //температура до которой может нагреться лёд/вода/пар внутри после чего нужно будет заменить по умолчанию 278.15f

            // Остальные параметры
            go.AddOrGet<MinimumOperatingTemperature>().minimumTemperature = CoolerVB.Settings.minimumOperatingTemperature;// температура самого здания по умолчанию go.AddOrGet<MinimumOperatingTemperature>().minimumTemperature = 273.15f;
            iceCooledFan.minCoolingRange = new Vector2I(-2, 0); 
            iceCooledFan.maxCoolingRange = new Vector2I(50, 100); //по умолчанию .minCoolingRange = new Vector2I(-2, 0); maxCoolingRange = new Vector2I(2, 4);
        }
    }

        // [HarmonyPatch(typeof(IceCooledFanConfig))] // минимальная доставка
        // [HarmonyPatch("ConfigureBuildingTemplate")]
        // public class IceCooledFanConfig_ConfigureBuildingTemplate_Patch4
        // {
        //     [HarmonyPostfix]
        //     public static void Postfix(GameObject go, Tag prefab_tag)
        //     {
        //         ManualDeliveryKG manualDeliveryKG = go.AddComponent<ManualDeliveryKG>();
        //         manualDeliveryKG.MinimumMass = 5f; //с 5 не работает
        //     }
        // }
}