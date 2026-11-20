using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 광맥 채굴 게임의 점수 / 라운드 / 점수판을 관리한다.
    /// roundSeconds 가 0 이하이면 시간 제한 없는 무한 모드.
    /// </summary>
    public class BlacksmithGame : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TMP_Text boardText;
        [SerializeField] private string title = "대장장이";
        [Tooltip("라운드 중에만 점수판을 보여준다 (대기·결과는 시작 패널이 표시)")]
        [SerializeField] private bool showBoardOnlyWhileRunning = true;

        [Header("라운드")]
        [Tooltip("0 이하이면 시간 제한 없음")]
        [SerializeField] private float roundSeconds = 60f;
        [Tooltip("끄면 시작 패널(GameStartPanel)의 Start 버튼으로 시작한다")]
        [SerializeField] private bool autoStart = false;

        [Header("점수 팝업")]
        [SerializeField] private bool showPopup = true;
        [SerializeField] private Color popupColor = new Color(1f, 0.85f, 0.2f);

        [Header("입력 (A 버튼 / 키보드 R = 시작·재시작)")]
        [SerializeField] private bool useRestartInput = true;

        private const string BestKey = "Blacksmith.Best";
        private const string OldBestKey = "PiggySmash.Best"; // 이전 이름으로 저장된 최고 점수 이어받기

        private int score;
        private int best;
        private int breakCount;
        private float remaining;
        private InputAction restartAction;

        public bool IsRunning { get; private set; }
        public int Score => score;
        public int Best => best;

        /// <summary>라운드가 시작될 때 (스포너가 필드를 초기화하는 데 사용)</summary>
        public event Action RoundStarted;
        /// <summary>라운드가 끝났을 때</summary>
        public event Action RoundEnded;

        private void Awake()
        {
            best = PlayerPrefs.GetInt(BestKey, PlayerPrefs.GetInt(OldBestKey, 0));
            // 점수판: 맨 위에 타이틀, 그 아래 점수
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
            RefreshBoard();
        }

        public void StartRound()
        {
            score = 0;
            breakCount = 0;
            remaining = roundSeconds;
            IsRunning = true;
            RoundStarted?.Invoke();
            RefreshBoard();
        }

        public void EndRound()
        {
            if (IsRunning == false) return;
            IsRunning = false;
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
            RoundEnded?.Invoke();
            RefreshBoard();
        }

        /// <summary>광맥을 캤을 때 스포너가 호출한다.</summary>
        public void AddPoints(int points, Vector3 worldPosition)
        {
            AddPoints(points, worldPosition, popupColor);
        }

        /// <summary>광맥 종류별 색으로 점수 팝업을 띄운다.</summary>
        public void AddPoints(int points, Vector3 worldPosition, Color color)
        {
            if (IsRunning == false) return;
            score += points;
            breakCount++;
            if (showPopup) ScorePopup.Spawn("+" + points, worldPosition, color);
            RefreshBoard();
        }

        private void OnRestartPerformed(InputAction.CallbackContext _)
        {
            // 스탠드에 올린 망치가 없으면 시작하지 않는다
            var rack = FindAnyObjectByType<HammerRack>();
            if (rack != null && rack.HasSelection == false) return;
            StartRound();
        }

        private void RefreshBoard()
        {
            if (boardText == null) return;

            bool visible = IsRunning || showBoardOnlyWhileRunning == false;
            if (boardText.gameObject.activeSelf != visible) boardText.gameObject.SetActive(visible);
            if (visible == false) return;

            string time = roundSeconds > 0f ? $"TIME  {Mathf.CeilToInt(remaining)}" : "FREE MODE";
            // 타이틀은 점수판 맨 위에 원래 글자 크기로, 점수 정보는 그 아래 작게
            if (IsRunning)
            {
                boardText.text =
                    $"{title}\n" +
                    $"<size=60%>SCORE  {score}</size>\n" +
                    $"<size=40%>{time}\nBEST  {best}</size>";
            }
            else
            {
                boardText.text =
                    $"{title}\n" +
                    $"<size=60%>{(breakCount > 0 ? $"FINAL  {score}" : "READY")}</size>\n" +
                    $"<size=40%>BEST  {best}</size>";
            }
        }
    }
}
