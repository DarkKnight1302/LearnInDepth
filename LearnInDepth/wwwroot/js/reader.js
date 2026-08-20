/* ==========================================================================
   LearnInDepth - Course Plan & Reader Controller (js/reader.js)
   ========================================================================== */

const ReaderController = (function() {
  // Plan Overview Elements
  const planBackBtn = document.getElementById('planBackBtn');
  const planStatusBadge = document.getElementById('planStatusBadge');
  const planTopicTitle = document.getElementById('planTopicTitle');
  const planMetaText = document.getElementById('planMetaText');
  const planProgressPct = document.getElementById('planProgressPct');
  const chaptersOutlineGrid = document.getElementById('chaptersOutlineGrid');

  // Workspace Elements
  const workspaceBackBtn = document.getElementById('workspaceBackBtn');
  const sidebarTopicTitle = document.getElementById('sidebarTopicTitle');
  const sidebarChapterList = document.getElementById('sidebarChapterList');
  const chapterNumBadge = document.getElementById('chapterNumBadge');
  const chapterMainTitle = document.getElementById('chapterMainTitle');
  const readerSpinner = document.getElementById('readerSpinner');
  const readerHtmlBody = document.getElementById('readerHtmlBody');
  const prevChapterBtn = document.getElementById('prevChapterBtn');
  const proceedToQuizBtn = document.getElementById('proceedToQuizBtn');

  let currentPlan = null;
  let activeChapterOrder = 1;
  let currentProgress = null;

  function init() {
    planBackBtn.addEventListener('click', () => App.navigate('dashboard'));
    workspaceBackBtn.addEventListener('click', () => App.openPlan(currentPlan?.slug));

    // Tab Switcher
    document.querySelectorAll('.ws-tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const tab = btn.getAttribute('data-tab');
        switchTab(tab);
      });
    });

    prevChapterBtn.addEventListener('click', () => {
      if (activeChapterOrder > 1) {
        selectChapter(activeChapterOrder - 1);
      }
    });

    proceedToQuizBtn.addEventListener('click', () => {
      switchTab('quiz');
    });
  }

  async function loadPlan(slug) {
    App.showView('plan');
    planTopicTitle.innerText = 'Loading Course Outline...';
    chaptersOutlineGrid.innerHTML = '<div class="glass-card" style="padding:40px; grid-column: 1/-1; text-align:center;">Loading outline...</div>';

    try {
      currentPlan = await ApiClient.getTopicPlan(slug);
      try { currentProgress = await ApiClient.getProgress(slug); } catch {}

      renderPlanView();
    } catch (err) {
      App.showToast(err.message || 'Failed to load course plan', 'error');
    }
  }

  function renderPlanView() {
    if (!currentPlan) return;

    planTopicTitle.innerText = currentPlan.topic;
    planStatusBadge.innerText = currentPlan.status;
    planMetaText.innerText = `Created: ${new Date(currentPlan.createdAtUtc).toLocaleDateString()} | Total Chapters: ${currentPlan.chapters.length}`;

    const pct = currentProgress?.progressPercent || 0;
    planProgressPct.innerText = `${pct}%`;

    chaptersOutlineGrid.innerHTML = currentPlan.chapters.map(ch => `
      <div class="chapter-outline-card glass-card">
        <div>
          <div class="chapter-card-header">
            <div class="chapter-card-num">${ch.order}</div>
            <h3 class="chapter-card-title">${escapeHtml(ch.title)}</h3>
          </div>
          <p class="chapter-card-desc">${escapeHtml(ch.description || '')}</p>

          <div class="concepts-group">
            <span class="concepts-label">Key Concepts:</span>
            <div class="concept-tags">
              ${(ch.keyConcepts || []).map(k => `<span class="concept-pill">${escapeHtml(k)}</span>`).join('')}
            </div>
          </div>

          <div class="concepts-group">
            <span class="concepts-label">Interview Focus:</span>
            <div class="concept-tags">
              ${(ch.interviewFocus || []).map(f => `<span class="concept-pill interview">🎯 ${escapeHtml(f)}</span>`).join('')}
            </div>
          </div>
        </div>

        <div style="margin-top: 20px;">
          <button class="primary-btn w-full" onclick="ReaderController.openWorkspace('${currentPlan.slug}', ${ch.order})">
            Start Chapter ${ch.order} →
          </button>
        </div>
      </div>
    `).join('');
  }

  async function openWorkspace(slug, chapterOrder) {
    if (!currentPlan || currentPlan.slug !== slug) {
      currentPlan = await ApiClient.getTopicPlan(slug);
    }

    App.showView('workspace');
    sidebarTopicTitle.innerText = currentPlan.topic;
    renderSidebar();
    selectChapter(chapterOrder);
  }

  function renderSidebar() {
    sidebarChapterList.innerHTML = currentPlan.chapters.map(ch => {
      const isDone = currentProgress?.chapters?.[ch.order]?.quizPassed && currentProgress?.chapters?.[ch.order]?.assignmentVerdict;
      return `
        <button class="sidebar-ch-item ${ch.order === activeChapterOrder ? 'active' : ''}" onclick="ReaderController.selectChapter(${ch.order})">
          <span class="ch-dot-status ${isDone ? 'done' : ''}"></span>
          <span>Chapter ${ch.order}: ${escapeHtml(ch.title)}</span>
        </button>
      `;
    }).join('');
  }

  async function selectChapter(order) {
    activeChapterOrder = order;
    renderSidebar();

    const chObj = currentPlan.chapters.find(c => c.order === order);
    chapterNumBadge.innerText = `Chapter ${order} of ${currentPlan.chapters.length}`;
    chapterMainTitle.innerText = chObj ? chObj.title : `Chapter ${order}`;

    prevChapterBtn.style.visibility = order === 1 ? 'hidden' : 'visible';

    // Default to content tab
    switchTab('content');
    loadChapterContent(currentPlan.slug, order);
  }

  async function loadChapterContent(slug, order) {
    readerSpinner.classList.remove('hidden');
    readerHtmlBody.innerHTML = '';

    try {
      const content = await ApiClient.getChapterContent(slug, order);
      readerSpinner.classList.add('hidden');
      readerHtmlBody.innerHTML = content.htmlContent || '<p>No content available.</p>';
      enhanceCodeBlocks();
    } catch (err) {
      readerSpinner.classList.add('hidden');
      readerHtmlBody.innerHTML = `<div class="glass-card" style="padding:24px; color:var(--accent-rose);">Failed to load chapter content: ${err.message}</div>`;
    }
  }

  function enhanceCodeBlocks() {
    readerHtmlBody.querySelectorAll('pre').forEach(pre => {
      if (!pre.querySelector('.copy-code-btn')) {
        const copyBtn = document.createElement('button');
        copyBtn.className = 'editor-tool-btn copy-code-btn';
        copyBtn.style.cssText = 'position: absolute; top: 8px; right: 8px; font-size: 11px;';
        copyBtn.innerText = 'Copy';
        pre.style.position = 'relative';
        copyBtn.addEventListener('click', () => {
          navigator.clipboard.writeText(pre.innerText.replace('Copy', '').trim());
          copyBtn.innerText = 'Copied!';
          setTimeout(() => copyBtn.innerText = 'Copy', 2000);
        });
        pre.appendChild(copyBtn);
      }
    });
  }

  function switchTab(tabName) {
    document.querySelectorAll('.ws-tab-btn').forEach(btn => {
      btn.classList.toggle('active', btn.getAttribute('data-tab') === tabName);
    });

    document.querySelectorAll('.ws-tab-panel').forEach(panel => {
      panel.classList.remove('active');
    });

    if (tabName === 'content') {
      document.getElementById('panelContent').classList.add('active');
    } else if (tabName === 'quiz') {
      document.getElementById('panelQuiz').classList.add('active');
      QuizController.loadQuiz(currentPlan.slug, activeChapterOrder);
    } else if (tabName === 'assignment') {
      document.getElementById('panelAssignment').classList.add('active');
      AssignmentController.loadAssignment(currentPlan.slug, activeChapterOrder);
    }
  }

  function escapeHtml(str) {
    return (str || '').replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  return {
    init,
    loadPlan,
    openWorkspace,
    selectChapter,
    switchTab
  };
})();
