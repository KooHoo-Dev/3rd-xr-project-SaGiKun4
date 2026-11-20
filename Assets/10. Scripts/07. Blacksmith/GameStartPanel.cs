using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 화로 앞 시작 패널. 버튼으로 라운드를 시작한다.
    /// (XR Ray Interactor → TrackedDeviceGraphicRaycaster → uGUI Button.onClick)
    /// - 라운드 중에는 패널을 숨기고, 라운드가 끝나면 다시 보여준다.
    /// - 점수는 표시하지 않는다. 문구는 망치 선택 상태로만 바뀐다:
    ///     스탠드에 망치가 있음 → readyMessage, 없음 → noHammerMessage (시작 버튼도 꺼짐)
    /// - 버튼 글자는 씬에서 적어 둔 그대로 둔다.
    /// </summary>
    public class GameStartPanel : MonoBehaviour
    {
        [SerializeField] private BlacksmithGame game;
        [SerializeField] private Button startButton;
        [Tooltip("(사용 안 함) 버튼 글자는 씬에서 직접 정한다")]
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text messageText;
        [Tooltip("선택된 망치가 있어야 시작할 수 있다 (비우면 씬에서 찾음)")]
        [SerializeField] private HammerRack rack;

        [Tooltip("숨기고 보여줄 오브젝트. 비우면 이 오브젝트")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool hideWhileRunning = true;

        [Header("문구")]
        [Tooltip("스탠드에 망치가 올라가 있을 때")]
        [SerializeField] private string readyMessage = "화로를 열고 작업을 시작하세요!";
        [Tooltip("스탠드에 망치가 없을 때")]
        [SerializeField] private string noHammerMessage = "작업 시작 전 망치를 선택하세요!";

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
            else Show();
        }

        private bool CanStart => rack == null || rack.HasSelection;

        private void Update()
        {
            bool can = CanStart;
            if (lastCanStart == can) return;
            lastCanStart = can;
            Refresh();
        }

        private void OnStartClicked()
        {
            if (game == null || game.IsRunning || CanStart == false) return;
            game.StartRound();
        }

        private void OnRoundStarted()
        {
            if (hideWhileRunning) panelRoot.SetActive(false);
        }

        private void OnRoundEnded() => Show();

        private void Show()
        {
            Refresh();
            panelRoot.SetActive(true);
        }

        private void Refresh()
        {
            bool can = CanStart;
            if (startButton != null) startButton.interactable = can;
            if (messageText != null) messageText.text = can ? readyMessage : noHammerMessage;
        }
    }
}
