using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 월드 스페이스 패널의 Start 버튼으로 라운드를 시작한다.
    /// XRI Examples 의 SpatialPanelBase + TextButton 프리팹 구조를 그대로 사용한다.
    /// (XR Ray Interactor → TrackedDeviceGraphicRaycaster → uGUI Button.onClick)
    /// - 라운드 중에는 패널을 숨기고, 라운드가 끝나면 결과와 RESTART 버튼으로 다시 보여준다.
    /// - 스탠드에 올린(선택된) 망치가 없으면 시작 버튼을 끄고 안내 문구를 덧붙인다.
    /// </summary>
    public class GameStartPanel : MonoBehaviour
    {
        [SerializeField] private BlacksmithGame game;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text messageText;
        [Tooltip("선택된 망치가 있어야 시작할 수 있다 (비우면 씬에서 찾음)")]
        [SerializeField] private HammerRack rack;

        [Tooltip("숨기고 보여줄 오브젝트. 비우면 이 오브젝트")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool hideWhileRunning = true;

        [Header("문구")]
        [SerializeField] private string readyMessage = "대장장이\n<size=60%>망치를 들고 광석을 단조하세요!</size>";
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string restartLabel = "RESTART";
        [SerializeField] private string noHammerHint = "<size=45%><color=#FF9A7A>망치를 스탠드에 올려두세요</color></size>";

        private string baseMessage = "";
        private bool? lastCanStart;

        // 패널이 비활성화돼도 이벤트를 받아야 하므로 OnEnable/OnDisable 대신 Awake/OnDestroy 에서 구독한다.
        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (game == null) game = FindAnyObjectByType<BlacksmithGame>();
            if (rack == null) rack = FindAnyObjectByType<HammerRack>();

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

        private bool CanStart => rack == null || rack.HasSelection;

        private void Update()
        {
            bool can = CanStart;
            if (lastCanStart == can) return;
            lastCanStart = can;
            if (startButton != null) startButton.interactable = can;
            RefreshMessage();
        }

        private void OnStartClicked()
        {
            if (game == null || game.IsRunning || CanStart == false) return;
            game.StartRound();
        }

        private void SetMessage(string text)
        {
            baseMessage = text;
            RefreshMessage();
        }

        private void RefreshMessage()
        {
            if (messageText == null) return;
            messageText.text = CanStart ? baseMessage : baseMessage + "\n" + noHammerHint;
        }

        private void OnRoundStarted()
        {
            if (hideWhileRunning) panelRoot.SetActive(false);
        }

        private void OnRoundEnded()
        {
            SetMessage($"FINAL  {game.Score}\n<size=60%>BEST  {game.Best}</size>");
            if (buttonLabel != null) buttonLabel.text = restartLabel;
            panelRoot.SetActive(true);
        }

        private void ShowReady()
        {
            SetMessage(readyMessage);
            if (buttonLabel != null) buttonLabel.text = startLabel;
            panelRoot.SetActive(true);
        }
    }
}
