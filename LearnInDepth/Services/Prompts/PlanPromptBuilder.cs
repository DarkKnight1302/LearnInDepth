namespace LearnInDepth.Services.Prompts
{
    public static class PlanPromptBuilder
    {
        public const string SystemPrompt = """
            You are a world-class curriculum architect who designs in-depth learning plans for software engineers.
            Your plans take a learner from absolute basics to advanced mastery of a topic.
            The success criterion: after completing ALL chapters, the learner must be able to confidently answer ANY interview question on the topic, from junior to staff-engineer level.
            You design for depth and true understanding, not surface familiarity.
            You always respond with valid JSON only - no markdown fences, no commentary.
            """;

        public static string BuildUserPrompt(string topic)
        {
            return $$"""
                Create a chapter-wise in-depth learning plan for the topic: "{{topic}}"

                Requirements:
                - 6 to 12 chapters, ordered from absolute basics to advanced/expert level.
                - Each chapter builds on the previous ones - no knowledge gaps, no big jumps.
                - Coverage must be COMPLETE: a learner who masters all chapters must be able to clear any interview on "{{topic}}". Include the fundamentals, the internals/how-it-works-under-the-hood, real-world usage, common pitfalls, performance aspects, and the advanced scenarios interviewers love to probe.
                - Avoid unnecessary verbosity in scope - every chapter must earn its place.

                For EACH chapter provide:
                - "title": short, clear chapter title.
                - "description": 3-6 sentences describing exactly what the chapter must cover - specific enough that a separate AI can generate full in-depth chapter content from this description alone without further context.
                - "keyConcepts": array of 4-8 specific concepts/terms the chapter must teach.
                - "interviewFocus": array of 2-5 interview-critical angles this chapter must make the learner ready for (e.g. typical questions, whiteboard scenarios, design trade-offs).

                Respond with JSON matching exactly this shape:
                {
                  "chapters": [
                    {
                      "title": "string",
                      "description": "string",
                      "keyConcepts": ["string"],
                      "interviewFocus": ["string"]
                    }
                  ]
                }
                """;
        }
    }
}
