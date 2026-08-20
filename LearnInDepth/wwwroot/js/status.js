/* ==========================================================================
   LearnInDepth - Status Monitor Controller (js/status.js)
   ========================================================================== */

const StatusController = (function() {
  const viewStatus = document.getElementById('viewStatus');
  const statusTopicName = document.getElementById('statusTopicName');
  const statusSlugText = document.getElementById('statusSlugText');
  const statusProgressBar = document.getElementById('statusProgressBar');
  const statusPctText = document.getElementById('statusPctText');
  const statusChapterCount = document.getElementById('statusChapterCount');
  const chaptersStatusList = document.getElementById('chaptersStatusList');
  const statusLiveBadge = document.getElementById('statusLiveBadge');
  const statusBackBtn = document.getElementById('statusBackBtn');
  const refreshStatusBtn = document.getElementById('refreshStatusBtn');
  const openPlanFromStatusBtn = document.getElementById('openPlanFromStatusBtn');

  let activeSlug = null;
  let activeTopic = '';
  let pollInterval = null;

  function init() {
    statusBackBtn.addEventListener('click', () => {
      stopPolling();
      App.navigate('dashboard');
    });

    refreshStatusBtn.addEventListener('click', () => {
      if (activeSlug) fetchStatus();
    });

    openPlanFromStatusBtn.addEventListener('click', () => {
      stopPolling();
      App.openPlan(activeSlug);
    });
  }

  function monitor(slug, topicName) {
    activeSlug = slug;
    activeTopic = topicName || slug;

    statusTopicName.innerText = activeTopic;
    statusSlugText.innerText = `Slug: ${slug}`;
    openPlanFromStatusBtn.classList.add('hidden');

    App.showView('status');
    fetchStatus();
    startPolling();
  }

  function startPolling() {
    stopPolling();
    pollInterval = setInterval(fetchStatus, 3000);
  }

  function stopPolling() {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
  }

  async function fetchStatus() {
    if (!activeSlug) return;

    try {
      const statusData = await ApiClient.getTopicStatus(activeSlug);
      renderStatus(statusData);

      if (statusData.status === 'Ready' || statusData.percentComplete === 100) {
        stopPolling();
        statusLiveBadge.innerHTML = `✅ Course Ready`;
        statusLiveBadge.style.borderColor = 'rgba(16, 185, 129, 0.4)';
        statusLiveBadge.style.color = '#10b981';
        openPlanFromStatusBtn.classList.remove('hidden');
      } else {
        statusLiveBadge.innerHTML = `<span class="spinner-sm"></span> Generating Course...`;
      }
    } catch (err) {
      console.error('Failed to fetch status:', err);
    }
  }

  function renderStatus(data) {
    const pct = data.percentComplete || 0;
    statusProgressBar.style.width = `${pct}%`;
    statusPctText.innerText = `${pct}% Generated`;
    statusChapterCount.innerText = `${data.readyChapters || 0} / ${data.totalChapters || 0} Chapters Ready`;

    if (!data.chapters || data.chapters.length === 0) {
      chaptersStatusList.innerHTML = `
        <div class="glass-card" style="padding: 24px; text-align: center; color: var(--text-muted);">
          Initializing course outline structure...
        </div>
      `;
      return;
    }

    chaptersStatusList.innerHTML = data.chapters.map(ch => `
      <div class="chapter-status-row">
        <div class="chapter-row-left">
          <div class="chapter-index-badge">${ch.order}</div>
          <div>
            <div class="chapter-row-title">${escapeHtml(ch.title)}</div>
            ${ch.error ? `<div style="color: var(--accent-rose); font-size: 12px;">Error: ${escapeHtml(ch.error)}</div>` : ''}
          </div>
        </div>

        <div class="artifact-badges">
          <span class="artifact-tag ${ch.contentStatus}" title="Content">Content: ${ch.contentStatus}</span>
          <span class="artifact-tag ${ch.quizStatus}" title="Quiz">Quiz: ${ch.quizStatus}</span>
          <span class="artifact-tag ${ch.assignmentStatus}" title="Assignment">Assignment: ${ch.assignmentStatus}</span>
          ${(ch.contentStatus === 'Failed' || ch.quizStatus === 'Failed' || ch.assignmentStatus === 'Failed') ? `
            <button class="editor-tool-btn" onclick="StatusController.handleRetry(${ch.order})" style="color: var(--accent-rose); border-color: rgba(244,63,94,0.3);">Retry</button>
          ` : ''}
        </div>
      </div>
    `).join('');
  }

  async function handleRetry(order) {
    if (!activeSlug) return;
    try {
      await ApiClient.retryChapter(activeSlug, order);
      App.showToast(`Retry queued for Chapter ${order}`, 'info');
      fetchStatus();
    } catch (err) {
      App.showToast(err.message || 'Retry failed', 'error');
    }
  }

  function escapeHtml(str) {
    return (str || '').replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  return {
    init,
    monitor,
    stopPolling,
    handleRetry
  };
})();
