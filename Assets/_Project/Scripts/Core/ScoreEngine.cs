using System.Collections.Generic;

namespace Karmaan.Core
{
    /// <summary>
    /// Turns a run of step results into a score and a pass/fail decision.
    /// Deliberately boring and auditable: no randomness, no model, no heuristics.
    /// A safety officer must be able to read this file and agree with the verdict.
    /// </summary>
    public static class ScoreEngine
    {
        public const int BaseScore = 100;
        public const int MissPenalty = 10;
        public const int QuizPenalty = 5;
        public const int CleanPpeBonus = 5;
        public const int MaxTimePenalty = 15;
        public const int SecondsPerPenaltyPoint = 5;

        /// <param name="steps">Every step the worker attempted, in presentation order.</param>
        /// <param name="quizCorrect">Number of quiz items answered correctly.</param>
        /// <param name="quizTotal">Number of quiz items presented.</param>
        /// <param name="cleanPpe">True when the PPE step had zero wrong picks.</param>
        public static ScoreReport Evaluate(
            LessonDef lesson,
            IList<StepResult> steps,
            int quizCorrect,
            int quizTotal,
            bool cleanPpe,
            float elapsedSec)
        {
            var report = new ScoreReport
            {
                lessonId = lesson != null ? lesson.lessonId : "unknown",
                quizCorrect = quizCorrect,
                quizTotal = quizTotal,
                elapsedSec = elapsedSec,
                ppeBonus = cleanPpe ? CleanPpeBonus : 0
            };

            var score = BaseScore;

            // One penalty per step that took more than one attempt, regardless of how many retries.
            for (var i = 0; i < steps.Count; i++)
            {
                var r = steps[i];
                if (r == null) continue;

                if (r.attempts > 1 || !r.correct)
                {
                    score -= MissPenalty;
                    report.missCount++;
                    report.weakSteps.Add(r.stepId);
                }
            }

            var quizWrong = quizTotal - quizCorrect;
            if (quizWrong > 0) score -= quizWrong * QuizPenalty;

            // Must-pass gates are evaluated from the lesson definition, not from the report.
            if (lesson != null)
            {
                for (var i = 0; i < lesson.steps.Count; i++)
                {
                    var def = lesson.steps[i];
                    if (!def.mustPass) continue;

                    var result = Find(steps, def.id);
                    var satisfied = result != null && result.correct;
                    if (!satisfied)
                    {
                        report.failedMustPassSteps.Add(def.id);
                        if (!report.weakSteps.Contains(def.id)) report.weakSteps.Add(def.id);
                    }
                }
            }

            report.timePenalty = ComputeTimePenalty(lesson, elapsedSec);
            score -= report.timePenalty;
            score += report.ppeBonus;

            if (score < 0) score = 0;
            if (score > 100) score = 100;

            report.score = score;

            var passLine = lesson != null ? lesson.passScore : 70;
            report.passed = score >= passLine && report.failedMustPassSteps.Count == 0;

            return report;
        }

        public static int ComputeTimePenalty(LessonDef lesson, float elapsedSec)
        {
            if (lesson == null || lesson.benchmarkSec <= 0f) return 0;

            var over = elapsedSec - lesson.benchmarkSec;
            if (over <= 0f) return 0;

            var points = (int)(over / SecondsPerPenaltyPoint);
            return points > MaxTimePenalty ? MaxTimePenalty : points;
        }

        private static StepResult Find(IList<StepResult> steps, string stepId)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].stepId == stepId) return steps[i];
            }
            return null;
        }
    }
}
