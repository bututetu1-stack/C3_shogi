using UnityEngine;
using UnityEngine.UIElements;

public class GameOverUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private Label resultLabel;
    private Button retryButton;
    private bool isSubscribed;

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= ShowGameOver;
        isSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (isSubscribed || GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver += ShowGameOver;
        isSubscribed = true;
    }

    void Start()
    {
        TrySubscribe();

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            root = uiDocument.rootVisualElement;
            root.Clear(); // UXML由来の子要素を除去
            root.style.display = DisplayStyle.None;
        }
    }

    private void ShowGameOver(Team winner)
    {
        if (root == null) return;

        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        // 背景パネル
        var panel = new VisualElement();
        panel.style.backgroundColor = new Color(0, 0, 0, 0.8f);
        panel.style.paddingTop = 40;
        panel.style.paddingBottom = 40;
        panel.style.paddingLeft = 60;
        panel.style.paddingRight = 60;
        panel.style.alignItems = Align.Center;
        panel.style.borderTopLeftRadius = 16;
        panel.style.borderTopRightRadius = 16;
        panel.style.borderBottomLeftRadius = 16;
        panel.style.borderBottomRightRadius = 16;

        resultLabel = new Label();
        resultLabel.style.fontSize = 48;
        resultLabel.style.color = Color.white;
        resultLabel.style.marginBottom = 30;

        if (winner == Team.Player)
        {
            resultLabel.text = "Victory!";
            resultLabel.style.color = new Color(0.3f, 0.9f, 0.4f);
        }
        else
        {
            resultLabel.text = "Defeat...";
            resultLabel.style.color = new Color(0.9f, 0.3f, 0.3f);
        }

        retryButton = new Button();
        retryButton.text = "Retry";
        retryButton.style.fontSize = 24;
        retryButton.style.paddingTop = 10;
        retryButton.style.paddingBottom = 10;
        retryButton.style.paddingLeft = 40;
        retryButton.style.paddingRight = 40;
        retryButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
        retryButton.style.color = Color.white;
        retryButton.style.borderTopLeftRadius = 8;
        retryButton.style.borderTopRightRadius = 8;
        retryButton.style.borderBottomLeftRadius = 8;
        retryButton.style.borderBottomRightRadius = 8;
        retryButton.clicked += OnRetryClicked;

        panel.Add(resultLabel);
        panel.Add(retryButton);
        root.Add(panel);
    }

    private void OnRetryClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
