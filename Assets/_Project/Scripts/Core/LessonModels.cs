using System;
using System.Collections.Generic;

namespace Suraksha.Core
{
    /// <summary>How a worker is expected to answer a step.</summary>
    public enum StepKind
    {
        /// <summary>React to an event inside a short window (alarm, beep).</summary>
        Reaction = 0,
        /// <summary>Pick one option (which extinguisher, which PPE).</summary>
        Choose = 1,
        /// <summary>Perform actions in the correct order (muster, lock-tag-test).</summary>
        Sequence = 2,
        /// <summary>Perform a physical gesture (PASS sweep).</summary>
        Gesture = 3,
        /// <summary>Find and tap the correct place in the scene (exit, buddy, zone).</summary>
        Locate = 4
    }

    public enum RunnerState
    {
        Idle = 0,
        Intro = 1,
        AwaitingStep = 2,
        Feedback = 3,
        Locked = 4,
        Finished = 5
    }

    [Serializable]
    public class OptionDef
    {
        public string id;
        public string labelKey;
        public string voiceKey;
        public bool isCorrect;

        /// <summary>Guardrail armed by picking this option (e.g. "G1" for water on wire fire).</summary>
        public string guardrailId;
    }

    [Serializable]
    public class StepDef
    {
        public string id;
        public StepKind kind = StepKind.Choose;
        public bool mustPass;
        public float timeLimitSec = 20f;
        public string promptKey;
        public string voiceKey;
        public string failHintKey;
        public List<OptionDef> options = new List<OptionDef>();
        public string correctOptionId;

        /// <summary>
        /// For multi-select steps (e.g. "pick exactly three PPE"). When non-empty, every listed
        /// option must be selected and no option flagged isCorrect=false may be picked.
        /// </summary>
        public List<string> requiredOptionIds = new List<string>();

        public List<string> guardrailIds = new List<string>();
    }

    [Serializable]
    public class QuizItem
    {
        public string id;
        public string promptKey;
        public List<string> optionKeys = new List<string>();
        public int correctIndex;
    }

    [Serializable]
    public class LessonDef
    {
        public int schemaVersion = 1;
        public string lessonId;
        public int displayOrder;
        public string titleKey;
        public int passScore = 70;
        public float timeLimitSec = 300f;

        /// <summary>Elapsed time after which the score starts bleeding. 0 disables the time penalty.</summary>
        public float benchmarkSec;

        public List<StepDef> steps = new List<StepDef>();
        public List<QuizItem> quiz = new List<QuizItem>();

        public StepDef GetStep(string stepId)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].id == stepId) return steps[i];
            }
            return null;
        }
    }

    /// <summary>What the scene layer reports back after the worker acts.</summary>
    public struct StepOutcome
    {
        public bool correct;
        public string chosenOptionId;
        public string guardrailId;
        public float secondsTaken;

        public static StepOutcome Ok(float secondsTaken = 0f)
        {
            return new StepOutcome { correct = true, secondsTaken = secondsTaken };
        }

        public static StepOutcome Wrong(string chosenOptionId, string guardrailId, float secondsTaken = 0f)
        {
            return new StepOutcome
            {
                correct = false,
                chosenOptionId = chosenOptionId,
                guardrailId = guardrailId,
                secondsTaken = secondsTaken
            };
        }
    }

    [Serializable]
    public class StepResult
    {
        public string stepId;
        public bool correct;
        public int attempts = 1;
        public float secondsTaken;
        public string chosenOptionId;
        public string guardrailId;
    }

    /// <summary>What the UI shows after a step submission.</summary>
    public struct StepFeedback
    {
        public bool correct;
        public string hintKey;
        public string guardrailId;
        public bool locked;
    }

    public sealed class ScoreReport
    {
        public string lessonId;
        public int score;
        public bool passed;
        public int missCount;
        public int quizCorrect;
        public int quizTotal;
        public int timePenalty;
        public int ppeBonus;
        public float elapsedSec;
        public readonly List<string> failedMustPassSteps = new List<string>();
        public readonly List<string> weakSteps = new List<string>();

        public bool HasWeakSteps => weakSteps.Count > 0;

        public override string ToString()
        {
            return lessonId + " score=" + score + " passed=" + passed +
                   " misses=" + missCount + " mustPassFails=" + failedMustPassSteps.Count;
        }
    }
}
