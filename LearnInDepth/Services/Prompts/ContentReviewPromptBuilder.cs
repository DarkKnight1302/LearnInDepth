using LearnInDepth.Models;

namespace LearnInDepth.Services.Prompts
{
    public static class ContentReviewPromptBuilder
    {
        public const string SystemPrompt = """
            You are a meticulous senior technical editor and subject-matter expert reviewing AI-generated learning chapter content.
            Your job is to find and FIX any mistakes, issues, or weaknesses before the content is shown to a learner.
            You are ruthlessly accurate: a learner must never be taught something wrong, misleading, or incomplete.
            You always output the COMPLETE corrected content as raw HTML only - no markdown, no commentary, no code fences.
            """;

        public static string BuildUserPrompt(string topic, ChapterOutline chapter, string generatedHtml)
        {
            string keyConcepts = string.Join(", ", chapter.KeyConcepts);
            string interviewFocus = string.Join("; ", chapter.InterviewFocus);

            return $$"""
                Review and correct the learning content for one chapter of an in-depth course on "{{topic}}".

                Chapter {{chapter.Order}}: {{chapter.Title}}
                What this chapter must cover: {{chapter.Description}}
                Key concepts it must teach: {{keyConcepts}}
                Interview readiness it must deliver: {{interviewFocus}}

                Below is the AI-generated HTML content for this chapter. Review it carefully and output the FULLY CORRECTED content.

                REVIEW AND FIX THESE ASPECTS:
                1. FACTUAL / TECHNICAL ACCURACY: Find any incorrect, outdated, or misleading statements, inaccurate details, or wrong code. Fix them precisely. Nothing a learner learns here may be wrong.
                2. COMPLETENESS: Ensure every key concept and interview focus area is actually taught. If something important is missing or glossed over, add a concise, correct explanation for it.
                3. CODE CORRECTNESS: Every code sample must compile/run correctly. Fix syntax errors, bugs, and logic mistakes. If code is illustrative pseudo-code, say so clearly.
                4. HTML WELL-FORMEDNESS: Fix broken/malformed markup, unclosed tags, invalid attributes, or malformed CSS/JS. The output must be a single self-contained <section class="lid-chapter">...</section> fragment that closes properly.
                5. INTERACTIVE ELEMENTS: Preserve and fix any SVG/canvas/JS interactive or animated elements. Keep ALL styling scoped under .lid-chapter and all JS in vanilla IIFEs with no external dependencies.
                6. READABILITY / WHITE SPACE: Ensure generous spacing for comfortable reading - comfortable paragraph spacing (about 1.25-1.6rem below paragraphs), relaxed line-height (1.7-1.8), ample padding inside boxes/callouts/code blocks, and clear breathing room above headings. Add subheadings or lists to break up any long walls of text.
                7. VISUAL LEARNING: Make sure EVERY major concept is illustrated with a dynamic visual (animated SVG, interactive canvas, sliders, step-by-step flows, etc.). If any important concept lacks a diagram or interactive element, ADD one that teaches it. Aim for at least 5-8 interactive/animated elements across the chapter, each paired with a short caption.
                8. INTERVIEW VALUE: Strengthen any "interview callout" and "common pitfalls" content so it genuinely prepares the learner for interviews.
                9. CONSISTENCY: Ensure no contradictions within the chapter and correct transitions into and out of the surrounding course.

                RULES:
                - Output the ENTIRE corrected chapter as a complete, self-contained HTML fragment (root: <section class="lid-chapter"> ... </section>).
                - Do not summarize or describe your changes - just return the corrected content.
                - Preserve the tone: deep, accurate, easy to grasp, not dumbed-down, not needlessly verbose.

                GENERATED CONTENT TO REVIEW AND CORRECT:
                ---
                {{generatedHtml}}
                ---
                """;
        }
    }
}
