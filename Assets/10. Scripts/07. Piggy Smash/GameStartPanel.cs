using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 월드 스페이스 패널의 Start 버튼으로 라운드를 시작한다.
    /// XRI Examples 의 SpatialPanelBase + TextButton 프리팹 구조를 그대로 사용한다.
    /// (XR Ray Interactor → TrackedDeviceGraphicRaycaster → uGUI Button.onClick)
    /// - 라운드 중에는 패널을 숨기고, 라운드가 끝나면 결과와 RESTART 버튼으로 다시 보여준다.
    /// </summary>
    public class GameStartPanel : MonoBehaviour
    {
        [SerializeField] private PiggySmashGame game;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text messageText;

        [Tooltip("숨기고 보여줄 오브젝트. 비우면 이 오브젝트")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool hideWhileRunning = true;

        [Header("문구")]
        [SerializeField] private string readyMessage = "ORE SMASH\n<size=60%>Grab the hammer and smash the ores!</size>";
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string restartLabel = "RESTART";

        // 패널이 비활성화돼도 이벤트를 받아야 하므로 OnEnable/OnDisable 대신 Awake/OnDestroy 에서 구독한다.
        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (game == null) game = FindAnyObjectByType<PiggySmashGame>();

            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (game != null)
            {
                game.RoundStarted += OnRoundStarted;
                game.RoundEnded += OnRoundEnded;
            }
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (game != null)
            {
                game.RoundStarted -= OnRoundStarted;
                game.RoundEnded -= OnRoundEnded;
            }
        }

        private void Start()
        {
            if (game != null && game.IsRunning) OnRoundStarted();
            else ShowReady();
        }

        private void OnStartClicked()
        {
            if (game == null || game.IsRunning) return;
            game.StartRound();
        }

        private void OnRoundStarted()
        {
            if (hideWhileRunning) panelRoot.SetActive(false);
        }

        private void OnRoundEnded()
        {
            if (messageText != null) messageText.text = $"FINAL  {game.Score}\n<size=60%>BEST  {game.Best}</size>";
            if (buttonLabel != null) buttonLabel.text = restartLabel;
            panelRoot.SetActive(true);
        }

        private void ShowReady()
        {
            if (messageText != null) messageText.text = readyMessage;
            if (buttonLabel != null) buttonLabel.text = startLabel;
            panelRoot.SetActive(true);
        }
    }
}
