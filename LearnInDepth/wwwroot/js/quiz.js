/* ==========================================================================
   LearnInDepth - Interactive Quiz Engine (js/quiz.js)
   ========================================================================== */

const QuizController = (function() {
  const quizForm = document.getElementById('quizForm');
  const quizTitle = document.getElementById('quizTitle');
  const quizQuestionsList = document.getElementById('quizQuestionsList');
  const quizHeaderScore = document.getElementById('quizHeaderScore');
  const submitQuizBtn = document.getElementById('submitQuizBtn');

  // Modal Elements
  const quizResultModal = document.getElementById('quizResultModal');
  const quizModalClose = document.getElementById('quizModalClose');
  const quizScoreText = document.getElementById('quizScoreText');
  const quizVerdictTitle = document.getElementById('quizVerdictTitle');
  const quizVerdictSubtitle = document.getElementById('quizVerdictSubtitle');
  const quizVerdictRing = document.getElementById('quizVerdictRing');
  const quizBreakdownList = document.getElementById('quizBreakdownList');
  const retakeQuizBtn = document.getElementById('retakeQuizBtn');
  const quizModalProceedBtn = document.getElementById('quizModalProceedBtn');

  let currentSlug = '';
  let currentOrder = 1;
  let quizData = null;
  let userAnswers = {};

  function init() {
    quizForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      await handleSubmit();
    });

    quizModalClose.addEventListener('click', () => {
      quizResultModal.classList.add('hidden');
    });

    retakeQuizBtn.addEventListener('click', () => {
      quizResultModal.classList.add('hidden');
      userAnswers = {};
      renderQuestions();
    });

    quizModalProceedBtn.addEventListener('click', () => {
      quizResultModal.classList.add('hidden');
      ReaderController.switchTab('assignment');
    });
  }

  async function loadQuiz(slug, order) {
    currentSlug = slug;
    currentOrder = order;
    userAnswers = {};
    quizHeaderScore.classList.add('hidden');
    quizQuestionsList.innerHTML = '<div style="padding: 24px; text-align: center; color: var(--text-muted);">Loading Quiz...</div>';

    try {
      quizData = await ApiClient.getChapterQuiz(slug, order);
      quizTitle.innerText = quizData.title || `Chapter ${order} Quiz`;
      renderQuestions();
    } catch (err) {
      quizQuestionsList.innerHTML = `<div style="padding: 24px; color: var(--accent-rose);">Failed to load quiz: ${err.message}</div>`;
    }
  }

  function renderQuestions() {
    if (!quizData || !quizData.questions) return;

    quizQuestionsList.innerHTML = quizData.questions.map(q => {
      const selectedIndex = userAnswers[q.questionNumber];
      return `
        <div class="quiz-card glass-card" data-qnum="${q.questionNumber}">
          <div class="quiz-card-header">
            <span class="q-num-pill">Question ${q.questionNumber}</span>
            <span class="diff-tag ${q.difficulty}">${q.difficulty}</span>
            ${q.interviewStyle ? '<span class="interview-tag">🎯 Interview Style</span>' : ''}
          </div>

          <p class="q-text">${escapeHtml(q.question)}</p>

          <div class="options-group">
            ${q.options.map((opt, idx) => `
              <div class="option-card ${selectedIndex === idx ? 'selected' : ''}" onclick="QuizController.selectOption(${q.questionNumber}, ${idx})">
                <span class="opt-index">${String.fromCharCode(65 + idx)}</span>
                <span>${escapeHtml(opt)}</span>
              </div>
            `).join('')}
          </div>
        </div>
      `;
    }).join('');
  }

  function selectOption(qNum, optIdx) {
    userAnswers[qNum] = optIdx;
    const card = quizQuestionsList.querySelector(`[data-qnum="${qNum}"]`);
    if (card) {
      card.querySelectorAll('.option-card').forEach((optCard, i) => {
        optCard.classList.toggle('selected', i === optIdx);
      });
    }
  }

  async function handleSubmit() {
    if (!quizData || !quizData.questions) return;

    // Validate that all questions are answered
    const unanswered = quizData.questions.filter(q => userAnswers[q.questionNumber] === undefined);
    if (unanswered.length > 0) {
      App.showToast(`Please answer all questions before submitting (${unanswered.length} remaining)`, 'error');
      return;
    }

    submitQuizBtn.disabled = true;
    submitQuizBtn.innerText = 'Evaluating...';

    try {
      const result = await ApiClient.submitQuiz(currentSlug, currentOrder, userAnswers);
      showResultModal(result);
    } catch (err) {
      App.showToast(err.message || 'Failed to submit quiz', 'error');
    } finally {
      submitQuizBtn.disabled = false;
      submitQuizBtn.innerText = 'Submit Quiz Answers';
    }
  }

  function showResultModal(result) {
    quizScoreText.innerText = `${result.scorePercent}%`;
    quizVerdictTitle.innerText = result.passed ? '🎉 Quiz Passed!' : 'Needs Review';
    quizVerdictSubtitle.innerText = `You answered ${result.correctCount} of ${result.totalQuestions} questions correctly (${result.scorePercent}%).`;

    if (result.passed) {
      quizVerdictRing.style.borderColor = 'var(--accent-emerald)';
      quizVerdictRing.style.background = 'rgba(16, 185, 129, 0.1)';
      quizScoreText.style.color = 'var(--accent-emerald)';
      quizHeaderScore.innerText = `Passed: ${result.scorePercent}%`;
      quizHeaderScore.classList.remove('hidden');
    } else {
      quizVerdictRing.style.borderColor = 'var(--accent-rose)';
      quizVerdictRing.style.background = 'rgba(244, 63, 94, 0.1)';
      quizScoreText.style.color = 'var(--accent-rose)';
    }

    quizBreakdownList.innerHTML = (result.results || []).map(r => `
      <div class="q-result-item ${r.isCorrect ? 'correct' : 'incorrect'}">
        <div class="q-res-title">
          ${r.isCorrect ? '✅ Question ' + r.questionNumber + ': Correct' : '❌ Question ' + r.questionNumber + ': Incorrect'}
        </div>
        <div class="q-res-exp">${escapeHtml(r.explanation || '')}</div>
      </div>
    `).join('');

    quizResultModal.classList.remove('hidden');
  }

  function escapeHtml(str) {
    return (str || '').replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  return {
    init,
    loadQuiz,
    selectOption
  };
})();
