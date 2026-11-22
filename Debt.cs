using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;

namespace WobblyDebtMod
{
    [BepInPlugin("com.custom.wobblydebt", "Wobbly Debt Mod", "1.0.0")]
    [BepInDependency("net.lstwo.lstwomods_wobblylife")]
    public class DebtPlugin : BaseUnityPlugin
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void Awake()
        {
            var harmony = new Harmony("com.custom.wobblydebt");
            var empType = AccessTools.TypeByName("lstwoMODS_WobblyLife.Mods.EmploymentManager");

            if (empType == null) return;

            var resetMethod = AccessTools.Method(empType, "ResetMoney");
            if (resetMethod != null)
                harmony.Patch(resetMethod, prefix: new HarmonyMethod(typeof(DebtPlugin), nameof(ResetMoneyPrefix)));

            var giveMethod = AccessTools.Method(empType, "_GiveMoney");
            if (giveMethod != null)
                harmony.Patch(giveMethod, prefix: new HarmonyMethod(typeof(DebtPlugin), nameof(GiveMoneyPrefix)));
        }

        public static bool ResetMoneyPrefix(object __instance)
        {
            object employment = GetTargetEmployment(__instance);
            if (employment == null) return true;

            if (TryInvoke(employment, "ResetMoney") ||
                TryInvoke(employment, "ServerSetMoney", 0) ||
                TryInvoke(employment, "SetMoney", 0))
                return false;

            int current = GetCurrentMoney(employment);
            if (current != 0) ModifyMoney(employment, -current);
            return false;
        }

        public static bool GiveMoneyPrefix(object __instance)
        {
            if ((GetAsyncKeyState(0xA0) & 0x8000) == 0) return true;

            var amountField = AccessTools.Field(__instance.GetType(), "giveMoneyAmount");
            if (amountField == null) return true;

            int amount = Math.Abs(Convert.ToInt32(amountField.GetValue(__instance)));
            if (amount <= 0) return false;

            object employment = GetTargetEmployment(__instance);
            if (employment == null) return true;

            ModifyMoney(employment, -amount);
            return false;
        }

        private static object GetTargetEmployment(object modInstance)
        {
            var playerProp = AccessTools.PropertyGetter(modInstance.GetType(), "Player");
            object playerRef = playerProp?.Invoke(modInstance, null);
            if (playerRef == null) return null;

            var ctrlProp = AccessTools.PropertyGetter(playerRef.GetType(), "Controller");
            object controller = ctrlProp != null ? ctrlProp.Invoke(playerRef, null) : playerRef;
            if (controller == null) return null;

            var empProp = AccessTools.PropertyGetter(controller.GetType(), "PlayerEmployment");
            if (empProp != null) return empProp.Invoke(controller, null);

            var getEmp = AccessTools.Method(controller.GetType(), "GetPlayerEmployment");
            if (getEmp != null) return getEmp.Invoke(controller, null);

            var empType = AccessTools.TypeByName("PlayerEmployment");
            var getComp = AccessTools.Method(controller.GetType(), "GetComponent", new[] { typeof(Type) });
            return (empType != null && getComp != null) ? getComp.Invoke(controller, new object[] { empType }) : null;
        }

        private static int GetCurrentMoney(object employment)
        {
            var getMoney = AccessTools.Method(employment.GetType(), "GetMoney");
            if (getMoney != null) return Convert.ToInt32(getMoney.Invoke(employment, null));

            var moneyProp = AccessTools.PropertyGetter(employment.GetType(), "Money");
            if (moneyProp != null) return Convert.ToInt32(moneyProp.Invoke(employment, null));

            var moneyField = AccessTools.Field(employment.GetType(), "money");
            if (moneyField != null) return Convert.ToInt32(moneyField.GetValue(employment));

            return 0;
        }

        private static void ModifyMoney(object employment, int delta)
        {
            if (TryInvoke(employment, "ServerAddMoney", delta)) return;
            if (TryInvoke(employment, "AddMoney", delta)) return;
            if (TryInvoke(employment, "GiveMoney", delta)) return;

            int next = Math.Max(0, GetCurrentMoney(employment) + delta);
            if (TryInvoke(employment, "ServerSetMoney", next)) return;
            TryInvoke(employment, "SetMoney", next);
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = AccessTools.Method(target.GetType(), methodName);
            if (method == null) return false;
            method.Invoke(target, args);
            return true;
        }
    }
}
