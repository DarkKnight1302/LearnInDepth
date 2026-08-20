using LearnInDepth.Models;

namespace LearnInDepth.Services.Prompts
{
    public static class QuizPromptBuilder
    {
        public const string SystemPrompt = """
            You are an expert assessment designer for software engineering interviews.
            You write multiple-choice questions that test DEEP understanding - never trivia, never definition recall alone.
            A learner who aces your quiz genuinely understands the material and can handle interview questions on it.
            You always respond with valid JSON only - no markdown fences, no commentary.
            """;

        public static string BuildUserPrompt(string topic, ChapterOutline chapter, string contentExcerpt)
        {
            string keyConcepts = string.Join(", ", chapter.KeyConcepts);
            string interviewFocus = string.Join("; ", chapter.InterviewFocus);

            return $$"""
                Create a quiz for a chapter of an in-depth course on "{{topic}}".

                Chapter {{chapter.Order}}: {{chapter.Title}}
                Chapter scope: {{chapter.Description}}
                Key concepts taught: {{keyConcepts}}
                Interview focus: {{interviewFocus}}

                Excerpt from the actual chapter content (grounding - quiz must match what was taught):
                {{contentExcerpt}}

                Requirements:
                - Exactly 10 multiple-choice questions, ordered from easier to harder.
                - Mix: ~4 conceptual understanding, ~3 scenario/application ("what happens if...", "which approach...", code-output reasoning), ~3 interview-style questions that are literally asked in real interviews.
                - Each question: exactly 4 options, exactly one correct. Options must be plausible - wrong options should represent real misconceptions.
                - "explanation": 1-3 sentences teaching why the correct answer is right and (briefly) why the tempting wrong one fails.
                - "difficulty": "Easy", "Medium" or "Hard".
                - "interviewStyle": true only for the interview-style questions.
                - Never reference "the chapter" or "the text" in questions - each must stand alone.

                Respond with JSON matching exactly this shape:
                {
                  "questions": [
                    {
                      "question": "string",
                      "options": ["string", "string", "string", "string"],
                      "correctOptionIndex": 0,
                      "explanation": "string",
                      "difficulty": "Medium",
                      "interviewStyle": false
                    }
                  ]
                }
                """;
        }
    }
}
