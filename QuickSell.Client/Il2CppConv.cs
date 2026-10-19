using System;
using Comfort.Common;

namespace QuickSell
{
    /// <summary>Mono 委托 → IL2CPP 委托的显式转换。</summary>
    internal static class Il2CppConv
    {
        public static Il2CppSystem.Action Act(Action action) => (Il2CppSystem.Action)(Delegate)action;

        public static Il2CppSystem.Action<T> Act<T>(Action<T> action) => (Il2CppSystem.Action<T>)(Delegate)action;

        public static Callback Cb(Action<IResult> action) => (Callback)(Delegate)new Action<IResult>(action);
    }
}
