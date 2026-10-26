using System;
using System.Collections.Generic;
using Suraksha.Bootstrap;
using Suraksha.Core;
using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// The bridge between the rules engine, the 3D room and the HUD.
    ///
    /// Everything here is presentation and input. It never decides whether an answer was right —
    /// it packages what the worker did into a <see cref="StepOutcome"/> and lets LessonRunner
    /// judge it. That separation is what lets the same lesson run in AR, in 3D fallback, or in a
    /// headless test.
    /// </summary>
    public sealed class LessonPresenter : MonoBehaviour
    {
        private const float LockedHoldSeconds = 3f;

        public event Action<ScoreReport, string> Completed;
        public event Action Abandoned;

        private GameRoot _root;
        private GameUI _ui;
        private LessonDef _lesson;
        private LessonRunner _runner;
        private BuiltRoom _room;
        private PlayerRig _rig;

        private StepDef _step;
        private float _stepTimer;
        private float _lockedTimer;
        private int _gesturePhase;
        private bool _cleanPpe = true;
        private bool _quizzing;
        private int _quizIndex;
        private int _quizCorrect;

        private readonly List<string> _sequence = new List<string>();
        private readonly HashSet<string> _picked = new HashSet<string>();
        private readonly Dictionary<string, NameTag> _tags = new Dictionary<string, NameTag>();

        public static LessonPresenter Create(GameRoot root, GameUI ui, LessonDef lesson)
        {
            var go = new GameObject("LessonPresenter");
            var presenter = go.AddComponent<LessonPresenter>();
            presenter._root = root;
            presenter._ui = ui;
            presenter._lesson = lesson;
            presenter.Begin();
            return presenter;
        }

        public void Begin()
        {
            _room = RoomBuilder.Build(_lesson.lessonId, transform);
            _rig = PlayerRig.Create(transform, _room.PlayerStart, _room.PlayerLookAt);

            BuildTags();

            _ui.ShowHud(_lesson.titleKey);

            _runner = new LessonRunner(_lesson);
            _runner.StepPresented += OnStepPresented;
            _runner.FeedbackShown += OnFeedback;
            _runner.Finished += OnFinished;
            _runner.Locked += OnLocked;
            _runner.Start();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ---------------------------------------------------------------- tags

        private void BuildTags()
        {
            var tagColor = new Color(1f, 0.96f, 0.78f);

            for (var i = 0; i < _room.All.Count; i++)
            {
                var item = _room.All[i];
                if (string.IsNullOrEmpty(item.labelKey)) continue;

                var text = _loc.GetOrEmpty(item.labelKey);
                if (string.IsNullOrEmpty(text)) continue;

                var tag = NameTag.Attach(item.gameObject, text, tagColor);
                if (tag == null) continue;

                tag.SetVisible(false);
                _tags[item.stepId + "|" + item.optionId] = tag;
            }
        }

        private void ShowTagsFor(string stepId)
        {
            var prefix = stepId + "|";
            foreach (var pair in _tags)
            {
                pair.Value.SetVisible(pair.Key.StartsWith(prefix, StringComparison.Ordinal));
            }
        }

        private LocalizationService _loc
        {
            get { return _root.Localization; }
        }

        // ---------------------------------------------------------------- events

        private void OnStepPresented(StepDef step, int index)
        {
            _step = step;
            _stepTimer = 0f;
            _gesturePhase = 0;
            _sequence.Clear();
            _picked.Clear();

            _ui.SetProgress(index, _lesson.steps.Count);
            _ui.SetPrompt(_loc.Get(step.promptKey));
            ShowTagsFor(step.id);

            _rig.ClearHover();
            _rig.Filter = target => target.stepId == step.id;

            if (step.kind == StepKind.Reaction) ToneBank.Alarm();
            if (step.kind == StepKind.Gesture) _ui.SetPrompt(_loc.GetOrEmpty("gesture.p1"));
        }

        private void OnFeedback(StepFeedback feedback)
        {
            // A retry always gets a fresh clock. Without this the reaction timer stays negative
            // and re-submits every frame until the worker is locked out.
            _stepTimer = 0f;

            if (feedback.correct)
            {
                ToneBank.Correct();
                _ui.SetHint(_loc.Get("ui.step.correct"), false);
                return;
            }

            ToneBank.Wrong();
            _sequence.Clear();
            _picked.Clear();
            _gesturePhase = 0;

            var hint = string.IsNullOrEmpty(feedback.hintKey)
                ? _loc.Get("ui.step.tryagain")
                : _loc.GetOrEmpty(feedback.hintKey);

            if (string.IsNullOrEmpty(hint)) hint = _loc.Get("ui.step.tryagain");
            _ui.SetHint(hint, true);
        }

        private void OnLocked()
        {
            _lockedTimer = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _ui.SetHint(_loc.Get("ui.trainer.locked"), true);
        }

        private void OnFinished(ScoreReport report)
        {
            var workerId = "JH-DEMO-" + (100 + _root.Attempts.Count);
            var token = _root.CompleteLesson(_lesson, report, workerId, "DEMO-CENTRE");

            if (report.passed) ToneBank.Pass();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var handler = Completed;
            if (handler != null) handler(report, token);
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            if (_runner == null || _quizzing) return;

            if (_runner.State == RunnerState.Locked)
            {
                _lockedTimer += Time.deltaTime;
                if (_lockedTimer > LockedHoldSeconds && InputBridge.TapDown)
                {
                    // Demo stand-in for the trainer PIN. A real build gates this behind a code.
                    Debug.LogWarning("[Presenter] Trainer override used after 3 failed attempts.");
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    _lockedTimer = 0f;
                    _runner.UnlockByTrainer();
                }
                return;
            }

            if (_runner.State != RunnerState.AwaitingStep && _runner.State != RunnerState.Feedback) return;

            _runner.Tick(Time.deltaTime);

            if (_step != null && _step.kind == StepKind.Reaction && _runner.State == RunnerState.AwaitingStep)
            {
                _stepTimer += Time.deltaTime;
                var remaining = _step.timeLimitSec - _stepTimer;

                if (remaining <= 0f)
                {
                    _ui.SetHint("⏱ 0", true);
                    Submit(StepOutcome.Wrong(null, null, _stepTimer));
                    return;
                }

                if (remaining < 4f) _ui.SetHint("⏱ " + Mathf.CeilToInt(remaining), true);
            }

            _ui.SetReticle(_rig != null && _rig.Hovered != null);

            if (!InputBridge.TapDown) return;
            if (_ui.IsOverUi) return;

            var hit = _rig != null ? _rig.Hovered : null;
            if (hit == null) return;

            HandleTap(hit);
        }

        private void HandleTap(Interactable hit)
        {
            if (_step == null) return;

            switch (_step.kind)
            {
                case StepKind.Reaction:
                    if (hit.stepId == _step.id) Submit(StepOutcome.Ok(_stepTimer));
                    break;

                case StepKind.Choose:
                    HandleChoose(hit);
                    break;

                case StepKind.Locate:
                    HandleChoose(hit);
                    break;

                case StepKind.Sequence:
                    HandleSequence(hit);
                    break;

                case StepKind.Gesture:
                    HandleGesture(hit);
                    break;
            }
        }

        private void HandleChoose(Interactable hit)
        {
            var option = FindOption(hit.optionId);
            if (option == null) return;

            // Multi-pick step (PPE): toggle until the required set is complete.
            if (_step.requiredOptionIds != null && _step.requiredOptionIds.Count > 0)
            {
                if (!option.isCorrect)
                {
                    _cleanPpe = false;
                    Submit(StepOutcome.Wrong(option.id, option.guardrailId, _stepTimer));
                    return;
                }

                _picked.Add(option.id);
                hit.SetSelected(true);

                var complete = true;
                for (var i = 0; i < _step.requiredOptionIds.Count; i++)
                {
                    if (!_picked.Contains(_step.requiredOptionIds[i])) { complete = false; break; }
                }

                if (complete) Submit(StepOutcome.Ok(_stepTimer));
                return;
            }

            if (option.isCorrect) Submit(StepOutcome.Ok(_stepTimer));
            else Submit(StepOutcome.Wrong(option.id, option.guardrailId, _stepTimer));
        }

        private void HandleSequence(Interactable hit)
        {
            if (hit.stepId != _step.id) return;

            var expectedIndex = _sequence.Count;
            if (expectedIndex >= _step.options.Count) return;

            var expected = _step.options[expectedIndex];

            if (hit.optionId != expected.id)
            {
                Submit(StepOutcome.Wrong(hit.optionId, null, _stepTimer));
                return;
            }

            _sequence.Add(hit.optionId);
            hit.Fade();

            if (_sequence.Count >= _step.options.Count) Submit(StepOutcome.Ok(_stepTimer));
        }

        private void HandleGesture(Interactable hit)
        {
            if (hit.optionId != "fire_base") return;

            _gesturePhase++;

            if (_gesturePhase >= 4)
            {
                Submit(StepOutcome.Ok(_stepTimer));
                return;
            }

            _ui.SetPrompt(_loc.GetOrEmpty("gesture.p" + (_gesturePhase + 1)));
            ToneBank.Click();
        }

        private OptionDef FindOption(string optionId)
        {
            if (_step == null || _step.options == null) return null;

            for (var i = 0; i < _step.options.Count; i++)
            {
                if (_step.options[i].id == optionId) return _step.options[i];
            }
            return null;
        }

        private void Submit(StepOutcome outcome)
        {
            _runner.Submit(outcome);

            if (_runner.StepsComplete && _runner.State != RunnerState.Locked)
            {
                BeginQuiz();
            }
        }

        // ---------------------------------------------------------------- quiz

        private void BeginQuiz()
        {
            if (_lesson.quiz == null || _lesson.quiz.Count == 0)
            {
                FinishLesson();
                return;
            }

            _quizzing = true;
            _quizIndex = 0;
            _quizCorrect = 0;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowQuizItem();
        }

        private void ShowQuizItem()
        {
            if (_quizIndex >= _lesson.quiz.Count)
            {
                FinishLesson();
                return;
            }

            var item = _lesson.quiz[_quizIndex];

            var options = new string[item.optionKeys.Count];
            for (var i = 0; i < options.Length; i++) options[i] = _loc.Get(item.optionKeys[i]);

            _ui.ShowQuiz(_loc.Get(item.promptKey), options, chosen =>
            {
                if (chosen == item.correctIndex) _quizCorrect++;
                else ToneBank.Wrong();

                _quizIndex++;
                ShowQuizItem();
            });
        }

        private void FinishLesson()
        {
            _quizzing = false;
            _runner.Finish(_quizCorrect, _lesson.quiz != null ? _lesson.quiz.Count : 0, _cleanPpe);
        }

        // ---------------------------------------------------------------- teardown

        public void Dispose()
        {
            if (_runner != null)
            {
                _runner.StepPresented -= OnStepPresented;
                _runner.FeedbackShown -= OnFeedback;
                _runner.Finished -= OnFinished;
                _runner.Locked -= OnLocked;
            }

            if (_room != null) _room.Destroy();
            if (_rig != null) _rig.Dispose();

            _tags.Clear();
            _room = null;
            _rig = null;
            _runner = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
