using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// GuideUIController.cs
// -------------------------------------------------------------
// Drives the Journal's "Guide" page. Two sides, both built the same
// way the Plants page builds its species grid from slotTemplate:
//
//   LEFT  — missionButtonTemplate is one real button already sitting
//           in missionListContainer. Its RectTransform is the
//           reference for every cloned button's size + starting
//           position; clicking a button shows that mission on the
//           right.
//   RIGHT — taskRowTemplate is one real checklist row already sitting
//           in taskListContainer, same deal. Populated whenever a
//           mission button is clicked.
//
// Neither template needs its size or position touched in code ever
// again — move/resize it once in the Editor and every clone follows.
//
// This controller only manages the Guide page's own two lists. Which
// page is currently visible (Plants/Progress/Guide/Settings) is
// still whatever owns that nav bar (e.g. JournalUIController) —
// just SetActive this page's GameObject the same way the others are
// toggled; OnEnable here repopulates the left list every time.
//
// SETUP:
//   1. Build the left button and right row once in the Editor,
//      each with their UI component (GuideMissionButtonUI /
//      GuideTaskRowUI) attached, and both parented under a
//      top-left-anchored (anchor/pivot 0,1) container.
//   2. Drag those two into missionButtonTemplate / taskRowTemplate.
//   3. Drag missionListContainer / taskListContainer (their parents).
//   4. Assign a MissionDatabase asset.
//   5. Assign checklistPanel (the whole right side) — it starts
//      inactive, same as the Plants page's detail panel, until a
//      mission is clicked.
// =============================================================
public class GuideUIController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Missions shown on the Guide bookmark.")]
    [SerializeField] private MissionDatabase database;
    [Tooltip("Lore entries for the Lore bookmark. Only entries the player has picked up are listed.")]
    [SerializeField] private MissionDatabase loreDatabase;
    [Tooltip("Auto-found via MissionProgressManager.Instance if left empty.")]
    [SerializeField] private MissionProgressManager progressManager;

    [Header("Testing")]
    [Tooltip("TESTING: list every Lore entry on the Lore bookmark, collected or not. Turn off for real play.")]
    [SerializeField] private bool debugShowAllLore = false;
    [Tooltip("TESTING: show every mission task from the start, ignoring task groups. Turn off for real play.")]
    [SerializeField] private bool debugRevealAllTasks = false;

    [Header("Guide / Lore Bookmarks")]
    [Tooltip("Page title art shown while the Guide bookmark is open.")]
    [SerializeField] private GameObject guideTitle;
    [Tooltip("Page title art shown while the Lore bookmark is open.")]
    [SerializeField] private GameObject loreTitle;
    [Tooltip("Optional. Shown instead of the right page while the Lore list is still empty.")]
    [SerializeField] private GameObject emptyLoreState;
    [Tooltip("How a Lore entry's body is laid out. {0} = where it was found, {1} = the entry text.")]
    [SerializeField] private string loreBodyFormat = "{0}\n\n{1}";

    [Header("Left Side — Mission List")]
    [Tooltip("Parent container for the mission buttons. Anchor/pivot must be top-left (0,1).")]
    [SerializeField] private RectTransform missionListContainer;
    [Tooltip("An existing button already placed in missionListContainer, with a GuideMissionButtonUI " +
             "component. Hidden at startup and cloned once per mission — its RectTransform is also the " +
             "size/position reference every cloned button uses.")]
    [SerializeField] private GuideMissionButtonUI missionButtonTemplate;
    [SerializeField] private float buttonGapY = 8f;

    [Header("Right Side — Checklist")]
    [Tooltip("The whole right-page contents. Inactive by default — there's no separate empty state, " +
             "this is just hidden until a mission is clicked.")]
    [SerializeField] private GameObject checklistPanel;
    [SerializeField] private TMP_Text missionTitleText;
    [Tooltip("Shows 'Complete' or 'Incomplete' for whichever mission is open. The 'Status' label itself " +
             "is static text baked into the panel — only this value text needs a reference.")]
    [SerializeField] private TMP_Text statusValueText;
    [Tooltip("Parent container for the checklist rows. Anchor/pivot must be top-left (0,1).")]
    [SerializeField] private RectTransform taskListContainer;
    [Tooltip("An existing checklist row already placed in taskListContainer, with a GuideTaskRowUI " +
             "component. Hidden at startup and cloned once per task.")]
    [SerializeField] private GuideTaskRowUI taskRowTemplate;
    [SerializeField] private float taskRowGapY = 8f;

    [Header("Right Side — Scroll + Text Sections")]
    [Tooltip("Optional ScrollRect wrapping the right page. Reset to the top whenever a different entry is opened.")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("The ScrollRect's Content object — must be anchored/pivoted TOP (pivot Y = 1). Its height is set " +
             "automatically to fit everything below. Put the opening text, task list container and completion " +
             "text (and ideally the title/status too, if they should scroll) inside it.")]
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("Text shown above the checklist (a mission's Opening Text) — also used as the body text of a Lore " +
             "entry. Anchor/pivot top-left (0,1). Hidden when empty.")]
    [SerializeField] private TMP_Text openingText;
    [Tooltip("Text shown below the checklist once the mission's last task is finished. Anchor/pivot top-left " +
             "(0,1). Hidden until then, and when empty.")]
    [SerializeField] private TMP_Text completionText;
        [Tooltip("Optional decorative line shown only when there IS opening text (sits between it and the checklist).")]
    [SerializeField] private RectTransform openingDivider;
    [Tooltip("Optional decorative line shown only when there IS completion text (sits between the checklist and it).")]
    [SerializeField] private RectTransform completionDivider;
    [SerializeField] private float sectionGapY = 16f;
    [Tooltip("Logs how every text block was measured — turn on if the text and the lines under it overlap.")]
    [SerializeField] private bool debugLayout = false;
    [SerializeField] private float bottomPadding = 16f;

    [Header("Right Side — Status + Tracking")]
    [Tooltip("Optional. Parent of the whole 'Status: Complete' line (label + value). Hidden for Lore entries. " +
             "If left blank only the value text is hidden.")]
    [SerializeField] private GameObject statusRoot;
    [Tooltip("Track / Untrack button. Hidden for Lore entries, non-trackable missions and completed missions.")]
    [SerializeField] private Button trackButton;
    [SerializeField] private TMP_Text trackButtonLabel;
    [SerializeField] private string trackText = "Track";
    [SerializeField] private string untrackText = "Untrack";

    public enum GuideMode { Guide, Lore }
    private GuideMode mode = GuideMode.Guide;
    private MissionData pendingSelect;
    public GuideMode Mode => mode;

    private string shownMissionId;
    // Where each block sits in the editor — all flow positions are offsets from these, so the layout
    // you set up by hand is respected instead of being overwritten.
    private float contentBaseHeight, startTopOffset;
    private bool layoutCaptured;
    private RectTransform missionButtonTemplateRect;
    private RectTransform taskRowTemplateRect;

    private readonly List<GuideMissionButtonUI> spawnedButtons = new List<GuideMissionButtonUI>();
    private readonly List<GuideTaskRowUI> spawnedRows = new List<GuideTaskRowUI>();

    private MissionData selectedMission;
    private GuideMissionButtonUI selectedButton;

    void Awake()
    {
        // The surviving manager (Instance) always wins over a reference dragged in the Inspector — a
        // duplicate manager in the scene destroys itself, and a reference to it would never see progress.
        if (MissionProgressManager.Instance != null)
            progressManager = MissionProgressManager.Instance;
        else if (progressManager == null)
            progressManager = FindAnyObjectByType<MissionProgressManager>();

        if (missionButtonTemplate != null)
        {
            missionButtonTemplateRect = missionButtonTemplate.GetComponent<RectTransform>();
            missionButtonTemplate.gameObject.SetActive(false); // hide the live scene object used as a clone source
        }

        if (taskRowTemplate != null)
        {
            taskRowTemplateRect = taskRowTemplate.GetComponent<RectTransform>();
            taskRowTemplate.gameObject.SetActive(false);
        }

        if (checklistPanel != null)
            checklistPanel.SetActive(false);

        if (trackButton != null)
            trackButton.onClick.AddListener(OnTrackClicked);


        // The height you set up in the editor is the minimum — it only ever grows from there.
    }

    /// <summary>Switches between the Guide and Lore bookmarks (title art, list and right page).</summary>
    public void SetMode(GuideMode newMode)
    {
        bool changed = newMode != mode;
        mode = newMode;
        if (guideTitle != null) guideTitle.SetActive(mode == GuideMode.Guide);
        if (loreTitle != null) loreTitle.SetActive(mode == GuideMode.Lore);

        if (changed)
        {
            ClearSelection();
            if (isActiveAndEnabled) PopulateMissionList();
        }
    }

    /// <summary>Switches to the Lore list and opens this entry (works before or after the page is active).</summary>
    public void OpenLoreEntry(MissionData entry)
    {
        pendingSelect = entry;
        SetMode(GuideMode.Lore);
        if (isActiveAndEnabled)
        {
            ClearSelection();
            PopulateMissionList();
        }
    }

    private void OnTrackClicked()
    {
        if (selectedMission == null || progressManager == null) return;
        progressManager.ToggleTracked(selectedMission); // fires OnProgressChanged -> everything refreshes
    }

    void OnEnable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged += HandleProgressChanged;

        SetMode(mode); // re-applies the right title art
        PopulateMissionList();
    }

    void OnDisable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged -= HandleProgressChanged;
    }

    private void HandleProgressChanged()
    {
        // Refresh whichever mission is currently open on the right, and the
        // left list's complete-indicators, without requiring a re-click.
        PopulateMissionList();
        if (selectedMission != null)
            ShowMissionDetail(selectedMission);
    }

    // ------------------------------------------------------------
    // LEFT — mission list
    // ------------------------------------------------------------
    private void PopulateMissionList()
    {
        MissionDatabase activeDb = mode == GuideMode.Lore ? loreDatabase : database;
        if (activeDb == null || missionButtonTemplate == null)
        {
            Debug.LogWarning("[GuideUIController] Missing database or mission button template — check the Inspector.");
            return;
        }

        foreach (GuideMissionButtonUI old in spawnedButtons)
            if (old != null) Destroy(old.gameObject);
        spawnedButtons.Clear();

        // Tracks the very first real (non-null) mission/button pair, purely so a fresh page-open with
        // nothing selected yet can auto-open it below — separate from selectedMission, which this loop
        // otherwise only ever re-applies the highlight for, never changes.
        MissionData firstMission = null;
        GuideMissionButtonUI firstButton = null;

        MissionData pendingMission = null;
        GuideMissionButtonUI pendingButton = null;
        int listIndex = 0;

        for (int i = 0; i < activeDb.missions.Count; i++)
        {
            MissionData mission = activeDb.missions[i];
            if (mission == null) continue;
            if (mode == GuideMode.Lore && !debugShowAllLore && (progressManager == null || !progressManager.IsLoreCollected(mission))) continue;

            GuideMissionButtonUI button = Instantiate(missionButtonTemplate, missionListContainer);
            button.gameObject.SetActive(true);

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = GuideVerticalLayout.GetPosition(missionButtonTemplateRect, listIndex++, buttonGapY);

            bool isComplete = !mission.IsLore && progressManager != null && progressManager.IsMissionComplete(mission);
            bool isTracked = progressManager != null && progressManager.IsTracked(mission);
            button.Initialize(mission, isComplete, m => OnMissionButtonClicked(m, button), isTracked);

            // Re-apply the persistent highlight if this clone happens to be the mission
            // that's currently open on the right (e.g. after a progress-triggered refresh).
            if (selectedMission != null && mission.ResolvedId == selectedMission.ResolvedId)
            {
                button.SetSelected(true);
                selectedButton = button;
            }

            if (pendingSelect != null && mission == pendingSelect)
            {
                pendingMission = mission;
                pendingButton = button;
            }

            if (firstMission == null)
            {
                firstMission = mission;
                firstButton = button;
            }

            spawnedButtons.Add(button);
        }

        // Auto-open the first mission the moment the page has something to show and nothing's
        // selected yet — a fresh journal-open, not a progress-triggered refresh (which always has
        // selectedMission already set by that point, so this never overrides an in-progress read).
        if (pendingMission != null)
        {
            OnMissionButtonClicked(pendingMission, pendingButton);
            pendingSelect = null;
        }
        else if (selectedMission == null && firstMission != null)
            OnMissionButtonClicked(firstMission, firstButton);

        // Nothing collected yet on the Lore bookmark: hide the right page, show the optional empty message.
        bool emptyLore = mode == GuideMode.Lore && firstMission == null;
        if (emptyLoreState != null) emptyLoreState.SetActive(emptyLore);
        if (emptyLore && checklistPanel != null) checklistPanel.SetActive(false);
    }

    // ------------------------------------------------------------
    // RIGHT — checklist for whichever mission was clicked
    // ------------------------------------------------------------
    private void OnMissionButtonClicked(MissionData mission, GuideMissionButtonUI button)
    {
        // Clear the previous button's highlight — stays applied otherwise, independent
        // of Unity's transient EventSystem Selected state, so it doesn't fall away just
        // because focus moves to a task row, a dropdown, or empty space.
        if (selectedButton != null)
            selectedButton.SetSelected(false);

        selectedButton = button;
        selectedButton?.SetSelected(true);

        ShowMissionDetail(mission);
    }

    public void ShowMissionDetail(MissionData mission)
    {
        if (mission == null) return;
        selectedMission = mission;

        bool lore = mission.IsLore;
        bool complete = !lore && progressManager != null && progressManager.IsMissionComplete(mission);

        if (checklistPanel != null) checklistPanel.SetActive(true);

        // The page was probably just switched on — let Unity finish laying it out so widths are real
        // before any text is measured.
        Canvas.ForceUpdateCanvases();

        // Capture the Content's editor height the first time the page is actually showing (Awake runs
        // before the layout is calculated, which gave a too-small number).
        if (contentRoot != null && !layoutCaptured)
        {
            Canvas.ForceUpdateCanvases();
            contentBaseHeight = contentRoot.rect.height;

            // Where the first block (the opening-text box) sits in the editor, measured down from the
            // top of the Content. Everything stacks downward from this point.
            Vector3[] cc = new Vector3[4];
            contentRoot.GetWorldCorners(cc);
            float capTop = cc[1].y;
            float capScale = Mathf.Max(0.0001f, Mathf.Abs(contentRoot.lossyScale.y));
            RectTransform first = openingText != null ? openingText.rectTransform : taskListContainer;
            if (first != null)
            {
                first.GetWorldCorners(cc);
                startTopOffset = (capTop - cc[1].y) / capScale;
            }
            layoutCaptured = true;
        }

        if (missionTitleText != null) missionTitleText.text = mission.missionTitle;

        if (statusRoot != null) statusRoot.SetActive(!lore);
        else if (statusValueText != null) statusValueText.gameObject.SetActive(!lore);
        if (statusValueText != null && !lore)
            statusValueText.text = complete ? "Complete" : "Incomplete";

        if (trackButton != null)
        {
            bool showTrack = !lore && mission.trackable && !complete && progressManager != null;
            trackButton.gameObject.SetActive(showTrack);
            if (showTrack && trackButtonLabel != null)
                trackButtonLabel.text = progressManager.IsTracked(mission) ? untrackText : trackText;
        }

        foreach (GuideTaskRowUI old in spawnedRows)
            if (old != null) Destroy(old.gameObject);
        spawnedRows.Clear();

        // ---- vertical flow: [opening / lore text] -> [checklist] -> [completion text] ----
        // Blocks are STACKED from the top: whatever isn't shown takes no space, so the checklist moves up
        // when there's no opening text, and the completion text sits right under the last row.
        bool showTasks = !lore && taskRowTemplate != null;
        if (taskListContainer != null)
            taskListContainer.gameObject.SetActive(showTasks);

        Vector3[] c = new Vector3[4];
        float scaleY = 1f, contentTop = 0f, cursorTop = 0f, lowest = 0f;
        bool canStack = contentRoot != null;

        if (canStack)
        {
            contentRoot.GetWorldCorners(c);
            contentTop = c[1].y;
            scaleY = Mathf.Max(0.0001f, Mathf.Abs(contentRoot.lossyScale.y));
            cursorTop = contentTop - startTopOffset * scaleY;
            lowest = cursorTop;
        }

        // 1) opening text (or the lore body)
        string topText = lore
            ? string.Format(loreBodyFormat, mission.loreSource, mission.loreText).Trim()
            : mission.openingText;
        float h = FitText(openingText, topText);
        if (h > 0f && canStack)
        {
            lowest = SetTopWorldY(openingText.rectTransform, cursorTop);
            cursorTop = lowest - sectionGapY * scaleY;
        }

        // 1b) divider under the opening text — only when that text is showing
        if (openingDivider != null) openingDivider.gameObject.SetActive(h > 0f);
        if (h > 0f && canStack && openingDivider != null)
        {
            lowest = SetTopWorldY(openingDivider, cursorTop);
            cursorTop = lowest - sectionGapY * scaleY;
        }

        // 2) checklist
        if (showTasks)
        {
            if (canStack && taskListContainer != null)
                SetTopWorldY(taskListContainer, cursorTop);

            int shown = 0; // only revealed tasks get a row; later groups stay hidden until earlier ones are done
            for (int i = 0; i < mission.tasks.Count; i++)
            {
                MissionTaskEntry task = mission.tasks[i];
                if (!debugRevealAllTasks && progressManager != null && !progressManager.IsTaskUnlocked(mission, task)) continue;

                GuideTaskRowUI row = Instantiate(taskRowTemplate, taskListContainer);
                row.gameObject.SetActive(true);

                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.anchoredPosition = GuideVerticalLayout.GetPosition(taskRowTemplateRect, shown, taskRowGapY);

                bool taskComplete = progressManager != null && progressManager.IsTaskComplete(mission, task);
                row.SetState(task.description, taskComplete);

                spawnedRows.Add(row);
                shown++;

                if (canStack)
                {
                    YRange(rowRect, out _, out float rowBottom);
                    lowest = Mathf.Min(lowest, rowBottom);
                }
            }

            if (canStack)
                cursorTop = lowest - sectionGapY * scaleY;
        }

        // 3) completion text — only once the mission is finished
        h = FitText(completionText, complete ? mission.completionText : null);
        if (completionDivider != null) completionDivider.gameObject.SetActive(h > 0f);
        if (h > 0f && canStack && completionDivider != null)
        {
            lowest = SetTopWorldY(completionDivider, cursorTop);
            cursorTop = lowest - sectionGapY * scaleY;
        }
        if (h > 0f && canStack)
        {
            lowest = SetTopWorldY(completionText.rectTransform, cursorTop);
        }

        if (debugLayout && canStack)
        {
            string P(RectTransform r)
            {
                if (r == null || !r.gameObject.activeSelf) return "hidden";
                YRange(r, out float yt, out float yb);
                return $"top={(contentTop - yt) / scaleY:F0} bottom={(contentTop - yb) / scaleY:F0}";
            }
            Debug.Log($"[GuideUI] layout (units below Content top) | opening {P(openingText != null ? openingText.rectTransform : null)} | " +
                      $"openingDivider {P(openingDivider)} | tasks {P(taskListContainer)} | " +
                      $"completionDivider {P(completionDivider)} | completion {P(completionText != null ? completionText.rectTransform : null)} | startTopOffset={startTopOffset:F0}");
        }

        // 4) size the Content to fit
        if (canStack)
        {
            float bottom = (contentTop - lowest) / scaleY;

            // At least as tall as the viewport (or the height you set in the editor), otherwise as tall as the content.
            float minHeight = scrollRect != null && scrollRect.viewport != null
                ? scrollRect.viewport.rect.height
                : contentBaseHeight;
            float total = Mathf.Max(bottom + bottomPadding, minHeight);

            // Resize, then slide back so the TOP edge stays exactly where it was (works for any pivot/anchors).
            contentRoot.GetWorldCorners(c);
            float topBefore = c[1].y;
            contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
            contentRoot.GetWorldCorners(c);
            contentRoot.position += new Vector3(0f, topBefore - c[1].y, 0f);
        }

        // Back to the top only when a DIFFERENT entry was opened, not on a progress refresh of the same one.
        bool newlyOpened = shownMissionId != mission.ResolvedId;
        if (scrollRect != null && newlyOpened)
            scrollRect.verticalNormalizedPosition = 1f;
        shownMissionId = mission.ResolvedId;

        // Measuring on the same frame the page switches on can come out short, so lay it out once more
        // next frame, when everything has its real size.
        if (newlyOpened && isActiveAndEnabled)
            StartCoroutine(RelayoutNextFrame(mission));
    }

    private System.Collections.IEnumerator RelayoutNextFrame(MissionData mission)
    {
        yield return null;
        if (selectedMission == mission)
            ShowMissionDetail(mission);
    }

    // Shows/hides one text block and sets its height to fit its text. Returns that height, or 0 if hidden/empty.
    private float FitText(TMP_Text label, string content)
    {
        if (label == null) return 0f;

        bool has = !string.IsNullOrWhiteSpace(content);
        label.gameObject.SetActive(has);
        if (!has) return 0f;

        label.text = content;
        RectTransform rt = label.rectTransform;

        float width = rt.rect.width;
        if (width <= 1f && contentRoot != null) width = contentRoot.rect.width;
        // Height is passed as "unlimited" — passing 0 made TMP report the box's CURRENT height back.
        float height = label.GetPreferredValues(content, width, 100000f).y;
        float preferred = height;

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        // Double-check against what TMP actually lays out at the box's real width (the estimate above can
        // come out short, which made the text run into the divider/list below it).
        float rendered = height;
        if (label.isActiveAndEnabled)
        {
            try { label.ForceMeshUpdate(true, true); rendered = label.preferredHeight; }
            catch (System.Exception) { }
        }
        if (rendered > height + 0.5f) { /* the current width gives a taller layout than the estimate — trust it */ }
        if (rendered > height + 0.5f)
        {
            height = rendered;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        // Third opinion, straight from the generated glyph lines: top of the first line to bottom of the last.
        // Only once the label is really running (during the page's own OnEnable its TMP hasn't started yet,
        // and TMP throws if asked to build a mesh at that point — the next-frame pass covers that case).
        TMP_TextInfo info = null;
        if (label.isActiveAndEnabled)
        {
            try { info = label.GetTextInfo(content); }
            catch (System.Exception) { info = null; }
        }
        float lineSpan = -1f;
        if (info != null && info.lineCount > 0)
        {
            lineSpan = info.lineInfo[0].ascender - info.lineInfo[info.lineCount - 1].descender + label.margin.y + label.margin.w;
            if (lineSpan > 1f && Mathf.Abs(lineSpan - height) > 0.5f)
            {
                height = lineSpan;
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }

        if (debugLayout)
            Debug.Log($"[GuideUI] '{label.name}' infoLines={(info != null ? info.lineCount : -1)} lineSpan={lineSpan:F1} width={width:F1} preferred={preferred:F1} rendered={rendered:F1} " +
                      $"final={height:F1} rectH={rt.rect.height:F1} lines={label.textInfo.lineCount} " +
                      $"fontSize={label.fontSize} lossyScaleY={rt.lossyScale.y:F5} contentScaleY={(contentRoot != null ? contentRoot.lossyScale.y : 0f):F5} " +
                      $"wrap={label.textWrappingMode} autosize={label.enableAutoSizing}", label);
        return height;
    }

    // Highest and lowest world-space Y of a rect. Uses all four corners, so a rect that has been flipped
    // (negative Y scale / rotated 180°, common for mirrored divider ornaments) is measured correctly.
    private static void YRange(RectTransform rt, out float top, out float bottom)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        top = Mathf.Max(Mathf.Max(c[0].y, c[1].y), Mathf.Max(c[2].y, c[3].y));
        bottom = Mathf.Min(Mathf.Min(c[0].y, c[1].y), Mathf.Min(c[2].y, c[3].y));
    }

    // Moves rt vertically so its visual TOP edge sits at world-space Y (independent of pivot/anchors/flipping).
    // Returns the world-space Y of its visual BOTTOM edge afterwards, measured from the real rect.
    private static float SetTopWorldY(RectTransform rt, float worldTop)
    {
        YRange(rt, out float top, out _);
        rt.position += new Vector3(0f, worldTop - top, 0f);
        YRange(rt, out _, out float bottom);
        return bottom;
    }

    /// <summary>Hides the right-side checklist, e.g. when the Guide page itself closes.</summary>
    public void ClearSelection()
    {
        selectedMission = null;
        shownMissionId = null;
        if (selectedButton != null)
            selectedButton.SetSelected(false);
        selectedButton = null;
        if (checklistPanel != null) checklistPanel.SetActive(false);
    }
}

// =============================================================
// GuideVerticalLayout
// -------------------------------------------------------------
// Same idea as the Plants page's ManualSlotLayout, but for a single
// top-to-bottom column instead of a wrapping grid — every item uses
// the template's own size, stepping down by that height + a gap per
// index. Template RectTransform must be anchored/pivoted top-left (0,1).
// =============================================================
public static class GuideVerticalLayout
{
    public static Vector2 GetPosition(RectTransform template, int index, float gapY)
    {
        if (template == null) return Vector2.zero;

        float rowHeight = template.rect.height + gapY;
        return template.anchoredPosition + new Vector2(0f, -rowHeight * index);
    }
}