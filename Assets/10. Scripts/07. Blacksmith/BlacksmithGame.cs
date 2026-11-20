using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 광맥 채굴 라운드를 관리한다. 점수는 기록하지 않고, 캔 광물은 광물 보관함(OreInventory)에 쌓인다.
    /// 점수판(Score Board)에는 타이틀과 라운드 남은 시간만 표시한다.
    /// roundSeconds 가 0 이하이면 시간 제한 없는 무한 모드.
    /// </summary>
    public class BlacksmithGame : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TMP_Text boardText;
        [SerializeField] private string title = "대장장이";
        [Tooltip("라운드 중에만 점수판을 보여준다")]
        [SerializeField] private bool showBoardOnlyWhileRunning = false;

        [Header("라운드")]
        [Tooltip("0 이하이면 시간 제한 없음")]
        [SerializeField] private float roundSeconds = 60f;
        [Tooltip("끄면 시작 패널(GameStartPanel)의 Start 버튼으로 시작한다")]
        [SerializeField] private bool autoStart = false;

        [Header("광물")]
        [Tooltip("캔 광물을 넣을 보관함 (비우면 씬에서 찾음)")]
        [SerializeField] private OreInventory inventory;
        [SerializeField] private bool showPopup = true;

        [Header("입력 (A 버튼 / 키보드 R = 시작·재시작)")]
        [SerializeField] private bool useRestartInput = true;

        private float remaining;
        private int lastShownSeconds = -1;
        private InputAction restartAction;

        public bool IsRunning { get; private set; }

        /// <summary>라운드가 시작될 때 (스포너가 필드를 초기화하는 데 사용)</summary>
        public event Action RoundStarted;
        /// <summary>라운드가 끝났을 때</summary>
        public event Action RoundEnded;

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<OreInventory>();
            // 점수판: 대기 중엔 타이틀, 라운드 중엔 같은 자리에 남은 시간
            if (boardText != null) boardText.alignment = TextAlignmentOptions.Top;
        }

        private void OnEnable()
        {
            if (useRestartInput == false) return;
            restartAction = new InputAction("Blacksmith Restart", InputActionType.Button);
            restartAction.AddBinding("<XRController>{RightHand}/primaryButton");
            restartAction.AddBinding("<Keyboard>/r");
            restartAction.performed += OnRestartPerformed;
            restartAction.Enable();
        }

        private void OnDisable()
        {
            if (restartAction == null) return;
            restartAction.performed -= OnRestartPerformed;
            restartAction.Disable();
            restartAction.Dispose();
            restartAction = null;
        }

        private void Start()
        {
            if (autoStart) StartRound();
            else RefreshBoard();
        }

        private void Update()
        {
            if (IsRunning == false) return;

            if (roundSeconds > 0f)
            {
                remaining -= Time.deltaTime;
                if (remaining <= 0f)
                {
                    remaining = 0f;
                    EndRound();
                    return;
                }
            }
            // 초가 바뀔 때만 다시 그린다
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != lastShownSeconds) RefreshBoard();
        }

        public void StartRound()
        {
            remaining = roundSeconds;
            IsRunning = true;
            RoundStarted?.Invoke();
            RefreshBoard();
        }

        public void EndRound()
        {
            if (IsRunning == false) return;
            IsRunning = false;
            RoundEnded?.Invoke();
            RefreshBoard();
        }

        /// <summary>광맥을 캤을 때 스포너가 호출한다. 광물을 보관함에 넣고 팝업을 띄운다.</summary>
        public void AddOre(string ore, Vector3 worldPosition, Color color)
        {
            if (IsRunning == false) return;
            if (inventory != null) inventory.Add(ore);
            if (showPopup) ScorePopup.Spawn("+1 " + OreInventory.Label(ore), worldPosition, color);
        }

        private void OnRestartPerformed(InputAction.CallbackContext _)
        {
            // 스탠드에 올린 망치가 없으면 시작하지 않는다
            var rack = FindAnyObjectByType<HammerRack>();
            if (rack != null && rack.HasSelection == false) return;
            if (IsRunning) return;
            StartRound();
        }

        private void RefreshBoard()
        {
            if (boardText == null) return;

            bool visible = IsRunning || showBoardOnlyWhileRunning == false;
            if (boardText.gameObject.activeSelf != visible) boardText.gameObject.SetActive(visible);
            if (visible == false) return;

            lastShownSeconds = Mathf.CeilToInt(remaining);
            // 라운드 중에는 타이틀 자리에 남은 시간을 그대로 띄운다 (아래 Ore Board 와 겹치지 않도록 한 줄만)
            if (IsRunning && roundSeconds > 0f)
                boardText.text = $"남은 시간 {lastShownSeconds}";
            else
                boardText.text = title;
        }
    }
}
