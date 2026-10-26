using System;
using System.Collections.Generic;

namespace Karmaan.Core
{
    /// <summary>
    /// The lesson state machine. Presentation-agnostic: it knows nothing about AR, cameras
    /// or UI. The scene layer feeds it <see cref="StepOutcome"/> values and listens to events.
    /// This is what makes the gyro 3D fallback possible with identical rules.
    /// </summary>
    public sealed class LessonRunner
    {
        public const int MaxFailsPerStep = 3;

        private readonly List<StepResult> _results = new List<StepResult>();
        private readonly List<int> _failCounts = new List<int>();
        private float _elapsedSec;

        public LessonDef Lesson { get; }
        public RunnerState State { get; private set; } = RunnerState.Idle;
        public int CurrentStepIndex { get; private set; } = -1;
        public ScoreReport Report { get; private set; }

        public event Action<LessonDef> Started;

        /// <summary>
        /// Fires when a step becomes the active one. Ordering contract: <see cref="FeedbackShown"/>
        /// always fires before the next <see cref="StepPresented"/>, both within the same Submit call.
        /// A presenter must queue the incoming step until the worker dismisses the feedback,
        /// otherwise the correction flashes past and the worker never reads it.
        /// </summary>
        public event Action<StepDef, int> StepPresented;

        public event Action<StepFeedback> FeedbackShown;
        public event Action<ScoreReport> Finished;
        public event Action Locked;

        public LessonRunner(LessonDef lesson)
        {
            if (lesson == null) throw new ArgumentNullException(nameof(lesson));
            if (lesson.steps == null || lesson.steps.Count == 0)
                throw new ArgumentException("Lesson '" + lesson.lessonId + "' has no steps.", nameof(lesson));

            Lesson = lesson;

            for (var i = 0; i < lesson.steps.Count; i++)
            {
                _failCounts.Add(0);
                _results.Add(new StepResult { stepId = lesson.steps[i].id, correct = false, attempts = 0 });
            }
        }

        public StepDef CurrentStep
        {
            get
            {
                if (StepsComplete) return null;
                if (CurrentStepIndex < 0 || CurrentStepIndex >= Lesson.steps.Count) return null;
                return Lesson.steps[CurrentStepIndex];
            }
        }

        public float ElapsedSec => _elapsedSec;

        public IReadOnlyList<StepResult> Results => _results;

        public void Start()
        {
            if (State != RunnerState.Idle) return;

            State = RunnerState.Intro;
            CurrentStepIndex = 0;
            _elapsedSec = 0f;
            Started?.Invoke(Lesson);
            PresentCurrentStep();
        }

        /// <summary>Called every frame by the scene layer while a lesson is running.</summary>
        public void Tick(float deltaTime)
        {
            if (State == RunnerState.AwaitingStep || State == RunnerState.Feedback)
            {
                _elapsedSec += deltaTime;
            }
        }

        /// <summary>
        /// Report what the worker did. Safe to call only while awaiting a step or in feedback.
        /// </summary>
        public StepFeedback Submit(StepOutcome outcome)
        {
            if (State != RunnerState.AwaitingStep && State != RunnerState.Feedback)
            {
                return new StepFeedback { correct = false, hintKey = null, locked = State == RunnerState.Locked };
            }

            var step = CurrentStep;
            if (step == null) return new StepFeedback();

            var result = _results[CurrentStepIndex];
            result.attempts++;
            result.chosenOptionId = outcome.chosenOptionId;
            result.secondsTaken += outcome.secondsTaken;

            if (outcome.correct)
            {
                result.correct = true;
                result.guardrailId = null;
                State = RunnerState.AwaitingStep;

                var feedback = new StepFeedback { correct = true, hintKey = null, locked = false };
                FeedbackShown?.Invoke(feedback);
                Advance();
                return feedback;
            }

            // Wrong. Count it, and decide whether this locks the lesson.
            result.correct = false;
            result.guardrailId = outcome.guardrailId;
            _failCounts[CurrentStepIndex]++;

            var willLock = _failCounts[CurrentStepIndex] >= MaxFailsPerStep;

            // A guardrail always wins the message: it names the specific lethal mistake.
            var hintKey = GuardrailRules.IsGuardrail(outcome.guardrailId)
                ? GuardrailRules.Get(outcome.guardrailId).correctionKey
                : step.failHintKey;

            var fb = new StepFeedback
            {
                correct = false,
                hintKey = hintKey,
                guardrailId = outcome.guardrailId,
                locked = willLock
            };

            if (willLock)
            {
                State = RunnerState.Locked;
                FeedbackShown?.Invoke(fb);
                Locked?.Invoke();
            }
            else
            {
                State = RunnerState.Feedback;
                FeedbackShown?.Invoke(fb);
            }

            return fb;
        }

        /// <summary>Trainer entered the PIN. Reopens the failed step with a cleared strike count.</summary>
        public void UnlockByTrainer()
        {
            if (State != RunnerState.Locked) return;

            _failCounts[CurrentStepIndex] = 0;
            State = RunnerState.AwaitingStep;
            PresentCurrentStep();
        }

        /// <summary>Call when the last step is done and the quiz has been taken.</summary>
        public ScoreReport Finish(int quizCorrect, int quizTotal, bool cleanPpe)
        {
            Report = ScoreEngine.Evaluate(Lesson, _results, quizCorrect, quizTotal, cleanPpe, _elapsedSec);
            State = RunnerState.Finished;
            Finished?.Invoke(Report);
            return Report;
        }

        private void Advance()
        {
            CurrentStepIndex++;

            if (CurrentStepIndex >= Lesson.steps.Count)
            {
                StepsComplete = true;
                CurrentStepIndex = Lesson.steps.Count - 1;
                State = RunnerState.AwaitingStep;
                return;
            }

            PresentCurrentStep();
        }

        private void PresentCurrentStep()
        {
            State = RunnerState.AwaitingStep;
            var step = CurrentStep;
            if (step == null) return;
            StepPresented?.Invoke(step, CurrentStepIndex);
        }
    }
}
