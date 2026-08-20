using LearnInDepth.Models;

namespace LearnInDepth.Services.Prompts
{
    public static class VerificationPromptBuilder
    {
        public const string SystemPrompt = """
            You are a strict but encouraging staff-engineer interviewer reviewing a learner's assignment submission.
            You grade against the rubric precisely, give actionable corrections with concrete fixes, and connect feedback to how this would play in a real interview.
            You never invent problems that are not there, and you never let a wrong or shallow solution pass.
            You always respond with valid JSON only - no markdown fences, no commentary.
            """;

        public static string BuildUserPrompt(string topic, ChapterOutline chapter, ChapterAssignment assignment, string solution)
        {
            string tasks = string.Join("\n", assignment.Tasks.Select((t, i) => $"  {i + 1}. {t}"));
            string rubric = string.Join("\n", assignment.EvaluationRubric.Select(r => $"  - {r}"));

            return $$"""
                Review this assignment submission for a course on "{{topic}}".

                Chapter {{chapter.Order}}: {{chapter.Title}}
                Assignment: {{assignment.Title}}
                Problem: {{assignment.ProblemStatement}}
                Tasks:
                {{tasks}}

                Evaluation rubric (grade each criterion):
                {{rubric}}

                Expected outcome: {{assignment.ExpectedOutcome}}

                LEARNER'S SUBMISSION:
                ---
                {{solution}}
                ---

                Grading instructions:
                - Check every rubric criterion against the submission. Be precise - partial credit where partially met.
                - "verdict": exactly one of "Pass" (all or nearly all criteria met, interview-ready), "NeedsWork" (right direction, real gaps), "Fail" (fundamentally wrong or missing the point).
                - "score": integer 0-100 reflecting rubric satisfaction.
                - "whatWentWell": array of 1-5 specific strengths (empty if none).
                - "corrections": array of concrete fixes. Each entry: what is wrong/missing AND exactly how to fix it, including corrected code or phrasing where applicable. Order by importance.
                - "interviewTips": 2-4 sentences on how this topic comes up in interviews and how the learner's current level would be perceived, plus what to say instead.

                Respond with JSON matching exactly this shape:
                {
                  "verdict": "Pass | NeedsWork | Fail",
                  "score": 0,
                  "whatWentWell": ["string"],
                  "corrections": ["string"],
                  "interviewTips": "string"
                }
                """;
        }
    }
}
