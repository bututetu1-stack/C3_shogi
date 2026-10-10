var ui = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>();
if (ui != null) typeof(TitleScreenUI).GetMethod("OnStartClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(ui, null);
return (ui == null ? "no title; " : "") + GameManager.Instance.currentPhase.ToString();
