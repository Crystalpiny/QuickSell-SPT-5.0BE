using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI;

namespace UIFixesInterop
{
    /// <summary>
    /// 5.0BE 没有 UI Fixes 的多选互操作，这里提供空实现：
    /// Count 恒为 0，Apply 不执行，GetItemsToSell 会退化为单选。
    /// </summary>
    internal static class MultiSelect
    {
        public static int Count => 0;
        public static IEnumerable<Item> Items => Enumerable.Empty<Item>();
        public static void Apply(Action<Item> action, ItemUiContext context) { }
    }
}
