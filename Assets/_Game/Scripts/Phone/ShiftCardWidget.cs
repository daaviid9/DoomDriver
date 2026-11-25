using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Displays and controls the Workday & Shift Dispatcher Card on the Phone OS Home Screen.
    /// Strictly mirrors the Figma / HTML design with 3 lifecycle states:
    /// 1. Off-Duty Morning Briefing (shows quota, bills, overtime bonus, Start Shift & Clock In button).
    /// 2. Active Shift (shows "kolko z kolko" deliveries completed, progress bar, overtime notice, and early Clock Out button).
    /// 3. Day Summary (shows net settlement receipt and Next Day button).
    /// </summary>
    public class ShiftCardWidget : MonoBehaviour
    {
        [Header("State Containers")]
        [SerializeField] private GameObject briefingContainer;
        [SerializeField] private GameObject activeContainer;
        [SerializeField] private GameObject summaryContainer;

        [Header("Shared Header Elements")]
        [SerializeField] private TMP_Text statusPillText;
        [SerializeField] private Image statusPillImage;
        [SerializeField] private TMP_Text dayTagText;

        [Header("Briefing State Elements")]
        [SerializeField] private TMP_Text briefingQuotaHeroText;
        [SerializeField] private TMP_Text minQuotaValueText;
        [SerializeField] private TMP_Text dailyBillsValueText;
        [SerializeField] private TMP_Text overtimeValueText;
        [SerializeField] private Button startShiftButton;

        [Header("Active Shift State Elements")]
        [SerializeField] private TMP_Text activeProgressHeroText;
        [SerializeField] private TMP_Text activeEarningsText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TMP_Text activeStatusNoteText;
        [SerializeField] private GameObject overtimeCalloutPanel;
        [SerializeField] private Button clockOutButton;

        [Header("Summary State Elements")]
        [SerializeField] private TMP_Text summaryNetHeroText;
        [SerializeField] private TMP_Text summaryDeliveredText;
        [SerializeField] private TMP_Text summaryGrossText;
        [SerializeField] private TMP_Text summaryBillsText;
        [SerializeField] private Button advanceDayButton;

        [Header("Visual Colors")]
        private static readonly Color ColorNavy = new Color32(0x1E, 0x1B, 0x4B, 0xFF);       // #1E1B4B
        private static readonly Color ColorMintBg = new Color32(0xD1, 0xFA, 0xE5, 0xFF);     // #D1FAE5
        private static readonly Color ColorMintText = new Color32(0x06, 0x5F, 0x46, 0xFF);   // #065F46
        private static readonly Color ColorSlateBg = new Color32(0xF1, 0xF5, 0xF9, 0xFF);    // #F1F5F9
        private static readonly Color ColorSlateText = new Color32(0x47, 0x55, 0x69, 0xFF);  // #475569
        private static readonly Color ColorAmberBg = new Color32(0xFE, 0xF3, 0xC7, 0xFF);    // #FEF3C7
        private static readonly Color ColorAmberText = new Color32(0x92, 0x40, 0x0E, 0xFF);  // #92400E
        private static readonly Color ColorIndigoBg = new Color32(0xE0, 0xE7, 0xFF, 0xFF);   // #E0E7FF
        private static readonly Color ColorIndigoText = new Color32(0x37, 0x30, 0xA3, 0xFF); // #3730A3
        private static readonly Color ColorGreenFill = new Color32(0x10, 0xB9, 0x81, 0xFF);  // #10B981
        private static readonly Color ColorAmberFill = new Color32(0xF5, 0x9E, 0x0B, 0xFF);  // #F59E0B

        private void Awake()
        {
            EnsureUIHierarchy();
            ApplyLayoutPositions();
            BindButtons();
        }

        private void OnEnable()
        {
            ShiftManager.OnShiftStarted += HandleShiftStarted;
            ShiftManager.OnDeliveryCountChanged += HandleDeliveryCountChanged;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
            WalletManager.OnBalanceChanged += HandleBalanceChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted += Refresh;
                GameManager.Instance.OnOrderSelectionStarted += Refresh;
                GameManager.Instance.OnDaySummaryStarted += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnDeliveryCountChanged -= HandleDeliveryCountChanged;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
            WalletManager.OnBalanceChanged -= HandleBalanceChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted -= Refresh;
                GameManager.Instance.OnOrderSelectionStarted -= Refresh;
                GameManager.Instance.OnDaySummaryStarted -= Refresh;
            }
        }

        private void Start()
        {
            Refresh();
        }

        private void HandleShiftStarted() => Refresh();
        private void HandleDeliveryCountChanged(int delivered, int quota) => Refresh();
        private void HandleDayAdvanced(int newDay) => Refresh();
        private void HandleBalanceChanged(float balance) => Refresh();

        /// <summary>
        /// Fully refreshes the card content and toggles between Briefing, Active Shift, and Summary states.
        /// </summary>
        public void Refresh()
        {
            int day = ShiftManager.Instance != null ? ShiftManager.Instance.CurrentDay : 1;
            int delivered = ShiftManager.Instance != null ? ShiftManager.Instance.DeliveriesCompletedToday : 0;
            int quota = ShiftManager.Instance != null ? ShiftManager.Instance.DailyQuota : 3;
            float earnings = ShiftManager.Instance != null ? ShiftManager.Instance.ShiftEarnings : 0f;
            float bills = 25f; // Daily living bills & rent due tonight
            bool isShiftActive = ShiftManager.Instance != null && ShiftManager.Instance.IsShiftActive;
            bool isQuotaMet = ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet;
            bool isSummary = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DaySummary;

            if (dayTagText != null)
            {
                dayTagText.text = $"DAY {day}";
            }

            // STATE 3: DAY SUMMARY
            if (isSummary)
            {
                if (briefingContainer != null) briefingContainer.SetActive(false);
                if (activeContainer != null) activeContainer.SetActive(false);
                if (summaryContainer != null) summaryContainer.SetActive(true);

                if (statusPillText != null) statusPillText.text = "🌙 SHIFT COMPLETE";
                if (statusPillImage != null) statusPillImage.color = ColorIndigoBg;
                if (statusPillText != null) statusPillText.color = ColorIndigoText;

                float net = earnings - bills;
                if (summaryNetHeroText != null)
                {
                    string sign = net >= 0f ? "+" : "-";
                    string hexColor = net >= 0f ? "#059669" : "#E11D48";
                    summaryNetHeroText.text = $"<color={hexColor}>{sign} € {Mathf.Abs(net):F2} NET</color>";
                }

                if (summaryDeliveredText != null)
                {
                    summaryDeliveredText.text = isQuotaMet && delivered > quota
                        ? $"{delivered} / {quota} (+{delivered - quota} Overtime)"
                        : $"{delivered} / {quota}";
                }

                if (summaryGrossText != null) summaryGrossText.text = $"+ € {earnings:F2}";
                if (summaryBillsText != null) summaryBillsText.text = $"- € {bills:F2}";
            }
            // STATE 2: ACTIVE SHIFT (During delivery loop)
            else if (isShiftActive)
            {
                if (briefingContainer != null) briefingContainer.SetActive(false);
                if (activeContainer != null) activeContainer.SetActive(true);
                if (summaryContainer != null) summaryContainer.SetActive(false);

                if (isQuotaMet)
                {
                    if (statusPillText != null) statusPillText.text = "🔥 OVERTIME ACTIVE";
                    if (statusPillImage != null) statusPillImage.color = ColorAmberBg;
                    if (statusPillText != null) statusPillText.color = ColorAmberText;
                }
                else
                {
                    if (statusPillText != null) statusPillText.text = "● LIVE SHIFT";
                    if (statusPillImage != null) statusPillImage.color = ColorMintBg;
                    if (statusPillText != null) statusPillText.color = ColorMintText;
                }

                if (activeProgressHeroText != null)
                {
                    activeProgressHeroText.text = $"{delivered} / {quota} ORDERS";
                }

                if (activeEarningsText != null)
                {
                    activeEarningsText.text = $"+ € {earnings:F2}";
                }

                float ratio = quota > 0 ? Mathf.Clamp01((float)delivered / quota) : 0f;
                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = ratio;
                    progressBarFill.color = isQuotaMet ? ColorAmberFill : ColorGreenFill;
                }

                if (overtimeCalloutPanel != null)
                {
                    overtimeCalloutPanel.SetActive(isQuotaMet);
                }

                if (activeStatusNoteText != null)
                {
                    activeStatusNoteText.gameObject.SetActive(!isQuotaMet);
                    int remaining = Mathf.Max(0, quota - delivered);
                    int percent = Mathf.RoundToInt(ratio * 100f);
                    activeStatusNoteText.text = $"{remaining} more orders required today ({percent}% quota met)";
                }
            }
            // STATE 1: MORNING BRIEFING (Before starting shift)
            else
            {
                if (briefingContainer != null) briefingContainer.SetActive(true);
                if (activeContainer != null) activeContainer.SetActive(false);
                if (summaryContainer != null) summaryContainer.SetActive(false);

                if (statusPillText != null) statusPillText.text = "• OFF-DUTY • MORNING";
                if (statusPillImage != null) statusPillImage.color = ColorSlateBg;
                if (statusPillText != null) statusPillText.color = ColorSlateText;

                if (briefingQuotaHeroText != null)
                {
                    briefingQuotaHeroText.text = $"{quota} DELIVERIES";
                }

                if (minQuotaValueText != null) minQuotaValueText.text = $"{quota} Orders";
                if (dailyBillsValueText != null) dailyBillsValueText.text = $"€ {bills:F2}";
                if (overtimeValueText != null) overtimeValueText.text = "+25% Tip";
            }
        }

        private void BindButtons()
        {
            if (startShiftButton != null)
            {
                startShiftButton.onClick.RemoveAllListeners();
                startShiftButton.onClick.AddListener(() =>
                {
                    if (ShiftManager.Instance != null)
                    {
                        ShiftManager.Instance.StartShift();
                    }
                    Refresh();
                });
            }

            if (clockOutButton != null)
            {
                clockOutButton.onClick.RemoveAllListeners();
                clockOutButton.onClick.AddListener(() =>
                {
                    if (ShiftManager.Instance != null)
                    {
                        ShiftManager.Instance.EndShift();
                    }
                    Refresh();
                });
            }

            if (advanceDayButton != null)
            {
                advanceDayButton.onClick.RemoveAllListeners();
                advanceDayButton.onClick.AddListener(() =>
                {
                    if (ShiftManager.Instance != null)
                    {
                        ShiftManager.Instance.AdvanceToNextDay();
                    }
                    Refresh();
                });
            }
        }

        /// <summary>
        /// Automatically discovers child UI elements if they are not explicitly wired in inspector.
        /// If the hierarchy does not exist yet at runtime, dynamically instantiates a pixel-perfect layout.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            // Search children by name
            var tBriefing = transform.Find("BriefingContainer");
            var tActive = transform.Find("ActiveContainer");
            var tSummary = transform.Find("SummaryContainer");

            if (tBriefing != null) briefingContainer = tBriefing.gameObject;
            if (tActive != null) activeContainer = tActive.gameObject;
            if (tSummary != null) summaryContainer = tSummary.gameObject;

            // Search header
            var tPill = transform.Find("TopRow/StatusPill");
            if (tPill != null)
            {
                statusPillImage = tPill.GetComponent<Image>();
                statusPillText = tPill.GetComponentInChildren<TMP_Text>(true);
            }
            var tDayTag = transform.Find("TopRow/DayTag");
            if (tDayTag != null) dayTagText = tDayTag.GetComponentInChildren<TMP_Text>(true);

            // Search Briefing
            if (briefingContainer != null)
            {
                var tHero = briefingContainer.transform.Find("HeroText") ?? briefingContainer.transform.Find("HeroRow/HeroText");
                if (tHero != null) briefingQuotaHeroText = tHero.GetComponent<TMP_Text>();

                var tPills = briefingContainer.transform.Find("PillsRow");
                if (tPills != null)
                {
                    var p1 = tPills.Find("Pill1/ValueText");
                    if (p1 != null) minQuotaValueText = p1.GetComponent<TMP_Text>();
                    var p2 = tPills.Find("Pill2/ValueText");
                    if (p2 != null) dailyBillsValueText = p2.GetComponent<TMP_Text>();
                    var p3 = tPills.Find("Pill3/ValueText");
                    if (p3 != null) overtimeValueText = p3.GetComponent<TMP_Text>();
                }

                var tBtn = briefingContainer.transform.Find("StartShiftButton");
                if (tBtn != null) startShiftButton = tBtn.GetComponent<Button>();
            }

            // Search Active
            if (activeContainer != null)
            {
                var tHero = activeContainer.transform.Find("ProgressHeroText") ?? activeContainer.transform.Find("HeroRow/ProgressHeroText");
                if (tHero != null) activeProgressHeroText = tHero.GetComponent<TMP_Text>();

                var tPay = activeContainer.transform.Find("ShiftPayBadge/PayText") ?? activeContainer.transform.Find("HeroRow/ShiftPayBadge/PayText");
                if (tPay != null) activeEarningsText = tPay.GetComponent<TMP_Text>();

                var tTrack = activeContainer.transform.Find("ProgressBar/Fill");
                if (tTrack != null) progressBarFill = tTrack.GetComponent<Image>();

                var tNote = activeContainer.transform.Find("StatusNoteText");
                if (tNote != null) activeStatusNoteText = tNote.GetComponent<TMP_Text>();

                var tCallout = activeContainer.transform.Find("OvertimeCallout");
                if (tCallout != null) overtimeCalloutPanel = tCallout.gameObject;

                var tClockOut = activeContainer.transform.Find("ClockOutButton");
                if (tClockOut != null) clockOutButton = tClockOut.GetComponent<Button>();
            }

            // Search Summary
            if (summaryContainer != null)
            {
                var tNet = summaryContainer.transform.Find("NetHeroText");
                if (tNet != null) summaryNetHeroText = tNet.GetComponent<TMP_Text>();

                var tRec = summaryContainer.transform.Find("ReceiptBox");
                if (tRec != null)
                {
                    var r1 = tRec.Find("Line1/Value");
                    if (r1 != null) summaryDeliveredText = r1.GetComponent<TMP_Text>();
                    var r2 = tRec.Find("Line2/Value");
                    if (r2 != null) summaryGrossText = r2.GetComponent<TMP_Text>();
                    var r3 = tRec.Find("Line3/Value");
                    if (r3 != null) summaryBillsText = r3.GetComponent<TMP_Text>();
                }

                var tAdv = summaryContainer.transform.Find("AdvanceDayButton");
                if (tAdv != null) advanceDayButton = tAdv.GetComponent<Button>();
            }

            // If still missing, dynamically construct pixel-perfect UI
            if (briefingContainer == null || activeContainer == null)
            {
                BuildRuntimeHierarchy();
            }

            ApplyLayoutPositions();
        }

        /// <summary>
        /// Applies generous, non-overlapping RectTransform bounds to all elements within the card.
        /// Guarantees that neither text nor buttons ever collide or get clipped.
        /// </summary>
        public void ApplyLayoutPositions()
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(460f, 250f);
            }

            var topRow = transform.Find("TopRow") as RectTransform;
            if (topRow != null)
            {
                topRow.anchorMin = new Vector2(0f, 1f);
                topRow.anchorMax = new Vector2(1f, 1f);
                topRow.pivot = new Vector2(0.5f, 1f);
                topRow.anchoredPosition = new Vector2(0f, -16f);
                topRow.sizeDelta = new Vector2(-36f, 28f);
            }

            // Briefing Container
            if (briefingContainer != null)
            {
                var brt = briefingContainer.GetComponent<RectTransform>();
                if (brt != null)
                {
                    brt.anchorMin = new Vector2(0f, 1f);
                    brt.anchorMax = new Vector2(1f, 1f);
                    brt.pivot = new Vector2(0.5f, 1f);
                    brt.anchoredPosition = new Vector2(0f, -54f);
                    brt.sizeDelta = new Vector2(-36f, 184f);
                }
                SetChildPos(briefingContainer.transform, "Kicker", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14));
                SetChildPos(briefingContainer.transform, "HeroText", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30));
                SetChildPos(briefingContainer.transform, "IconBox", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(40, 40));
                SetChildPos(briefingContainer.transform, "PillsRow", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 52));
                SetChildPos(briefingContainer.transform, "StartShiftButton", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            }

            // Active Container
            if (activeContainer != null)
            {
                var art = activeContainer.GetComponent<RectTransform>();
                if (art != null)
                {
                    art.anchorMin = new Vector2(0f, 1f);
                    art.anchorMax = new Vector2(1f, 1f);
                    art.pivot = new Vector2(0.5f, 1f);
                    art.anchoredPosition = new Vector2(0f, -54f);
                    art.sizeDelta = new Vector2(-36f, 184f);
                }
                SetChildPos(activeContainer.transform, "Kicker", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14));
                SetChildPos(activeContainer.transform, "ProgressHeroText", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30));
                SetChildPos(activeContainer.transform, "ShiftPayBadge", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(110, 36));
                SetChildPos(activeContainer.transform, "ProgressBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 14));
                SetChildPos(activeContainer.transform, "StatusNoteText", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(0, 18));
                SetChildPos(activeContainer.transform, "OvertimeCallout", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(0, 24));
                SetChildPos(activeContainer.transform, "ClockOutButton", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            }

            // Summary Container
            if (summaryContainer != null)
            {
                var srt = summaryContainer.GetComponent<RectTransform>();
                if (srt != null)
                {
                    srt.anchorMin = new Vector2(0f, 1f);
                    srt.anchorMax = new Vector2(1f, 1f);
                    srt.pivot = new Vector2(0.5f, 1f);
                    srt.anchoredPosition = new Vector2(0f, -54f);
                    srt.sizeDelta = new Vector2(-36f, 184f);
                }
                SetChildPos(summaryContainer.transform, "Kicker", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14));
                SetChildPos(summaryContainer.transform, "NetHeroText", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30));
                SetChildPos(summaryContainer.transform, "ReceiptBox", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 58));
                SetChildPos(summaryContainer.transform, "AdvanceDayButton", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            }
        }

        private void SetChildPos(Transform parent, string childName, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(childName) as RectTransform;
            if (t != null)
            {
                t.anchorMin = aMin;
                t.anchorMax = aMax;
                t.pivot = pivot;
                t.anchoredPosition = pos;
                t.sizeDelta = size;
            }
        }

        private void BuildRuntimeHierarchy()
        {
            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts/FredokaOne-Regular SDF");
            if (fontAsset == null)
            {
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in fonts)
                {
                    if (f.name.Contains("Fredoka") || f.name.Contains("Bruno"))
                    {
                        fontAsset = f;
                        break;
                    }
                }
            }

            // Card background setup
            var cardImage = GetComponent<Image>();
            if (cardImage == null) cardImage = gameObject.AddComponent<Image>();
            cardImage.color = Color.white;
            cardImage.type = Image.Type.Sliced;

            var outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = ColorNavy;
            outline.effectDistance = new Vector2(2, -2);

            var shadow = GetComponent<Shadow>();
            if (shadow == null) shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0xFB, 0xBF, 0x24, 0xFF); // #FBBF24
            shadow.effectDistance = new Vector2(0, -6);

            // TOP ROW
            var topRow = CreateRect("TopRow", transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-36, 28));
            
            // Status Pill
            var pillGo = CreateRect("StatusPill", topRow.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(170, 26));
            statusPillImage = pillGo.AddComponent<Image>();
            statusPillImage.color = ColorSlateBg;
            statusPillImage.type = Image.Type.Sliced;
            var pillOutline = pillGo.AddComponent<Outline>();
            pillOutline.effectColor = ColorNavy;
            pillOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var pillTextGo = CreateRect("Text", pillGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            statusPillText = pillTextGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) statusPillText.font = fontAsset;
            statusPillText.fontSize = 10;
            statusPillText.fontStyle = FontStyles.Bold;
            statusPillText.color = ColorSlateText;
            statusPillText.alignment = TextAlignmentOptions.Center;

            // Day Tag
            var dayGo = CreateRect("DayTag", topRow.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(70, 24));
            var dayImg = dayGo.AddComponent<Image>();
            dayImg.color = new Color32(0xF8, 0xFA, 0xFC, 0xFF);
            var dayOut = dayGo.AddComponent<Outline>();
            dayOut.effectColor = new Color32(0xCB, 0xD5, 0xE1, 0xFF);
            dayOut.effectDistance = new Vector2(1, -1);

            var dayTextGo = CreateRect("Text", dayGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dayTagText = dayTextGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) dayTagText.font = fontAsset;
            dayTagText.fontSize = 10;
            dayTagText.fontStyle = FontStyles.Bold;
            dayTagText.color = ColorNavy;
            dayTagText.alignment = TextAlignmentOptions.Center;

            // 1. BRIEFING CONTAINER
            var bGo = CreateRect("BriefingContainer", transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(-36, 184));
            briefingContainer = bGo;

            // Hero
            CreateText("Kicker", bGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14), "TODAY'S SHIFT QUOTA", 10, FontStyles.Bold, new Color32(0xEA, 0x58, 0x0C, 0xFF), fontAsset, TextAlignmentOptions.Left);
            var bHero = CreateText("HeroText", bGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30), "3 DELIVERIES", 22, FontStyles.Bold, ColorNavy, fontAsset, TextAlignmentOptions.Left);
            briefingQuotaHeroText = bHero.GetComponent<TMP_Text>();

            // Lightning icon
            var iconBox = CreateRect("IconBox", bGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(40, 40));
            var iconImg = iconBox.AddComponent<Image>();
            iconImg.color = ColorAmberBg;
            var iconOut = iconBox.AddComponent<Outline>();
            iconOut.effectColor = ColorNavy;
            iconOut.effectDistance = new Vector2(1.5f, -1.5f);
            var iconShadow = iconBox.AddComponent<Shadow>();
            iconShadow.effectColor = ColorNavy;
            iconShadow.effectDistance = new Vector2(0, -3);
            CreateText("Emoji", iconBox.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "⚡", 18, FontStyles.Bold, new Color32(0xF5, 0x9E, 0x0B, 0xFF), fontAsset, TextAlignmentOptions.Center);

            // 3 Stat Pills
            var pillsRow = CreateRect("PillsRow", bGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 52));
            var hlg = pillsRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var p1 = CreateStatPill("Pill1", pillsRow.transform, "MIN QUOTA", "3 Orders", ColorNavy, fontAsset);
            minQuotaValueText = p1;
            var p2 = CreateStatPill("Pill2", pillsRow.transform, "DAILY BILLS", "€ 25.00", new Color32(0xE1, 0x1D, 0x48, 0xFF), fontAsset);
            dailyBillsValueText = p2;
            var p3 = CreateStatPill("Pill3", pillsRow.transform, "OVERTIME", "+25% Tip", new Color32(0x05, 0x96, 0x69, 0xFF), fontAsset);
            overtimeValueText = p3;

            // Start Shift Button
            var startBtnGo = CreateRect("StartShiftButton", bGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            var sBtnImg = startBtnGo.AddComponent<Image>();
            sBtnImg.color = new Color32(0x34, 0xD3, 0x99, 0xFF);
            var sBtnOut = startBtnGo.AddComponent<Outline>();
            sBtnOut.effectColor = ColorNavy;
            sBtnOut.effectDistance = new Vector2(2, -2);
            var sBtnShadow = startBtnGo.AddComponent<Shadow>();
            sBtnShadow.effectColor = new Color32(0x05, 0x96, 0x69, 0xFF);
            sBtnShadow.effectDistance = new Vector2(0, -4);
            startShiftButton = startBtnGo.AddComponent<Button>();
            CreateText("BtnText", startBtnGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "START SHIFT & CLOCK IN →", 14, FontStyles.Bold, ColorNavy, fontAsset, TextAlignmentOptions.Center);

            // 2. ACTIVE CONTAINER
            var aGo = CreateRect("ActiveContainer", transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(-36, 184));
            activeContainer = aGo;

            CreateText("Kicker", aGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14), "DELIVERIES COMPLETED", 10, FontStyles.Bold, new Color32(0xEA, 0x58, 0x0C, 0xFF), fontAsset, TextAlignmentOptions.Left);
            var aHero = CreateText("ProgressHeroText", aGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30), "0 / 3 ORDERS", 22, FontStyles.Bold, ColorNavy, fontAsset, TextAlignmentOptions.Left);
            activeProgressHeroText = aHero.GetComponent<TMP_Text>();

            // Shift pay badge
            var payBadge = CreateRect("ShiftPayBadge", aGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(110, 36));
            var pbImg = payBadge.AddComponent<Image>();
            pbImg.color = ColorMintBg;
            var pbOut = payBadge.AddComponent<Outline>();
            pbOut.effectColor = ColorMintText;
            pbOut.effectDistance = new Vector2(1.5f, -1.5f);
            var payTextGo = CreateText("PayText", payBadge.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "+ € 0.00", 12, FontStyles.Bold, ColorMintText, fontAsset, TextAlignmentOptions.Center);
            activeEarningsText = payTextGo.GetComponent<TMP_Text>();

            // Progress Bar Track
            var trackGo = CreateRect("ProgressBar", aGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 14));
            var trkImg = trackGo.AddComponent<Image>();
            trkImg.color = ColorSlateBg;
            var trkOut = trackGo.AddComponent<Outline>();
            trkOut.effectColor = ColorNavy;
            trkOut.effectDistance = new Vector2(1.5f, -1.5f);

            var fillGo = CreateRect("Fill", trackGo.transform, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            progressBarFill = fillGo.AddComponent<Image>();
            progressBarFill.color = ColorGreenFill;
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillAmount = 0f;

            // Note Text
            var noteGo = CreateText("StatusNoteText", aGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(0, 18), "3 more orders required today", 11, FontStyles.Normal, ColorSlateText, fontAsset, TextAlignmentOptions.Left);
            activeStatusNoteText = noteGo.GetComponent<TMP_Text>();

            // Overtime callout
            var otBox = CreateRect("OvertimeCallout", aGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(0, 24));
            overtimeCalloutPanel = otBox;
            var otImg = otBox.AddComponent<Image>();
            otImg.color = ColorAmberBg;
            var otOut = otBox.AddComponent<Outline>();
            otOut.effectColor = new Color32(0xF5, 0x9E, 0x0B, 0xFF);
            otOut.effectDistance = new Vector2(1, -1);
            CreateText("OtText", otBox.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "🔥 Quota met! Extra deliveries earn +25% Speed Tip!", 10, FontStyles.Bold, ColorAmberText, fontAsset, TextAlignmentOptions.Center);
            otBox.SetActive(false);

            // Clock Out Button (Figma style: soft red with dark red outline/text)
            var clockOutBtnGo = CreateRect("ClockOutButton", aGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            var cBtnImg = clockOutBtnGo.AddComponent<Image>();
            cBtnImg.color = new Color32(0xFE, 0xE2, 0xE2, 0xFF);
            var cBtnOut = clockOutBtnGo.AddComponent<Outline>();
            cBtnOut.effectColor = new Color32(0xB9, 0x1C, 0x1C, 0xFF);
            cBtnOut.effectDistance = new Vector2(2, -2);
            var cBtnShadow = clockOutBtnGo.AddComponent<Shadow>();
            cBtnShadow.effectColor = new Color32(0x99, 0x1B, 0x1B, 0xFF);
            cBtnShadow.effectDistance = new Vector2(0, -4);
            clockOutButton = clockOutBtnGo.AddComponent<Button>();
            CreateText("BtnText", clockOutBtnGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "CLOCK OUT 🏁", 14, FontStyles.Bold, new Color32(0x99, 0x1B, 0x1B, 0xFF), fontAsset, TextAlignmentOptions.Center);

            // 3. SUMMARY CONTAINER
            var sGo = CreateRect("SummaryContainer", transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(-36, 184));
            summaryContainer = sGo;

            CreateText("Kicker", sGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 14), "DAY SETTLEMENT", 10, FontStyles.Bold, new Color32(0x63, 0x66, 0xF1, 0xFF), fontAsset, TextAlignmentOptions.Left);
            var sHero = CreateText("NetHeroText", sGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(0, 30), "+ € 0.00 NET", 22, FontStyles.Bold, ColorNavy, fontAsset, TextAlignmentOptions.Left);
            summaryNetHeroText = sHero.GetComponent<TMP_Text>();

            // Receipt Box
            var recBox = CreateRect("ReceiptBox", sGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(0, 58));
            var recImg = recBox.AddComponent<Image>();
            recImg.color = ColorSlateBg;
            var recOut = recBox.AddComponent<Outline>();
            recOut.effectColor = new Color32(0xCB, 0xD5, 0xE1, 0xFF);
            recOut.effectDistance = new Vector2(1, -1);

            var r1Go = CreateReceiptRow("Line1", recBox.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), "Orders Delivered:", "0 / 3", fontAsset);
            summaryDeliveredText = r1Go;
            var r2Go = CreateReceiptRow("Line2", recBox.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), "Shift Gross Pay:", "+ € 0.00", fontAsset);
            summaryGrossText = r2Go;
            var r3Go = CreateReceiptRow("Line3", recBox.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), "Daily Living Bills:", "- € 25.00", fontAsset);
            summaryBillsText = r3Go;

            // Advance Day Button
            var advBtnGo = CreateRect("AdvanceDayButton", sGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 46));
            var aBtnImg = advBtnGo.AddComponent<Image>();
            aBtnImg.color = new Color32(0x63, 0x66, 0xF1, 0xFF); // #6366F1
            var aBtnOut = advBtnGo.AddComponent<Outline>();
            aBtnOut.effectColor = ColorNavy;
            aBtnOut.effectDistance = new Vector2(2, -2);
            var aBtnShadow = advBtnGo.AddComponent<Shadow>();
            aBtnShadow.effectColor = new Color32(0x43, 0x38, 0xCA, 0xFF);
            aBtnShadow.effectDistance = new Vector2(0, -4);
            advanceDayButton = advBtnGo.AddComponent<Button>();
            CreateText("BtnText", advBtnGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "START NEXT DAY 🌙 →", 14, FontStyles.Bold, Color.white, fontAsset, TextAlignmentOptions.Center);

            // Bind click events
            BindButtons();
        }

        private GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private GameObject CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, string text, float fontSize, FontStyles style, Color color, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var go = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            return go;
        }

        private TMP_Text CreateStatPill(string name, Transform parent, string label, string val, Color valColor, TMP_FontAsset font)
        {
            var pillGo = new GameObject(name, typeof(RectTransform));
            pillGo.transform.SetParent(parent, false);
            var img = pillGo.AddComponent<Image>();
            img.color = new Color32(0xF8, 0xFA, 0xFC, 0xFF);
            var outl = pillGo.AddComponent<Outline>();
            outl.effectColor = new Color32(0xCB, 0xD5, 0xE1, 0xFF);
            outl.effectDistance = new Vector2(1, -1);

            CreateText("Label", pillGo.transform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(-8, 14), label, 9, FontStyles.Bold, ColorSlateText, font, TextAlignmentOptions.Center);
            var valGo = CreateText("ValueText", pillGo.transform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(-8, 18), val, 12, FontStyles.Bold, valColor, font, TextAlignmentOptions.Center);
            return valGo.GetComponent<TMP_Text>();
        }

        private TMP_Text CreateReceiptRow(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, string label, string initialVal, TMP_FontAsset font)
        {
            var row = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 1), pos, new Vector2(-20, 16));
            CreateText("Label", row.transform, new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, label, 10, FontStyles.Normal, ColorSlateText, font, TextAlignmentOptions.Left);
            var valGo = CreateText("Value", row.transform, new Vector2(0.6f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero, initialVal, 10, FontStyles.Bold, ColorNavy, font, TextAlignmentOptions.Right);
            return valGo.GetComponent<TMP_Text>();
        }
    }
}
