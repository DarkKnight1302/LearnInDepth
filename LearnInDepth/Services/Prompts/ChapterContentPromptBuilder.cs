using LearnInDepth.Models;

namespace LearnInDepth.Services.Prompts
{
    public static class ChapterContentPromptBuilder
    {
        public const string SystemPrompt = """
            You are an elite technical educator and front-end craftsman. You write in-depth learning chapters as self-contained HTML fragments.
            Your teaching style: crystal-clear explanations, memorable analogies, interactive visuals, real code, and hard-won practical insights.
            You NEVER dumb things down - you make deep things easy to grasp. You are thorough but never verbose: every sentence teaches something.
            You output raw HTML only - no markdown, no commentary, no code fences.
            """;

        public static string BuildUserPrompt(string topic, LearningPlan plan, ChapterOutline chapter)
        {
            string keyConcepts = string.Join(", ", chapter.KeyConcepts);
            string interviewFocus = string.Join("; ", chapter.InterviewFocus);
            string chapterMap = string.Join("\n", plan.Chapters.Select(c =>
                $"  {c.Order}. {c.Title}" + (c.Order == chapter.Order ? "  <-- THIS CHAPTER" : string.Empty)));

            return $$"""
                Write the complete in-depth learning content for one chapter of a course on "{{topic}}".

                Course chapter map (for context and smooth transitions):
                {{chapterMap}}

                Chapter {{chapter.Order}}: {{chapter.Title}}
                What this chapter must cover: {{chapter.Description}}
                Key concepts to teach: {{keyConcepts}}
                Interview readiness this chapter must deliver: {{interviewFocus}}

                OUTPUT FORMAT - strict rules:
                - Output a single self-contained HTML fragment (a root <section class="lid-chapter"> ... </section>). No <html>, <head>, or <body> tags.
                - ALL CSS must be inline in ONE <style> block at the top of the fragment. Scope every CSS selector under .lid-chapter so it cannot leak into a host page.
                - ALL JavaScript must be in <script> blocks inside the fragment. Scripts must be plain vanilla JS, wrapped in an IIFE, and must not rely on any external library, network call, or global variable.
                - No external resources whatsoever: no CDN links, no images from URLs, no fonts to download. Build every visual with inline SVG, <canvas>, or pure CSS.

                TEACHING DESIGN - what makes this chapter great:
                - Start with a short hook: why this chapter matters and what the learner will be able to do after it.
                - Teach from first principles, then build up to the advanced details. Use concrete analogies to make abstract ideas click - but always map the analogy back to the real mechanism precisely.
                - Include MANY diagrams and moving elements: animated SVG walkthroughs, step-by-step visual flows the user can play/scrub with buttons, interactive <canvas> demos, before/after sliders, clickable reveal sections. At least 3 substantial interactive/animated elements.
                - Include real, runnable code examples in <pre><code> blocks with simple syntax-highlighting CSS. Where useful, make the code example interactive (e.g. a button that runs a simulation and shows output).
                - Sprinkle "Interview callout" boxes (styled distinctly) that name exact interview questions related to the concept and how to answer them like an expert.
                - Include a "Common pitfalls" section with the mistakes juniors make and why.
                - End with a compact "Cheat sheet" summary table or card grid of everything interview-critical in the chapter.
                - Depth without verbosity: prefer precise, information-dense writing. No filler, no repetition.

                Length target: a rich chapter a motivated learner finishes in 30-60 minutes.
                """;
        }
    }
}
