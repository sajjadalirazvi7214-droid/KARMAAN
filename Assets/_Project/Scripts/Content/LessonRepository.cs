using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suraksha.Core;
using UnityEngine;

namespace Suraksha.Content
{
    /// <summary>
    /// Loads lesson packs and refuses to run a lesson that is internally inconsistent.
    ///
    /// This validator exists because the content is safety-critical. A typo in a lesson file
    /// can turn a life-saving rule into a lethal one. Better to fail loudly at load than to
    /// quietly teach a worker to pour water on a wire fire.
    /// </summary>
    public static class LessonRepository
    {
        public const string ResourceFolder = "Lessons/";

        public static LessonDef Load(string lessonId)
        {
            if (string.IsNullOrEmpty(lessonId)) throw new ArgumentException("lessonId is required", nameof(lessonId));

            var asset = Resources.Load<TextAsset>(ResourceFolder + lessonId);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Lesson pack not found at Resources/" + ResourceFolder + lessonId + ".json");
            }

            LessonDef def;
            try
            {
                def = JsonConvert.DeserializeObject<LessonDef>(asset.text);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Lesson '" + lessonId + "' failed to parse: " + e.Message, e);
            }

            Validate(def);
            return def;
        }

        public static void Validate(LessonDef def)
        {
            if (def == null) throw new InvalidOperationException("Lesson is null.");

            var errors = new List<string>();

            if (string.IsNullOrEmpty(def.lessonId)) errors.Add("lessonId is empty.");
            if (string.IsNullOrEmpty(def.titleKey)) errors.Add("titleKey is empty.");
            if (def.steps == null || def.steps.Count == 0) errors.Add("lesson has no steps.");

            if (def.steps != null)
            {
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                var mustPassCount = 0;

                for (var i = 0; i < def.steps.Count; i++)
                {
                    var step = def.steps[i];
                    var where = "step[" + i + "]";

                    if (step == null)
                    {
                        errors.Add(where + " is null.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(step.id)) errors.Add(where + " has no id.");
                    else if (!seenIds.Add(step.id)) errors.Add(where + " duplicates id '" + step.id + "'.");

                    if (string.IsNullOrEmpty(step.promptKey)) errors.Add(where + " has no promptKey.");
                    if (step.mustPass) mustPassCount++;

                    if (step.guardrailIds != null)
                    {
                        foreach (var g in step.guardrailIds)
                        {
                            if (!GuardrailRules.IsGuardrail(g))
                                errors.Add(where + " references unknown guardrail '" + g + "'.");
                        }
                    }

                    var needsOptions = step.kind == StepKind.Choose
                                       || step.kind == StepKind.Sequence
                                       || step.kind == StepKind.Locate;

                    var needsSingleAnswer = step.kind == StepKind.Choose || step.kind == StepKind.Locate;

                    if (needsOptions)
                    {
                        if (step.options == null || step.options.Count < 2)
                        {
                            errors.Add(where + " (" + step.kind + ") needs at least two options.");
                        }
                        else
                        {
                            var correct = 0;
                            var optionIds = new HashSet<string>(StringComparer.Ordinal);

                            foreach (var opt in step.options)
                            {
                                if (opt == null || string.IsNullOrEmpty(opt.id))
                                {
                                    errors.Add(where + " has an option with no id.");
                                    continue;
                                }

                                optionIds.Add(opt.id);
                                if (opt.isCorrect) correct++;
                                if (!string.IsNullOrEmpty(opt.guardrailId) && !GuardrailRules.IsGuardrail(opt.guardrailId))
                                    errors.Add(where + " option '" + opt.id + "' references unknown guardrail '" + opt.guardrailId + "'.");
                            }

                            if (needsSingleAnswer)
                            {
                                var hasMulti = step.requiredOptionIds != null && step.requiredOptionIds.Count > 0;
                                if (!hasMulti && correct == 0 && string.IsNullOrEmpty(step.correctOptionId))
                                    errors.Add(where + " has no correct option.");
                                if (!hasMulti && correct > 1)
                                    errors.Add(where + " has " + correct + " correct options; exactly one expected.");
                            }

                            if (step.requiredOptionIds != null && step.requiredOptionIds.Count > 0)
                            {
                                if (step.requiredOptionIds.Count < 2)
                                    errors.Add(where + " has a single requiredOptionId; use correctOptionId instead.");

                                foreach (var requiredId in step.requiredOptionIds)
                                {
                                    if (!optionIds.Contains(requiredId))
                                        errors.Add(where + " requires option '" + requiredId + "' which is not in options.");
                                }
                            }
                        }
                    }

                    // Every must-pass gate must be satisfiable. An unsatisfiable gate means
                    // nobody can ever earn a certificate for this lesson.
                    if (step.mustPass && needsOptions && string.IsNullOrEmpty(step.correctOptionId))
                    {
                        var hasCorrectFlag = false;
                        if (step.options != null)
                        {
                            foreach (var opt in step.options)
                            {
                                if (opt != null && opt.isCorrect) { hasCorrectFlag = true; break; }
                            }
                        }
                        if (!hasCorrectFlag) errors.Add(where + " is must-pass but has no correct answer.");
                    }
                }

                if (mustPassCount == 0)
                    errors.Add("lesson has no must-pass steps — it cannot refuse a certificate, which defeats the product.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Lesson '" + def.lessonId + "' failed validation:\n - " + string.Join("\n - ", errors));
            }
        }
    }
}
