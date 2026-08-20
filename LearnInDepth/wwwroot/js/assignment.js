/* ==========================================================================
   LearnInDepth - Coding Assignment & Solution Studio (js/assignment.js)
   ========================================================================== */

const AssignmentController = (function() {
  const assignTitle = document.getElementById('assignTitle');
  const assignProblemText = document.getElementById('assignProblemText');
  const assignTasksList = document.getElementById('assignTasksList');
  const assignExpectedOutcome = document.getElementById('assignExpectedOutcome');
  const assignHintsList = document.getElementById('assignHintsList');

  const solutionTextarea = document.getElementById('solutionTextarea');
  const charCounter = document.getElementById('charCounter');
  const submitSolutionBtn = document.getElementById('submitSolutionBtn');
  const loadSampleCodeBtn = document.getElementById('loadSampleCodeBtn');
  const clearCodeBtn = document.getElementById('clearCodeBtn');
  const copyCodeBtn = document.getElementById('copyCodeBtn');

  // Feedback Modal Elements
  const feedbackModal = document.getElementById('feedbackModal');
  const feedbackModalClose = document.getElementById('feedbackModalClose');
  const closeFeedbackModalBtn = document.getElementById('closeFeedbackModalBtn');
  const feedbackScoreBadge = document.getElementById('feedbackScoreBadge');
  const feedbackVerdictChip = document.getElementById('feedbackVerdictChip');
  const feedbackWellList = document.getElementById('feedbackWellList');
  const feedbackCorrectionsList = document.getElementById('feedbackCorrectionsList');
  const feedbackTipsText = document.getElementById('feedbackTipsText');

  let currentSlug = '';
  let currentOrder = 1;
  let assignmentData = null;

  function init() {
    solutionTextarea.addEventListener('input', () => {
      const len = solutionTextarea.value.length;
      charCounter.innerText = `${len} / 50000 chars`;
    });

    loadSampleCodeBtn.addEventListener('click', loadSampleTemplate);
    clearCodeBtn.addEventListener('click', () => {
      solutionTextarea.value = '';
      charCounter.innerText = '0 / 50000 chars';
    });

    copyCodeBtn.addEventListener('click', () => {
      navigator.clipboard.writeText(solutionTextarea.value);
      App.showToast('Solution copied to clipboard!', 'info');
    });

    submitSolutionBtn.addEventListener('click', handleSubmit);

    feedbackModalClose.addEventListener('click', () => feedbackModal.classList.add('hidden'));
    closeFeedbackModalBtn.addEventListener('click', () => feedbackModal.classList.add('hidden'));
  }

  async function loadAssignment(slug, order) {
    currentSlug = slug;
    currentOrder = order;

    assignTitle.innerText = 'Loading Assignment...';
    assignProblemText.innerText = 'Fetching problem statement...';

    try {
      assignmentData = await ApiClient.getChapterAssignment(slug, order);
      renderAssignment();
    } catch (err) {
      assignProblemText.innerText = `Failed to load assignment: ${err.message}`;
    }
  }

  function renderAssignment() {
    if (!assignmentData) return;

    assignTitle.innerText = assignmentData.title || `Chapter ${currentOrder} Assignment`;
    assignProblemText.innerText = assignmentData.problemStatement || '';

    assignTasksList.innerHTML = (assignmentData.tasks || []).map(t => `<li>${escapeHtml(t)}</li>`).join('');
    assignExpectedOutcome.innerText = assignmentData.expectedOutcome || '';
    assignHintsList.innerHTML = (assignmentData.hints || []).map(h => `<li>${escapeHtml(h)}</li>`).join('');
  }

  function loadSampleTemplate() {
    const template = `// LearnInDepth - Solution Implementation Studio
// Topic: ${currentSlug} (Chapter ${currentOrder})

public class SolutionEngine {
    public static void Main(string[] args) {
        // 1. Initialize data structures & thread pool
        System.Console.WriteLine("Executing resilient solution strategy...");

        // 2. Core business logic execution
        ExecuteTaskPipeline();
    }

    private static void ExecuteTaskPipeline() {
        // Implement task steps here
    }
}`;
    solutionTextarea.value = template;
    charCounter.innerText = `${template.length} / 50000 chars`;
  }

  async function handleSubmit() {
    const solution = solutionTextarea.value.trim();
    if (!solution) {
      App.showToast('Please write your solution code before submitting', 'error');
      return;
    }

    submitSolutionBtn.disabled = true;
    submitSolutionBtn.querySelector('.btn-text').innerText = 'AI Evaluating Solution...';

    try {
      const feedback = await ApiClient.submitAssignment(currentSlug, currentOrder, solution);
      showFeedbackModal(feedback);
    } catch (err) {
      App.showToast(err.message || 'Solution verification failed', 'error');
    } finally {
      submitSolutionBtn.disabled = false;
      submitSolutionBtn.querySelector('.btn-text').innerText = 'Submit Solution for AI Evaluation';
    }
  }

  function showFeedbackModal(feedback) {
    feedbackScoreBadge.innerText = `Score: ${feedback.score}/100`;
    feedbackVerdictChip.innerText = feedback.verdict || 'Reviewed';

    feedbackWellList.innerHTML = (feedback.whatWentWell || []).map(w => `<li>${escapeHtml(w)}</li>`).join('');
    feedbackCorrectionsList.innerHTML = (feedback.corrections || []).map(c => `<li>${escapeHtml(c)}</li>`).join('');
    feedbackTipsText.innerText = feedback.interviewTips || 'Keep honing your code structure for production-grade reliability.';

    feedbackModal.classList.remove('hidden');
  }

  function escapeHtml(str) {
    return (str || '').replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  return {
    init,
    loadAssignment
  };
})();
