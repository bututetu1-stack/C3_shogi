// 画面のボタンを文字で探して押す（TEXT を置き換えて使う。例: sed 's/TEXT/準備ができた/'）
foreach (var doc in UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
{
    if (doc.rootVisualElement == null) continue;
    foreach (var b in UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Button>(doc.rootVisualElement).ToList())
    {
        if (b.text != "TEXT" || !b.enabledInHierarchy) continue;
        using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        return "押した: " + b.text;
    }
}
return "見つからない: TEXT";
