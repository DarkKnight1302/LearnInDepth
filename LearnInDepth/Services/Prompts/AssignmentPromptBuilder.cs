using LearnInDepth.Models;

namespace LearnInDepth.Services.Prompts
{
    public static class AssignmentPromptBuilder
    {
        public const string SystemPrompt = """
            You are a senior engineer who designs hands-on challenges that build real, interview-grade skills.
            Your assignments make learners apply knowledge, not recite it. They are practical, completable in 30-90 minutes, and unambiguous about what "done" means.
            You always respond with valid JSON only - no markdown fences, no commentary.
            """;

        public static string BuildUserPrompt(string topic, ChapterOutline chapter, string contentExcerpt)
        {
            string keyConcepts = string.Join(", ", chapter.KeyConcepts);
            string interviewFocus = string.Join("; ", chapter.InterviewFocus);

            return $$"""
                Design a hands-on assignment for a chapter of an in-depth course on "{{topic}}".

                Chapter {{chapter.Order}}: {{chapter.Title}}
                Chapter scope: {{chapter.Description}}
                Key concepts taught: {{keyConcepts}}
                Interview focus: {{interviewFocus}}

                Excerpt from the actual chapter content (grounding):
                {{contentExcerpt}}

                Requirements:
                - ONE coherent hands-on challenge that requires applying this chapter's concepts (build something, solve something, analyze something, or explain a design with justification).
                - Completable in 30-90 minutes with just a text editor / REPL / browser - no special infrastructure unless the topic itself is a tool.
                - The learner will paste their solution as text (code, design, or written analysis) - so the deliverable must be text-pastable.
                - "problemStatement": the scenario and goal, 2-4 sentences, concrete.
                - "tasks": 3-6 numbered, specific, verifiable tasks that together complete the challenge.
                - "hints": 1-3 gentle nudges that don't give away the answer.
                - "evaluationRubric": 4-6 specific, checkable criteria an AI reviewer will use to grade submissions. Each criterion must be objectively verifiable from the pasted solution.
                - "expectedOutcome": one paragraph describing what an excellent solution demonstrates.

                Respond with JSON matching exactly this shape:
                {
                  "title": "string",
                  "problemStatement": "string",
                  "tasks": ["string"],
                  "hints": ["string"],
                  "evaluationRubric": ["string"],
                  "expectedOutcome": "string"
                }
                """;
        }
    }
}
